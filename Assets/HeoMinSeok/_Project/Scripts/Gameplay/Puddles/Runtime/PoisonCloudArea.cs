using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 독성 돌진과 독성 투하 패턴이 남기는 독구름 장판 피해와 소멸을 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class PoisonCloudArea : MonoBehaviour
    {
        [Header("Area")]
        [Tooltip("독구름 피해 판정 반지름입니다.")]
        [SerializeField, Min(0.05f)] private float radius = 0.75f;

        [Tooltip("독구름 피해 판정에 사용할 Trigger Circle Collider입니다.")]
        [SerializeField] private CircleCollider2D areaCollider;

        [Header("Lifetime")]
        [Tooltip("피해를 줄 수 있는 활성 유지 시간입니다.")]
        [SerializeField, Min(0f)] private float activeSeconds = 4f;

        [Tooltip("활성 시간이 끝난 뒤 피해 없이 투명해지며 사라지는 시간입니다.")]
        [SerializeField, Min(0f)] private float fadeSeconds = 1f;

        [Tooltip("소멸 시간이 끝나면 오브젝트를 Destroy합니다. 풀링 시에는 끄고 직접 회수하면 됩니다.")]
        [SerializeField] private bool destroyOnFinished = true;

        [Header("Sound")]
        [Tooltip("독구름이 존재하는 동안 반복 재생할 루프 사운드입니다. 비우면 재생하지 않습니다.")]
        [SerializeField] private SoundRef loopSound;

        [Tooltip("독구름 루프 사운드를 정리할 때 사용할 페이드아웃 시간입니다.")]
        [SerializeField, Min(0f)] private float loopFadeOutSeconds = 0.1f;

        [Header("Damage")]
        [Tooltip("독구름이 플레이어에게 적용할 GAS Damage Effect입니다.")]
        [SerializeField] private GE_Damage_Spec damageEffect;

        [Tooltip("독구름 접촉 시 플레이어에게 줄 피해량입니다.")]
        [SerializeField, Min(0f)] private float playerDamage = 1f;

        [Tooltip("같은 플레이어에게 반복 피해를 적용하는 간격입니다.")]
        [SerializeField, Min(0.05f)] private float damageIntervalSeconds = 1f;

        [Header("Visual")]
        [Tooltip("독구름 스프라이트 렌더러입니다. 스프라이트 교체는 이 렌더러에서 처리합니다.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("독구름 활성 상태 색상입니다.")]
        [SerializeField] private Color activeColor = new Color(0.28f, 0.95f, 0.18f, 0.65f);

        [Tooltip("자식 스프라이트 렌더러를 반지름 지름에 맞춰 자동 스케일합니다.")]
        [SerializeField] private bool scaleVisualToRadius = true;

        private readonly HashSet<GameObject> overlappingTargets = new HashSet<GameObject>();
        private readonly Dictionary<GameObject, float> nextDamageTimes = new Dictionary<GameObject, float>();
        private float elapsedSeconds;
        private bool isFading;
        private AudioHandle loopHandle;

        private void Awake()
        {
            CacheComponents();
            ApplyRadius();
        }

        private void OnEnable()
        {
            CacheComponents();
            ResetRuntimeState();
            StartLoopSound();
        }

        private void OnDisable()
        {
            StopLoopSound();
        }

        private void OnDestroy()
        {
            StopLoopSound();
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0.05f, radius);
            activeSeconds = Mathf.Max(0f, activeSeconds);
            fadeSeconds = Mathf.Max(0f, fadeSeconds);
            damageIntervalSeconds = Mathf.Max(0.05f, damageIntervalSeconds);
            CacheComponents();
            ApplyRadius();
            ApplyAlpha(1f);
        }

        private void Update()
        {
            elapsedSeconds += Time.deltaTime;

            if (!isFading && elapsedSeconds >= activeSeconds)
                BeginFade();

            if (!isFading)
            {
                ApplyPeriodicDamage();
                return;
            }

            UpdateFade();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isFading)
                return;

            GameObject target = ResolveDamageTarget(other);
            if (target == null)
                return;

            overlappingTargets.Add(target);
            ApplyDamage(target);
            nextDamageTimes[target] = Time.time + damageIntervalSeconds;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            GameObject target = ResolveDamageTarget(other);
            if (target == null)
                return;

            overlappingTargets.Remove(target);
            nextDamageTimes.Remove(target);
        }

        /// <summary>패턴 실행 중 생성된 독구름의 주요 수치를 초기화합니다.</summary>
        public void Initialize(
            float newRadius,
            float newActiveSeconds,
            float newFadeSeconds,
            float newPlayerDamage,
            float newDamageIntervalSeconds,
            GE_Damage_Spec newDamageEffect,
            SoundRef newLoopSound = default)
        {
            radius = Mathf.Max(0.05f, newRadius);
            activeSeconds = Mathf.Max(0f, newActiveSeconds);
            fadeSeconds = Mathf.Max(0f, newFadeSeconds);
            playerDamage = Mathf.Max(0f, newPlayerDamage);
            damageIntervalSeconds = Mathf.Max(0.05f, newDamageIntervalSeconds);

            if (newDamageEffect != null)
                damageEffect = newDamageEffect;

            loopSound = newLoopSound;

            ApplyRadius();
            ResetRuntimeState();
            StartLoopSound();
        }

        /// <summary>독구름을 피해 없는 페이드 상태로 전환합니다.</summary>
        private void BeginFade()
        {
            isFading = true;
            overlappingTargets.Clear();
            nextDamageTimes.Clear();

            if (areaCollider != null)
                areaCollider.enabled = false;

            if (fadeSeconds <= 0f)
                FinishLifetime();
        }

        /// <summary>페이드 진행도에 맞춰 독구름 투명도를 갱신합니다.</summary>
        private void UpdateFade()
        {
            if (fadeSeconds <= 0f)
                return;

            float fadeElapsed = Mathf.Max(0f, elapsedSeconds - activeSeconds);
            float normalizedFade = Mathf.Clamp01(fadeElapsed / fadeSeconds);
            ApplyAlpha(1f - normalizedFade);

            if (normalizedFade >= 1f)
                FinishLifetime();
        }

        /// <summary>독구름 수명이 끝났을 때 파괴하거나 비활성화합니다.</summary>
        private void FinishLifetime()
        {
            StopLoopSound();

            if (destroyOnFinished)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        /// <summary>독구름 안에 머무는 플레이어에게 주기 피해를 적용합니다.</summary>
        private void ApplyPeriodicDamage()
        {
            if (damageEffect == null || playerDamage <= 0f)
                return;

            List<GameObject> removedTargets = null;

            foreach (GameObject target in overlappingTargets)
            {
                if (target == null)
                {
                    if (removedTargets == null)
                        removedTargets = new List<GameObject>();

                    removedTargets.Add(target);
                    continue;
                }

                if (!nextDamageTimes.TryGetValue(target, out float nextDamageTime))
                    nextDamageTime = Time.time;

                if (Time.time < nextDamageTime)
                    continue;

                ApplyDamage(target);
                nextDamageTimes[target] = Time.time + damageIntervalSeconds;
            }

            if (removedTargets == null)
                return;

            for (int i = 0; i < removedTargets.Count; i++)
            {
                overlappingTargets.Remove(removedTargets[i]);
                nextDamageTimes.Remove(removedTargets[i]);
            }
        }

        /// <summary>지정 대상에게 환경 피해 경로로 독구름 피해를 적용합니다.</summary>
        private void ApplyDamage(GameObject target)
        {
            if (target == null || damageEffect == null || playerDamage <= 0f)
                return;

            AbilitySystem targetSystem = ResolveAbilitySystem(target);
            if (targetSystem == null)
                return;

            HazardDamageAction.ApplyDamage(
                targetSystem,
                target,
                damageEffect,
                playerDamage,
                gameObject,
                this);
        }

        /// <summary>콜라이더에서 지상 상태의 플레이어 피해 대상을 찾습니다.</summary>
        private GameObject ResolveDamageTarget(Collider2D other)
        {
            GameObject target = CombatTargetResolver2D.ResolveDamageTarget(other);
            if (target == null || !IsPlayerTarget(target))
                return null;

            return CombatHeightFilter2D.CanAffectGroundTarget(target) ? target : null;
        }

        /// <summary>대상 오브젝트 계층에서 AbilitySystem을 찾습니다.</summary>
        private static AbilitySystem ResolveAbilitySystem(GameObject target)
        {
            if (target == null)
                return null;

            AbilitySystem system = target.GetComponent<AbilitySystem>();
            if (system != null)
                return system;

            system = target.GetComponentInParent<AbilitySystem>();
            if (system != null)
                return system;

            return target.GetComponentInChildren<AbilitySystem>(true);
        }

        /// <summary>대상 오브젝트 계층에 플레이어 식별 컴포넌트가 있는지 확인합니다.</summary>
        private static bool IsPlayerTarget(GameObject target)
        {
            if (target == null)
                return false;

            return target.GetComponent<PlayerInteractor2D>() != null ||
                   target.GetComponentInParent<PlayerInteractor2D>() != null ||
                   target.GetComponentInChildren<PlayerInteractor2D>(true) != null;
        }

        /// <summary>독구름 런타임 수명과 피해 대상 기록을 초기 상태로 되돌립니다.</summary>
        private void ResetRuntimeState()
        {
            elapsedSeconds = 0f;
            isFading = false;
            overlappingTargets.Clear();
            nextDamageTimes.Clear();
            ApplyAlpha(1f);

            if (areaCollider != null)
                areaCollider.enabled = true;
        }

        /// <summary>독구름 실행에 필요한 컴포넌트 참조를 캐싱합니다.</summary>
        private void CacheComponents()
        {
            if (areaCollider == null)
                areaCollider = GetComponent<CircleCollider2D>();

            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        /// <summary>인스펙터 반지름 값을 Trigger Collider에 반영합니다.</summary>
        private void ApplyRadius()
        {
            if (areaCollider == null)
                return;

            areaCollider.isTrigger = true;
            areaCollider.radius = radius;
            ApplyVisualScale();
        }

        /// <summary>자식 스프라이트 렌더러의 크기를 피해 지름에 맞춥니다.</summary>
        private void ApplyVisualScale()
        {
            if (!scaleVisualToRadius || spriteRenderer == null || spriteRenderer.transform == transform)
                return;

            float diameter = radius * 2f;
            spriteRenderer.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        /// <summary>활성 색상을 기준으로 스프라이트 알파를 적용합니다.</summary>
        private void ApplyAlpha(float normalizedAlpha)
        {
            if (spriteRenderer == null)
                return;

            Color color = activeColor;
            color.a *= Mathf.Clamp01(normalizedAlpha);
            spriteRenderer.color = color;
        }

        /// <summary>독구름 수명과 함께 유지될 루프 사운드를 기존 오디오 시스템으로 시작합니다.</summary>
        private void StartLoopSound()
        {
            StopLoopSound();

            if (!loopSound.IsSet)
                return;

            loopHandle = SoundPlaybackUtility.Play(
                loopSound,
                gameObject,
                gameObject,
                null,
                transform.position,
                this);
        }

        /// <summary>독구름이 사라지거나 비활성화될 때 루프 사운드 핸들을 정리합니다.</summary>
        private void StopLoopSound()
        {
            if (!loopHandle.IsValid)
                return;

            SoundPlaybackUtility.Stop(loopHandle, loopFadeOutSeconds);
            loopHandle = AudioHandle.Invalid;
        }
    }
}

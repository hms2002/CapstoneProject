using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 불 장판의 수명, 주기 피해, 흡수 탄막 전환 전 바닥 위험 판정을 관리한다.
    /// - 환경 피해 적용은 기존 HazardDamageAction 경로를 사용해 전투 이벤트 규칙과 맞춘다.
    /// </summary>
    public sealed class FirePuddleArea : PuddleAreaBase
    {
        private static readonly SoundRef FireLoopSound = SoundRef.FromKey("sound_puddle_Fire");

        [Header("Lifetime")]
        [SerializeField, Min(0f)] private float lifetimeSeconds = 10f;

        [Header("Damage")]
        [SerializeField] private GE_Damage_Spec damageEffect;
        [SerializeField] private AttributeDefinition maxHealthAttribute;
        [SerializeField, Min(0f)] private float damageIntervalSeconds = 1.5f;
        [SerializeField, Min(0f)] private float playerDamage = 1f;
        [SerializeField, Range(0f, 1f)] private float enemyMaxHpDamageRatio = 0.1f;
        [SerializeField, Min(0f)] private float projectilePlayerDamage = 1f;

        [Header("Presentation")]
        [SerializeField] private Color fireColor = new(1f, 0.18f, 0.04f, 0.8f);

        private readonly HashSet<GameObject> overlappingTargets = new();
        private Coroutine directIgnitionScanRoutine;
        private AudioHandle fireLoopHandle;
        private float expireTime;
        private float nextDamageTime;

        public override PuddleElementType ElementType => PuddleElementType.Fire;

        protected override void Awake()
        {
            base.Awake();
            // Blob visual + outline은 Noita풍 particle field 전환 테스트 동안 비활성화한다.
            // BlobVisual?.SetColor(fireColor);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            expireTime = lifetimeSeconds > 0f ? Time.time + lifetimeSeconds : float.PositiveInfinity;
            nextDamageTime = Time.time + Mathf.Max(0.01f, damageIntervalSeconds);
            directIgnitionScanRoutine = StartCoroutine(IgniteDirectOverlappingAlcoholNextFrame());
            TryStartFireLoop();
        }

        protected override void OnDisable()
        {
            if (directIgnitionScanRoutine != null)
            {
                StopCoroutine(directIgnitionScanRoutine);
                directIgnitionScanRoutine = null;
            }

            StopFireLoop();
            base.OnDisable();
        }

        private void Update()
        {
            if (!CanApplyGroundEffect)
                return;

            if (Time.time >= expireTime)
            {
                MarkConsumed();
                gameObject.SetActive(false);
                return;
            }

            if (Time.time < nextDamageTime)
                return;

            nextDamageTime = Time.time + Mathf.Max(0.01f, damageIntervalSeconds);
            ApplyPeriodicDamage();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (CanApplyProjectileEffect)
            {
                TryApplyProjectileDamage(other);
                return;
            }

            if (!CanApplyGroundEffect)
                return;

            TryIgniteAlcohol(other);

            GameObject target = ResolvePuddleTarget(other);
            if (target != null && CanApplyGroundEffectTo(target))
                overlappingTargets.Add(target);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            GameObject target = ResolvePuddleTarget(other);
            if (target != null)
                overlappingTargets.Remove(target);
        }

        private void TryIgniteAlcohol(Collider2D other)
        {
            if (other == null)
                return;

            AlcoholPuddleArea alcohol = other.GetComponentInParent<AlcoholPuddleArea>();
            if (alcohol == null)
                return;

            if (!PuddleManager.AreIgnitionContactAreasOverlapping(this, alcohol))
                return;

            alcohol.RequestIgnite();
        }

        private IEnumerator IgniteDirectOverlappingAlcoholNextFrame()
        {
            yield return new WaitForFixedUpdate();
            directIgnitionScanRoutine = null;

            if (!CanApplyGroundEffect)
                yield break;

            PuddleManager manager = PuddleManager.ResolveForScene();
            List<AlcoholPuddleArea> alcoholPuddles = manager != null
                ? manager.CollectDirectIgnitableAlcoholPuddles(this)
                : null;

            if (alcoholPuddles == null)
                yield break;

            for (int i = 0; i < alcoholPuddles.Count; i++)
                alcoholPuddles[i]?.RequestIgnite();
        }

        protected override void HandleModeChanged(PuddleAreaMode previousMode, PuddleAreaMode newMode)
        {
            if (!CanApplyGroundEffect)
            {
                overlappingTargets.Clear();
                StopFireLoop();
            }
            else
            {
                TryStartFireLoop();
            }
        }

        /// <summary>불 장판이 ground hazard로 활성화된 동안 루프 사운드를 유지합니다.</summary>
        private void TryStartFireLoop()
        {
            if (!CanApplyGroundEffect || fireLoopHandle.IsValid)
                return;

            fireLoopHandle = SoundPlaybackUtility.Play(FireLoopSound, causer: gameObject, position: transform.position, sourceObject: this);
        }

        /// <summary>불 장판이 꺼지거나 다른 모드로 전환될 때 남은 루프 사운드를 정지합니다.</summary>
        private void StopFireLoop()
        {
            if (!fireLoopHandle.IsValid)
                return;

            SoundPlaybackUtility.Stop(fireLoopHandle, 0.08f);
            fireLoopHandle = AudioHandle.Invalid;
        }

        private void ApplyPeriodicDamage()
        {
            if (damageEffect == null)
                return;

            foreach (GameObject target in overlappingTargets)
            {
                if (target == null)
                    continue;

                if (!CanApplyGroundEffectTo(target))
                    continue;

                if (IsBossTarget(target))
                    continue;

                AbilitySystem targetSystem = ResolveAbilitySystem(target);
                if (targetSystem == null)
                    continue;

                float finalDamage = ResolveDamageAmount(target);
                HazardDamageAction.ApplyDamage(
                    targetSystem,
                    target,
                    damageEffect,
                    finalDamage,
                    gameObject,
                    this);
            }
        }

        private void TryApplyProjectileDamage(Collider2D other)
        {
            if (damageEffect == null)
                return;

            GameObject target = ResolvePuddleTarget(other);
            if (target == null || !IsPlayerTarget(target))
                return;

            if (!TryRegisterProjectileHit(target))
                return;

            AbilitySystem targetSystem = ResolveAbilitySystem(target);
            if (targetSystem == null)
                return;

            HazardDamageAction.ApplyDamage(
                targetSystem,
                target,
                damageEffect,
                projectilePlayerDamage,
                gameObject,
                this);

            ConsumeAfterProjectileHit();
        }

        private void ConsumeAfterProjectileHit()
        {
            MarkConsumed();
            gameObject.SetActive(false);
        }

        private float ResolveDamageAmount(GameObject target)
        {
            if (IsPlayerTarget(target))
                return playerDamage;

            if (maxHealthAttribute == null)
                return playerDamage;

            AttributeSet attributes = target.GetComponent<AttributeSet>();
            if (attributes == null)
                attributes = target.GetComponentInParent<AttributeSet>();

            if (attributes == null)
                return playerDamage;

            float maxHealth = Mathf.Max(0f, attributes.GetAttributeValue(maxHealthAttribute));
            return maxHealth * enemyMaxHpDamageRatio;
        }

        private static bool IsBossTarget(GameObject target)
        {
            if (target == null)
                return false;

            return target.GetComponent<Boss>() != null ||
                   target.GetComponentInParent<Boss>() != null ||
                   target.GetComponentInChildren<Boss>(true) != null ||
                   target.GetComponent<BossControllerBase>() != null ||
                   target.GetComponentInParent<BossControllerBase>() != null ||
                   target.GetComponentInChildren<BossControllerBase>(true) != null;
        }
    }
}

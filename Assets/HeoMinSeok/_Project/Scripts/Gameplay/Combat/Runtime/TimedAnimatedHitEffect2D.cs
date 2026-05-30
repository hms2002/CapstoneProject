using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - 정해진 수명에 맞춰 이펙트 Animator 재생 속도를 보정한다.
    /// - 애니메이션 이벤트로 피해 콜라이더 활성/비활성 및 이펙트 회수를 제어한다.
    /// - 활성화된 피해 콜라이더가 감지한 대상에게 생성 시점 CombatHitPayload를 적용한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimedAnimatedHitEffect2D : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip referenceClip;

        [Header("Hit")]
        [SerializeField] private Collider2D[] hitColliders;
        [SerializeField] private LayerMask targetLayers = 1 << 3;
        [SerializeField] private bool applyOnlyOncePerEffect = true;

        [Header("Lifetime")]
        [SerializeField] private bool destroyOnFinished = true;

        private readonly Collider2D[] overlapResults = new Collider2D[16];
        private readonly HashSet<GameObject> localHitTargets = new();
        private CombatHitPayload payload;
        private SharedHitRegistry sharedRegistry;
        private Action hitWindowOpenedCallback;
        private ContactFilter2D contactFilter;
        private Coroutine lifetimeCoroutine;
        private float originalAnimatorSpeed = 1f;
        private bool hasOriginalAnimatorSpeed;
        private bool isCollected;
        private bool hitWindowOpenedCallbackInvoked;

        public void Play(float lifetimeSeconds, CombatHitPayload hitPayload, SharedHitRegistry registry = null)
        {
            Play(lifetimeSeconds, hitPayload, registry, null);
        }

        public void Play(
            float lifetimeSeconds,
            CombatHitPayload hitPayload,
            SharedHitRegistry registry,
            Action onHitWindowOpened)
        {
            payload = hitPayload;
            sharedRegistry = registry;
            hitWindowOpenedCallback = onHitWindowOpened;
            localHitTargets.Clear();
            isCollected = false;
            hitWindowOpenedCallbackInvoked = false;

            CacheReferences();
            ConfigureContactFilter();
            DisableHitCollision();
            ApplyAnimatorSpeed(lifetimeSeconds);

            if (lifetimeCoroutine != null)
                StopCoroutine(lifetimeCoroutine);

            float resolvedLifetime = ResolveLifetimeSeconds(lifetimeSeconds);
            if (resolvedLifetime > 0f)
                lifetimeCoroutine = StartCoroutine(CollectAfterDelay(resolvedLifetime));
        }

        /// <summary>애니메이션 이벤트에서 호출해 피해 콜라이더를 켜고 현재 겹친 대상을 즉시 검사한다.</summary>
        public void EnableHitCollision()
        {
            InvokeHitWindowOpenedCallbackIfNeeded();
            SetHitCollisionEnabled(true);
            CheckCurrentOverlaps();
        }

        /// <summary>애니메이션 이벤트에서 호출해 피해 콜라이더를 끈다.</summary>
        public void DisableHitCollision()
        {
            SetHitCollisionEnabled(false);
        }

        /// <summary>애니메이션 이벤트 또는 수명 타이머에서 호출해 이펙트를 회수한다.</summary>
        public void CollectEffect()
        {
            if (isCollected)
                return;

            isCollected = true;
            DisableHitCollision();
            RestoreAnimatorSpeed();
            hitWindowOpenedCallback = null;

            if (lifetimeCoroutine != null)
            {
                StopCoroutine(lifetimeCoroutine);
                lifetimeCoroutine = null;
            }

            if (destroyOnFinished)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }

        private void Awake()
        {
            CacheReferences();
            DisableHitCollision();
        }

        private void OnDisable()
        {
            DisableHitCollision();
            RestoreAnimatorSpeed();
            hitWindowOpenedCallback = null;

            if (lifetimeCoroutine != null)
            {
                StopCoroutine(lifetimeCoroutine);
                lifetimeCoroutine = null;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryApplyHit(other);
        }

        private IEnumerator CollectAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            lifetimeCoroutine = null;
            CollectEffect();
        }

        private void CacheReferences()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            if (hitColliders == null || hitColliders.Length == 0)
                hitColliders = GetComponentsInChildren<Collider2D>(true);

            if (animator != null && !hasOriginalAnimatorSpeed)
            {
                originalAnimatorSpeed = animator.speed;
                hasOriginalAnimatorSpeed = true;
            }
        }

        public void SetReferenceClip(AnimationClip clip)
        {
            referenceClip = clip;
        }

        public void ConfigureHitCollision(Collider2D[] colliders, LayerMask layers, bool applyOnlyOnce = true)
        {
            DisableHitCollision();
            hitColliders = colliders;
            targetLayers = layers;
            applyOnlyOncePerEffect = applyOnlyOnce;
            ConfigureContactFilter();
            DisableHitCollision();
        }

        private void ApplyAnimatorSpeed(float lifetimeSeconds)
        {
            if (animator == null || lifetimeSeconds <= 0f)
                return;

            AnimationClip clip = ResolveReferenceClip();
            if (clip == null || clip.length <= 0f)
                return;

            animator.speed = clip.length / Mathf.Max(0.01f, lifetimeSeconds);
        }

        private void RestoreAnimatorSpeed()
        {
            if (animator != null && hasOriginalAnimatorSpeed)
                animator.speed = originalAnimatorSpeed;
        }

        private float ResolveLifetimeSeconds(float requestedLifetimeSeconds)
        {
            if (requestedLifetimeSeconds > 0f)
                return requestedLifetimeSeconds;

            AnimationClip clip = ResolveReferenceClip();
            if (clip == null || clip.length <= 0f)
                return 0f;

            float speed = animator != null ? Mathf.Max(0.01f, animator.speed) : 1f;
            return clip.length / speed;
        }

        private AnimationClip ResolveReferenceClip()
        {
            if (referenceClip != null)
                return referenceClip;

            if (animator == null || animator.runtimeAnimatorController == null)
                return null;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            return clips != null && clips.Length > 0 ? clips[0] : null;
        }

        private void ConfigureContactFilter()
        {
            contactFilter = new ContactFilter2D
            {
                useLayerMask = targetLayers.value != 0,
                layerMask = targetLayers,
                useTriggers = true
            };
        }

        private void SetHitCollisionEnabled(bool isEnabled)
        {
            if (hitColliders == null)
                return;

            for (int i = 0; i < hitColliders.Length; i++)
            {
                if (hitColliders[i] == null)
                    continue;

                hitColliders[i].enabled = isEnabled;
            }
        }

        private void CheckCurrentOverlaps()
        {
            if (hitColliders == null)
                return;

            for (int i = 0; i < hitColliders.Length; i++)
            {
                Collider2D hitCollider = hitColliders[i];
                if (hitCollider == null || !hitCollider.enabled)
                    continue;

                int hitCount = hitCollider.Overlap(contactFilter, overlapResults);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    TryApplyHit(overlapResults[hitIndex]);
                    overlapResults[hitIndex] = null;
                }
            }
        }

        private void InvokeHitWindowOpenedCallbackIfNeeded()
        {
            if (hitWindowOpenedCallbackInvoked)
                return;

            hitWindowOpenedCallbackInvoked = true;
            hitWindowOpenedCallback?.Invoke();
        }

        private void TryApplyHit(Collider2D other)
        {
            if (other == null || payload == null || !payload.IsValid())
                return;

            if (targetLayers.value != 0 && (targetLayers.value & (1 << other.gameObject.layer)) == 0)
                return;

            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(other);
            if (targetRoot == null)
                return;

            if (applyOnlyOncePerEffect && !localHitTargets.Add(targetRoot))
                return;

            if (sharedRegistry != null && !sharedRegistry.TryRegister(targetRoot))
                return;

            Vector3 hitPoint = other.ClosestPoint(transform.position);
            CombatHitPayloadApplier.Apply(targetRoot, payload, hitPoint);
        }

        /// <summary>
        /// 책임:
        /// - 여러 TimedAnimatedHitEffect2D 인스턴스가 같은 패턴 실행 안에서 동일 대상을 중복 타격하지 않도록 공유 적중 목록을 관리한다.
        /// </summary>
        public sealed class SharedHitRegistry
        {
            private readonly HashSet<GameObject> hitTargets = new();

            public bool TryRegister(GameObject target)
            {
                return target != null && hitTargets.Add(target);
            }
        }
    }
}

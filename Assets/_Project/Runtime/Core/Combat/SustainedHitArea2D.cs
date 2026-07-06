using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - 일정 시간 켜져 있는 전투 이펙트의 hit collider를 주기적으로 재검사한다.
    /// - OnTriggerEnter 누락이나 이미 겹친 상태를 보완하고, 실제 피해 성공 시에만 다음 피해 쿨다운을 소비한다.
    /// - 등록된 hit collider가 안전한 trigger 판정으로 동작하도록 런타임에서 보정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SustainedHitArea2D : MonoBehaviour
    {
        private readonly Collider2D[] overlapResults = new Collider2D[32];
        private readonly Dictionary<GameObject, float> nextDamageTimes = new();

        [Header("Hit")]
        [SerializeField] private Collider2D[] hitColliders;
        [SerializeField] private LayerMask targetLayers = 1 << 3;
        [SerializeField, Min(0.01f)] private float damageIntervalSeconds = 0.25f;
        [SerializeField] private bool applyImmediatelyOnEnable = true;
        [SerializeField] private bool debugLog;

        private CombatHitPayload payload;
        private SharedHitRegistry2D sharedRegistry;
        private ContactFilter2D contactFilter;
        private Coroutine tickRoutine;
        private bool isActive;

        private void Awake()
        {
            CacheReferences();
            ConfigureContactFilter();
            EnforceColliderSettings(disableColliders: true);
        }

        private void OnValidate()
        {
            CacheReferences();
            EnforceColliderSettings(disableColliders: false);
        }

        private void OnDisable()
        {
            Stop();
        }

        public void Configure(
            Collider2D[] colliders,
            LayerMask layers,
            float intervalSeconds,
            bool applyImmediately,
            bool logDebug)
        {
            Stop();
            hitColliders = colliders;
            targetLayers = layers;
            damageIntervalSeconds = Mathf.Max(0.01f, intervalSeconds);
            applyImmediatelyOnEnable = applyImmediately;
            debugLog = logDebug;
            ConfigureContactFilter();
            EnforceColliderSettings(disableColliders: true);
        }

        public void Play(CombatHitPayload hitPayload, SharedHitRegistry2D registry = null)
        {
            Stop();

            payload = hitPayload;
            sharedRegistry = registry;
            nextDamageTimes.Clear();

            if (payload == null || !payload.IsValid())
            {
                Log("blocked: invalid payload.");
                return;
            }

            CacheReferences();
            ConfigureContactFilter();
            EnforceColliderSettings(disableColliders: false);
            SetHitCollisionEnabled(true);
            isActive = true;

            if (applyImmediatelyOnEnable)
                ScanAndApply();

            tickRoutine = StartCoroutine(TickRoutine());
        }

        public void Stop()
        {
            if (tickRoutine != null)
            {
                StopCoroutine(tickRoutine);
                tickRoutine = null;
            }

            isActive = false;
            SetHitCollisionEnabled(false);
            nextDamageTimes.Clear();
            payload = null;
            sharedRegistry = null;
        }

        private IEnumerator TickRoutine()
        {
            WaitForSeconds wait = new(Mathf.Max(0.01f, damageIntervalSeconds));
            while (isActive)
            {
                yield return wait;
                ScanAndApply();
            }
        }

        private void CacheReferences()
        {
            if (hitColliders == null || hitColliders.Length == 0)
                hitColliders = GetComponentsInChildren<Collider2D>(true);
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

        private void EnforceColliderSettings(bool disableColliders)
        {
            if (hitColliders == null)
                return;

            for (int i = 0; i < hitColliders.Length; i++)
            {
                Collider2D hitCollider = hitColliders[i];
                if (hitCollider == null)
                    continue;

                hitCollider.isTrigger = true;
                if (disableColliders)
                    hitCollider.enabled = false;
            }
        }

        private void SetHitCollisionEnabled(bool isEnabled)
        {
            if (hitColliders == null)
                return;

            for (int i = 0; i < hitColliders.Length; i++)
            {
                if (hitColliders[i] != null)
                    hitColliders[i].enabled = isEnabled;
            }
        }

        private void ScanAndApply()
        {
            if (!isActive || payload == null || !payload.IsValid() || hitColliders == null)
                return;

            for (int i = 0; i < hitColliders.Length; i++)
            {
                Collider2D hitCollider = hitColliders[i];
                if (hitCollider == null || !hitCollider.enabled)
                    continue;

                int hitCount = hitCollider.Overlap(contactFilter, overlapResults);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    TryApplyHit(overlapResults[hitIndex], hitCollider);
                    overlapResults[hitIndex] = null;
                }
            }
        }

        private void TryApplyHit(Collider2D other, Collider2D sourceCollider)
        {
            if (other == null)
                return;

            if (targetLayers.value != 0 && (targetLayers.value & (1 << other.gameObject.layer)) == 0)
                return;

            GameObject targetRoot = CombatTargetResolver2D.ResolveDamageTarget(other);
            if (targetRoot == null)
                return;

            if (sharedRegistry != null && sharedRegistry.Contains(targetRoot))
                return;

            if (nextDamageTimes.TryGetValue(targetRoot, out float nextDamageTime) && Time.time < nextDamageTime)
                return;

            Vector3 hitPoint = other.ClosestPoint(sourceCollider != null ? sourceCollider.bounds.center : transform.position);
            if (!CombatHitPayloadApplier.Apply(targetRoot, payload, hitPoint))
                return;

            sharedRegistry?.Register(targetRoot);
            nextDamageTimes[targetRoot] = Time.time + Mathf.Max(0.01f, damageIntervalSeconds);
            Log($"applied. target={targetRoot.name}, next={nextDamageTimes[targetRoot]:0.###}");
        }

        private void Log(string message)
        {
            if (debugLog)
                Debug.Log($"[{nameof(SustainedHitArea2D)}] {message}", this);
        }
    }
}

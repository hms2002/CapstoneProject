using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 엔티티의 몸체 충돌 콜라이더가 어떤 레이어와 물리 충돌할지 런타임 모드로 관리한다.
    /// - 피해 판정용 hitbox/hurtbox와 이동 방해용 body collider의 책임을 분리하기 위한 공통 진입점을 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityCollisionProfile2D : MonoBehaviour
    {
        public enum BodyCollisionMode
        {
            Normal,
            PassThroughActors,
            Disabled
        }

        [Header("Body Colliders")]
        [SerializeField] private Collider2D[] bodyColliders;

        [Header("Layers")]
        [Tooltip("모드와 무관하게 body collider에서 제외할 레이어입니다.")]
        [SerializeField] private LayerMask baseExcludedLayers;

        [Tooltip("PassThroughActors 모드에서 body collider가 통과할 액터 레이어입니다. 예: Player, Enemy")]
        [SerializeField] private LayerMask actorLayers;

        [Header("Mode")]
        [SerializeField] private BodyCollisionMode defaultMode = BodyCollisionMode.PassThroughActors;

        private BodyCollisionMode currentMode;

        public BodyCollisionMode CurrentMode => currentMode;

        /// <summary>런타임/authoring 보정 경로에서 body collider와 충돌 정책을 한 번에 지정합니다.</summary>
        public void Configure(
            Collider2D[] managedBodyColliders,
            LayerMask excludedLayers,
            LayerMask passThroughActorLayers,
            BodyCollisionMode profileDefaultMode,
            bool applyImmediately = true)
        {
            bodyColliders = managedBodyColliders;
            baseExcludedLayers = excludedLayers;
            actorLayers = passThroughActorLayers;
            defaultMode = profileDefaultMode;

            CacheBodyCollidersIfNeeded();

            if (applyImmediately)
                ApplyMode(defaultMode);
        }

        private void Awake()
        {
            CacheBodyCollidersIfNeeded();
            ApplyMode(defaultMode);
        }

        private void OnEnable()
        {
            ApplyMode(currentMode);
        }

        private void OnValidate()
        {
            CacheBodyCollidersIfNeeded();
            currentMode = defaultMode;
            ApplyMode(currentMode);
        }

        /// <summary>몸체 충돌 모드를 변경해 액터 통과, 일반 충돌, 비활성화를 전환한다.</summary>
        public void SetBodyCollisionMode(BodyCollisionMode mode)
        {
            ApplyMode(mode);
        }

        /// <summary>몸체 충돌 모드를 프리팹/인스펙터 기본값으로 되돌린다.</summary>
        public void RestoreDefaultMode()
        {
            ApplyMode(defaultMode);
        }

        private void CacheBodyCollidersIfNeeded()
        {
            if (bodyColliders != null && bodyColliders.Length > 0)
                return;

            Collider2D[] candidates = GetComponentsInChildren<Collider2D>(true);
            int bodyCount = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (IsBodyCollider(candidates[i]))
                    bodyCount++;
            }

            bodyColliders = new Collider2D[bodyCount];
            int writeIndex = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                Collider2D candidate = candidates[i];
                if (!IsBodyCollider(candidate))
                    continue;

                bodyColliders[writeIndex] = candidate;
                writeIndex++;
            }
        }

        /// <summary>
        /// 책임 :
        /// - EntityCollisionProfile2D가 관리할 이동 방해용 body collider만 선별한다.
        /// - 피해 판정용 trigger는 combat 계층이 관리하므로 body profile에서 제외한다.
        /// - 기존 프리팹처럼 non-trigger 몸체 콜라이더가 CombatHurtbox2D도 겸하는 경우에는 body profile 적용 대상에 포함한다.
        /// </summary>
        private static bool IsBodyCollider(Collider2D candidate)
        {
            if (candidate == null)
                return false;

            if (candidate.isTrigger)
                return false;

            return true;
        }

        private void ApplyMode(BodyCollisionMode mode)
        {
            currentMode = mode;

            if (bodyColliders == null)
                return;

            for (int i = 0; i < bodyColliders.Length; i++)
            {
                Collider2D bodyCollider = bodyColliders[i];
                if (bodyCollider == null)
                    continue;

                bodyCollider.enabled = mode != BodyCollisionMode.Disabled;
                if (mode == BodyCollisionMode.Disabled)
                    continue;

                LayerMask excludedLayers = baseExcludedLayers;
                if (mode == BodyCollisionMode.PassThroughActors)
                    excludedLayers |= actorLayers;

                bodyCollider.excludeLayers = excludedLayers;
            }
        }
    }
}

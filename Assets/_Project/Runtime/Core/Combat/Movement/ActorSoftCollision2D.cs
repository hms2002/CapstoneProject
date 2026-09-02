using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// 액터끼리의 단단한 물리 충돌 대신, 겹침을 감지해 벽 안으로 밀어 넣지 않는 부드러운 분리 외압을 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorSoftCollision2D : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Collider2D bodyCollider;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private ExternalMovementController2D externalMovement;
        [SerializeField] private EntityCollisionProfile2D collisionProfile;

        [Header("Layers")]
        [Tooltip("부드럽게 미끄러질 대상 액터 레이어입니다. 예: Enemy")]
        [SerializeField] private LayerMask actorLayers;
        [Tooltip("이 방향으로 밀면 벽에 박히는지 검사할 레이어입니다.")]
        [SerializeField] private LayerMask wallLayers;

        [Header("Push")]
        [Tooltip("EntityCollisionProfile2D가 액터 통과 모드일 때 부드러운 분리 외압도 멈춥니다.")]
        [SerializeField] private bool suspendWhileBodyPassesThroughActors;
        [SerializeField, Min(0f)] private float pushSpeed = 2.8f;
        [Tooltip("값이 클수록 같은 겹침에서도 덜 밀립니다. Tank처럼 무거운 액터의 소프트 분리 저항에 사용합니다.")]
        [SerializeField, Min(0.05f)] private float pushResistance = 1f;
        [SerializeField, Min(0.01f)] private float pushDurationSeconds = 0.08f;
        [SerializeField, Min(0f)] private float wallProbeDistance = 0.08f;
        [SerializeField, Min(1)] private int maxActorsPerTick = 8;

        private readonly Collider2D[] actorHits = new Collider2D[16];
        private readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];
        private ContactFilter2D actorFilter;
        private ContactFilter2D wallFilter;

        public float PushResistance => pushResistance;

        /// <summary>
        /// 책임:
        /// 프리팹 authoring 도구가 자식 body collider와 이동 구성, 역할별 밀림 저항을 명시적으로 저장할 수 있게 한다.
        /// </summary>
        public void Configure(
            Collider2D configuredBodyCollider,
            Rigidbody2D configuredBody,
            ExternalMovementController2D configuredExternalMovement,
            EntityCollisionProfile2D configuredCollisionProfile,
            LayerMask configuredActorLayers,
            LayerMask configuredWallLayers,
            bool suspendForPassThroughMode,
            float configuredPushSpeed,
            float configuredPushResistance,
            float configuredPushDurationSeconds,
            float configuredWallProbeDistance,
            int configuredMaxActorsPerTick)
        {
            bodyCollider = configuredBodyCollider;
            body = configuredBody;
            externalMovement = configuredExternalMovement;
            collisionProfile = configuredCollisionProfile;
            actorLayers = configuredActorLayers;
            wallLayers = configuredWallLayers;
            suspendWhileBodyPassesThroughActors = suspendForPassThroughMode;
            pushSpeed = Mathf.Max(0f, configuredPushSpeed);
            pushResistance = Mathf.Max(0.05f, configuredPushResistance);
            pushDurationSeconds = Mathf.Max(0.01f, configuredPushDurationSeconds);
            wallProbeDistance = Mathf.Max(0f, configuredWallProbeDistance);
            maxActorsPerTick = Mathf.Clamp(configuredMaxActorsPerTick, 1, actorHits.Length);

            ConfigureFilters();
        }

        private void Awake()
        {
            if (bodyCollider == null)
                bodyCollider = ResolveBodyCollider();

            if (body == null)
                body = GetComponent<Rigidbody2D>();

            if (externalMovement == null)
                externalMovement = GetComponent<ExternalMovementController2D>();

            if (collisionProfile == null)
                collisionProfile = GetComponent<EntityCollisionProfile2D>();

            ConfigureFilters();
        }

        private void FixedUpdate()
        {
            if (ShouldSuspendForCollisionProfile())
            {
                externalMovement?.RemoveTimedVelocitiesFromSource(this);
                return;
            }

            if (bodyCollider == null || externalMovement == null || actorLayers.value == 0 || pushSpeed <= 0f)
                return;

            Vector2 pushDirection = ResolvePushDirection();
            externalMovement.RemoveTimedVelocitiesFromSource(this);

            if (pushDirection.sqrMagnitude <= 0.0001f)
                return;

            externalMovement.AddTimedVelocity(
                pushDirection.normalized * (pushSpeed / Mathf.Max(0.05f, pushResistance)),
                pushDurationSeconds,
                source: this);
        }

        /// <summary>
        /// 책임:
        /// 현재 액터와 겹친 다른 액터들을 조사해, 안전하게 미끄러질 평균 분리 방향을 계산한다.
        /// </summary>
        private Vector2 ResolvePushDirection()
        {
            int hitCount = bodyCollider.Overlap(actorFilter, actorHits);
            if (hitCount <= 0)
                return Vector2.zero;

            Vector2 selfCenter = ResolveColliderCenter(bodyCollider);
            Vector2 sum = Vector2.zero;
            int acceptedCount = 0;
            int cappedCount = Mathf.Min(hitCount, Mathf.Min(maxActorsPerTick, actorHits.Length));

            for (int i = 0; i < cappedCount; i++)
            {
                Collider2D other = actorHits[i];
                if (!IsValidActorCollider(other))
                    continue;

                Vector2 direction = selfCenter - ResolveColliderCenter(other);
                if (direction.sqrMagnitude <= 0.0001f)
                    direction = ResolveFallbackDirection(other);

                if (direction.sqrMagnitude <= 0.0001f)
                    continue;

                direction.Normalize();
                if (IsWallBlocked(direction))
                    continue;

                sum += direction;
                acceptedCount++;
            }

            return acceptedCount > 0 ? sum / acceptedCount : Vector2.zero;
        }

        /// <summary>
        /// 책임:
        /// EntityCollisionProfile2D가 액터 통과/비활성 상태일 때 소프트 충돌 외압이 통과 정책을 다시 막지 않도록 한다.
        /// </summary>
        private bool ShouldSuspendForCollisionProfile()
        {
            if (!suspendWhileBodyPassesThroughActors || collisionProfile == null)
                return false;

            return collisionProfile.CurrentMode != EntityCollisionProfile2D.BodyCollisionMode.Normal;
        }

        private Collider2D ResolveBodyCollider()
        {
            Collider2D[] colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D candidate = colliders[i];
                if (candidate != null && candidate.enabled && !candidate.isTrigger)
                    return candidate;
            }

            return null;
        }

        private void ConfigureFilters()
        {
            if (wallLayers.value == 0)
            {
                int wallLayer = LayerMask.NameToLayer("Wall");
                if (wallLayer >= 0)
                    wallLayers = 1 << wallLayer;
            }

            actorFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = actorLayers,
                useTriggers = false
            };

            wallFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = wallLayers,
                useTriggers = false
            };
        }

        private bool IsValidActorCollider(Collider2D other)
        {
            if (other == null || !other.enabled || other.isTrigger)
                return false;

            if (other == bodyCollider)
                return false;

            if (body != null && other.attachedRigidbody == body)
                return false;

            return true;
        }

        private Vector2 ResolveFallbackDirection(Collider2D other)
        {
            if (body != null && body.linearVelocity.sqrMagnitude > 0.0001f)
                return -body.linearVelocity.normalized;

            return other != null && other.transform.position.x < transform.position.x
                ? Vector2.right
                : Vector2.left;
        }

        private bool IsWallBlocked(Vector2 direction)
        {
            if (wallLayers.value == 0 || wallProbeDistance <= 0f || direction.sqrMagnitude <= 0.0001f)
                return false;

            int hitCount = bodyCollider.Cast(direction.normalized, wallFilter, wallHits, wallProbeDistance);
            for (int i = 0; i < hitCount; i++)
            {
                if (wallHits[i].collider != null)
                    return true;
            }

            return false;
        }

        private static Vector2 ResolveColliderCenter(Collider2D collider)
        {
            return collider != null ? (Vector2)collider.bounds.center : Vector2.zero;
        }
    }
}

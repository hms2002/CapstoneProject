using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 최종 이동 승인 및 적용 책임자.
    /// 규칙:
    /// - 고정/스턴이면 전부 무시
    /// - 넉백 우세 상태면 외압만 반영
    /// - 특수이동 중이면 의도 이동 무시, 특수이동 + 외압 반영
    /// - 기본 상태면 의도 이동 + 외압 반영
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementMotor2D : MonoBehaviour, IMovementStateProvider
    {
        [Header("Physics")]
        [SerializeField] private Rigidbody2D body;
        [Tooltip("빠른 이동에서 벽 관통 가능성을 줄이기 위해 Rigidbody2D 충돌 검출을 Continuous로 강제합니다.")]
        [SerializeField] private bool enforceContinuousCollision = true;

        [Header("Wall Safety")]
        [Tooltip("최종 속도를 적용하기 전에 벽 레이어를 캐스트하여 강한 넉백/강제 이동의 벽 관통을 방지합니다.")]
        [SerializeField] private bool preventWallTunneling = true;
        [Tooltip("비워두면 Awake에서 Wall 레이어를 자동으로 사용합니다.")]
        [SerializeField] private LayerMask wallCollisionLayers;
        [Tooltip("이 속도 이상일 때만 벽 캐스트/끼임 복구를 수행합니다. 일반 이동은 Unity 물리 충돌에 맡깁니다.")]
        [SerializeField, Min(0f)] private float wallSafetyMinSpeed = 8f;
        [SerializeField, Min(0f)] private float wallCastSkinWidth = 0.03f;
        [SerializeField, Min(0f)] private float depenetrationSkinWidth = 0.01f;
        [SerializeField, Range(1, 6)] private int maxDepenetrationIterations = 3;

        [Header("Sources")]
        [Tooltip("IIntentMovementSource2D를 구현한 컴포넌트")]
        [SerializeField] private MonoBehaviour intentSourceBehaviour;

        [Tooltip("IStatProvider를 구현한 컴포넌트 (예: AttributeStatSource)")]
        [SerializeField] private MonoBehaviour statProviderBehaviour;

        [SerializeField] private ExternalMovementController2D externalMovement;
        [SerializeField] private AbilityMotionController2D motionController;

        [Header("Tags - Hard Stop")]
        [Tooltip("이 태그가 있으면 모든 이동 연산을 무시하고 정지한다.")]
        [SerializeField] private GameplayTag freezeAllMovementTag;

        [Header("Tags - Intent")]
        [Tooltip("이 태그가 있으면 의도 이동을 차단한다.")]
        [SerializeField] private GameplayTag intentMoveBlockedTag;

        [Header("Tags - External")]
        [Tooltip("이 태그가 있으면 외압 이동을 차단한다.")]
        [SerializeField] private GameplayTag externalMoveBlockedTag;

        private IIntentMovementSource2D intentSource;
        private IStatProvider statProvider;
        private TagSystem tagSystem;
        private Collider2D[] bodyColliders;
        private readonly RaycastHit2D[] wallCastHits = new RaycastHit2D[16];
        private readonly Collider2D[] wallOverlapHits = new Collider2D[16];
        private ContactFilter2D wallContactFilter;

        private bool hasPendingWarp;
        private Vector2 pendingWarpPosition;
        private bool clearExternalOnWarp = true;
        private bool clearMotionOnWarp = true;

        public Vector2 LastIntentVelocity { get; private set; }
        public Vector2 LastExternalVelocity { get; private set; }
        public Vector2 LastMotionVelocity { get; private set; }
        public Vector2 LastFinalVelocity { get; private set; }

        public bool IsMoving
        {
            get
            {
                if (body != null)
                    return body.linearVelocity.sqrMagnitude > 0.0001f;

                return LastFinalVelocity.sqrMagnitude > 0.0001f;
            }
        }

        private void Awake()
        {
            if (body == null)
                body = GetComponent<Rigidbody2D>();

            CacheBodyColliders();
            ConfigureWallContactFilter();
            EnsureCollisionDetectionMode();

            if (externalMovement == null)
                externalMovement = GetComponent<ExternalMovementController2D>();

            if (motionController == null)
                motionController = GetComponent<AbilityMotionController2D>();

            tagSystem = GetComponent<TagSystem>();

            if (intentSourceBehaviour != null)
                intentSource = intentSourceBehaviour as IIntentMovementSource2D;
            else
                intentSource = GetComponent<IIntentMovementSource2D>();

            if (statProviderBehaviour != null)
                statProvider = statProviderBehaviour as IStatProvider;
            else
                statProvider = GetComponent<IStatProvider>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (intentSource == null)
                Debug.LogWarning($"[MovementMotor2D] {name}: IIntentMovementSource2D를 찾지 못했습니다.");

            if (statProvider == null)
                Debug.LogWarning($"[MovementMotor2D] {name}: IStatProvider를 찾지 못했습니다. (예: AttributeStatSource 필요)");
#endif
        }

        /// <summary>
        /// 책임 :
        /// - MovementMotor2D가 최종 이동을 담당하는 Rigidbody2D의 충돌 검출 모드를 안전한 기본값으로 보정한다.
        /// - 프리팹 세팅 누락으로 고속 이동체가 Discrete 충돌 검출에 머무는 상황을 줄인다.
        /// </summary>
        private void EnsureCollisionDetectionMode()
        {
            if (!enforceContinuousCollision || body == null)
                return;

            if (body.collisionDetectionMode != CollisionDetectionMode2D.Continuous)
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        /// <summary>
        /// 책임 :
        /// - MovementMotor2D가 실제 물리 이동을 대표하는 비트리거 콜라이더 목록을 캐시한다.
        /// - 최종 속도 벽 캐스트/끼임 복구가 hitbox 트리거가 아닌 물리 box 기준으로 동작하게 한다.
        /// </summary>
        private void CacheBodyColliders()
        {
            Collider2D[] candidates = GetComponentsInChildren<Collider2D>(true);
            int bodyCount = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                Collider2D candidate = candidates[i];
                if (IsUsableBodyCollider(candidate))
                    bodyCount++;
            }

            bodyColliders = new Collider2D[bodyCount];
            int writeIndex = 0;
            for (int i = 0; i < candidates.Length; i++)
            {
                Collider2D candidate = candidates[i];
                if (!IsUsableBodyCollider(candidate))
                    continue;

                bodyColliders[writeIndex] = candidate;
                writeIndex++;
            }
        }

        /// <summary>
        /// 책임 :
        /// - MovementMotor2D의 벽 안전장치가 사용할 ContactFilter2D를 구성한다.
        /// - 인스펙터 값이 비어 있으면 프로젝트 표준 Wall 레이어를 자동으로 사용한다.
        /// </summary>
        private void ConfigureWallContactFilter()
        {
            if (wallCollisionLayers.value == 0)
            {
                int wallLayer = LayerMask.NameToLayer("Wall");
                if (wallLayer >= 0)
                    wallCollisionLayers = 1 << wallLayer;
            }

            wallContactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = wallCollisionLayers,
                useTriggers = false
            };
        }

        private void FixedUpdate()
        {
            if (body == null)
                return;

            HandlePendingWarp();

            float dt = Time.fixedDeltaTime;

            if (externalMovement != null)
                externalMovement.Tick(dt);

            // 1) 하드 스톱
            if (IsHardStopped())
            {
                if (motionController != null && motionController.HasActiveMotion)
                    motionController.CancelMotion();

                ApplyVelocities(Vector2.zero, Vector2.zero, Vector2.zero);
                return;
            }

            // 2) 외압 계산
            Vector2 externalVelocity = ResolveExternalVelocity();
            bool hasKnockbackDominance = externalMovement != null && externalMovement.HasKnockbackDominance;

            // 3) 넉백 우세 상태: 외압만 반영
            if (hasKnockbackDominance)
            {
                if (motionController != null && motionController.HasActiveMotion)
                    motionController.CancelMotion();

                ApplyVelocities(Vector2.zero, externalVelocity, Vector2.zero);
                return;
            }

            // 4) 특수이동 계산
            Vector2 motionVelocity = ResolveMotionVelocity(dt);
            bool hasMotion = motionController != null && motionController.HasActiveMotion;

            // 5) 특수이동 중: 특수이동 + 외압
            if (hasMotion)
            {
                ApplyVelocities(Vector2.zero, externalVelocity, motionVelocity);
                return;
            }

            // 6) 기본: 의도 + 외압
            Vector2 intentVelocity = ResolveIntentVelocity();
            ApplyVelocities(intentVelocity, externalVelocity, Vector2.zero);
        }


        public void WarpTo(
            Vector2 worldPosition,
            bool clearExternalMovement = true,
            bool clearMotion = true)
        {
            hasPendingWarp = true;
            pendingWarpPosition = worldPosition;
            clearExternalOnWarp = clearExternalMovement;
            clearMotionOnWarp = clearMotion;
        }

        public void StopAllMotion(bool clearExternal = true, bool clearMotion = true)
        {
            EnsureMotionController();

            if (body != null)
                body.linearVelocity = Vector2.zero;

            LastIntentVelocity = Vector2.zero;
            LastExternalVelocity = Vector2.zero;
            LastMotionVelocity = Vector2.zero;
            LastFinalVelocity = Vector2.zero;

            if (clearExternal && externalMovement != null)
                externalMovement.ClearAll();

            if (clearMotion && motionController != null)
                motionController.CancelMotion();
        }

        private Vector2 ResolveIntentVelocity()
        {
            if (!CanUseIntentMovement())
                return Vector2.zero;

            if (intentSource == null || statProvider == null)
                return Vector2.zero;

            IntentMovementData intent = intentSource.GetIntent();


            float moveSpeed = Mathf.Max(0f, statProvider.Get(StatId.MoveSpeedFinal));
            if (moveSpeed <= 0f)
                return Vector2.zero;

            Vector2 direction = intent.Direction;
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            float finalSpeed = moveSpeed * Mathf.Max(0f, intent.SpeedScale);
            return direction * finalSpeed;
        }

        private Vector2 ResolveExternalVelocity()
        {
            if (!CanUseExternalMovement())
                return Vector2.zero;

            if (externalMovement == null)
                return Vector2.zero;

            return externalMovement.GetCurrentExternalVelocity();
        }

        private Vector2 ResolveMotionVelocity(float dt)
        {
            EnsureMotionController();

            if (motionController == null || !motionController.HasActiveMotion)
                return Vector2.zero;

            return motionController.TickAndGetMotionVelocity(dt);
        }

        private void EnsureMotionController()
        {
            if (motionController != null)
                return;

            motionController = GetComponent<AbilityMotionController2D>();
        }

        private bool IsHardStopped()
        {
            if (tagSystem == null || freezeAllMovementTag == null)
                return false;

            return tagSystem.HasTag(freezeAllMovementTag);
        }

        private bool CanUseIntentMovement()
        {
            if (tagSystem == null || intentMoveBlockedTag == null)
                return true;

            return !tagSystem.HasTag(intentMoveBlockedTag);
        }

        private bool CanUseExternalMovement()
        {
            if (tagSystem == null || externalMoveBlockedTag == null)
                return true;

            return !tagSystem.HasTag(externalMoveBlockedTag);
        }

        private void ApplyVelocities(Vector2 intentVelocity, Vector2 externalVelocity, Vector2 motionVelocity)
        {
            intentVelocity = SanitizeVelocity(intentVelocity, nameof(intentVelocity));
            externalVelocity = SanitizeVelocity(externalVelocity, nameof(externalVelocity));
            motionVelocity = SanitizeVelocity(motionVelocity, nameof(motionVelocity));

            LastIntentVelocity = intentVelocity;
            LastExternalVelocity = externalVelocity;
            LastMotionVelocity = motionVelocity;
            LastFinalVelocity = SanitizeVelocity(intentVelocity + externalVelocity + motionVelocity, nameof(LastFinalVelocity));
            LastFinalVelocity = ResolveWallSafeVelocity(LastFinalVelocity);
            if (body != null)
                body.linearVelocity = LastFinalVelocity;
        }

        /// <summary>
        /// 책임 :
        /// - 최종 이동 속도가 이번 FixedUpdate에서 벽 콜라이더를 관통하지 않도록 이동량을 제한한다.
        /// - 이미 벽과 겹친 상태라면 짧은 depenetration을 먼저 수행해 벽 내부 고착을 완화한다.
        /// </summary>
        private Vector2 ResolveWallSafeVelocity(Vector2 velocity)
        {
            if (!preventWallTunneling || body == null || wallCollisionLayers.value == 0)
                return velocity;

            float speed = velocity.magnitude;
            if (speed <= 0.0001f)
                return velocity;

            if (speed < wallSafetyMinSpeed)
                return velocity;

            if (bodyColliders == null || bodyColliders.Length == 0)
                CacheBodyColliders();

            ResolveWallPenetration();

            float dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            float moveDistance = speed * dt;
            if (moveDistance <= 0.0001f)
                return velocity;

            Vector2 direction = velocity / speed;
            float allowedDistance = moveDistance;

            for (int i = 0; i < bodyColliders.Length; i++)
            {
                Collider2D bodyCollider = bodyColliders[i];
                if (!IsUsableBodyCollider(bodyCollider))
                    continue;

                int hitCount = bodyCollider.Cast(direction, wallContactFilter, wallCastHits, moveDistance + wallCastSkinWidth);
                for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                {
                    RaycastHit2D hit = wallCastHits[hitIndex];
                    if (hit.collider == null || hit.collider.attachedRigidbody == body)
                        continue;

                    allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - wallCastSkinWidth));
                }
            }

            if (allowedDistance >= moveDistance)
                return velocity;

            float safeSpeed = allowedDistance / dt;
            return direction * safeSpeed;
        }

        /// <summary>
        /// 책임 :
        /// - 물리 콜라이더가 벽 콜라이더와 이미 겹친 상태를 감지하고 최소 이동 벡터에 가깝게 밀어낸다.
        /// - 강한 넉백이 타일맵 벽 내부에 플레이어를 남기는 상황을 다음 프레임부터 복구한다.
        /// </summary>
        private void ResolveWallPenetration()
        {
            for (int iteration = 0; iteration < maxDepenetrationIterations; iteration++)
            {
                bool resolvedAny = false;

                for (int i = 0; i < bodyColliders.Length; i++)
                {
                    Collider2D bodyCollider = bodyColliders[i];
                    if (!IsUsableBodyCollider(bodyCollider))
                        continue;

                    int overlapCount = bodyCollider.Overlap(wallContactFilter, wallOverlapHits);
                    for (int overlapIndex = 0; overlapIndex < overlapCount; overlapIndex++)
                    {
                        Collider2D wallCollider = wallOverlapHits[overlapIndex];
                        if (wallCollider == null || wallCollider.attachedRigidbody == body)
                            continue;

                        ColliderDistance2D distance = bodyCollider.Distance(wallCollider);
                        if (!distance.isOverlapped)
                            continue;

                        Vector2 correction = distance.normal * (distance.distance - depenetrationSkinWidth);
                        if (!float.IsFinite(correction.x) || !float.IsFinite(correction.y) || correction.sqrMagnitude <= 0.000001f)
                            continue;

                        body.position += correction;
                        resolvedAny = true;
                    }
                }

                if (!resolvedAny)
                    return;
            }
        }

        private static bool IsUsableBodyCollider(Collider2D bodyCollider)
        {
            return bodyCollider != null
                && bodyCollider.enabled
                && bodyCollider.gameObject.activeInHierarchy
                && !bodyCollider.isTrigger;
        }

        /// <summary>
        /// 책임 :
        /// - MovementMotor2D가 렌더/물리 계층으로 NaN, Infinity 속도를 넘기지 않도록 마지막 방어선을 제공한다.
        /// - 비정상 값이 감지되면 경고를 남기고 안전한 0 벡터로 치환한다.
        /// </summary>
        private Vector2 SanitizeVelocity(Vector2 velocity, string sourceName)
        {
            if (float.IsFinite(velocity.x) && float.IsFinite(velocity.y))
                return velocity;

            Debug.LogWarning($"[MovementMotor2D] {name}: {sourceName} 에 비정상 속도 값이 들어와 Vector2.zero 로 치환했습니다. value={velocity}");
            return Vector2.zero;
        }

        private void HandlePendingWarp()
        {
            if (!hasPendingWarp)
                return;

            hasPendingWarp = false;

            body.position = pendingWarpPosition;
            body.linearVelocity = Vector2.zero;

            LastIntentVelocity = Vector2.zero;
            LastExternalVelocity = Vector2.zero;
            LastMotionVelocity = Vector2.zero;
            LastFinalVelocity = Vector2.zero;

            if (clearExternalOnWarp && externalMovement != null)
                externalMovement.ClearAll();

            if (clearMotionOnWarp && motionController != null)
                motionController.CancelMotion();
        }
    }
}

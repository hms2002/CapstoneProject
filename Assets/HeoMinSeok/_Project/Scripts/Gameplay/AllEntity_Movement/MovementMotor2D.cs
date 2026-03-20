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
        [SerializeField] private GameplayTag blockIntentMoveTag;

        [Header("Tags - External")]
        [Tooltip("이 태그가 있으면 외압 이동을 차단한다.")]
        [SerializeField] private GameplayTag blockExternalMoveTag;

        private IIntentMovementSource2D intentSource;
        private IStatProvider statProvider;
        private TagSystem tagSystem;

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
            if (motionController == null || !motionController.HasActiveMotion)
                return Vector2.zero;

            return motionController.TickAndGetMotionVelocity(dt);
        }

        private bool IsHardStopped()
        {
            if (tagSystem == null || freezeAllMovementTag == null)
                return false;

            return tagSystem.HasTag(freezeAllMovementTag);
        }

        private bool CanUseIntentMovement()
        {
            if (tagSystem == null || blockIntentMoveTag == null)
                return true;

            return !tagSystem.HasTag(blockIntentMoveTag);
        }

        private bool CanUseExternalMovement()
        {
            if (tagSystem == null || blockExternalMoveTag == null)
                return true;

            return !tagSystem.HasTag(blockExternalMoveTag);
        }

        private void ApplyVelocities(Vector2 intentVelocity, Vector2 externalVelocity, Vector2 motionVelocity)
        {
            LastIntentVelocity = intentVelocity;
            LastExternalVelocity = externalVelocity;
            LastMotionVelocity = motionVelocity;
            LastFinalVelocity = intentVelocity + externalVelocity + motionVelocity;

            if (body != null)
                body.linearVelocity = LastFinalVelocity;
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
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

/// <summary>
/// 책임: 튜토리얼 전용 포탈 상호작용, 플레이어 런타임 상태 보존, 포탈 흡입 연출, 튜토리얼 씬 전환 요청을 담당한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TutorialScenePortal : InteractableBase
{
    [Header("Target")]
    [SerializeField] private string targetSceneName = "DarkLord_Tutorial";

    [Header("Interact")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "Move";

    [Header("Runtime State")]
    [SerializeField, HideInInspector] private bool preservePlayerRuntimeState = true;
    [SerializeField, HideInInspector] private bool prepareTransitionContext = true;
    [SerializeField] private bool resetPlayerRuntimeStateOnTravel;
    [SerializeField] private bool skipTransitionContextPreparation;

    [Header("Entrance Presentation")]
    [SerializeField] private bool playEntrancePresentation = true;
    [SerializeField, Min(0f)] private float entranceDuration = 0.55f;
    [SerializeField] private float entranceRotationDegrees = 720f;
    [SerializeField] private Transform entranceCenter;
    [SerializeField] private Vector3 entranceCenterWorldOffset;

    [Header("Optional Visual")]
    [SerializeField] private GameObject highlightTarget;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private bool isTransitioning;
    private bool acceptedTravel;
    private bool hasActiveEntranceSnapshot;
    private Coroutine entranceRoutine;
    private TutorialPortalEntranceSnapshot activeEntranceSnapshot;
    private PlayerCinematicProtection lockedPlayerProtection;
    private GameFlowInputBlocker entranceInputBlocker;
    private MaterialPropertyBlock propertyBlock;
    private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");

    private void Awake()
    {
        NormalizeLegacyRuntimeStateFlags();
        propertyBlock = new MaterialPropertyBlock();
        OnUnHighlight();
    }

    private void Reset()
    {
        Collider2D portalCollider = GetComponent<Collider2D>();
        if (portalCollider != null)
            portalCollider.isTrigger = true;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        NormalizeLegacyRuntimeStateFlags();
        resetPlayerRuntimeStateOnTravel = false;
        skipTransitionContextPreparation = false;
    }

    private void OnValidate()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        NormalizeLegacyRuntimeStateFlags();
    }

    private void OnDisable()
    {
        CleanupEntrancePresentation(restoreSnapshot: true);
    }

    public override void OnPlayerLeave()
    {
        OnUnHighlight();
    }

    public override void OnHighlight()
    {
        SetOutline(true);

        if (highlightTarget != null)
            highlightTarget.SetActive(true);
    }

    public override void OnUnHighlight()
    {
        SetOutline(false);

        if (highlightTarget != null)
            highlightTarget.SetActive(false);
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return !isTransitioning &&
               player != null &&
               player.CurrentState == InteractState.Idle &&
               !string.IsNullOrWhiteSpace(targetSceneName);
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        isTransitioning = true;
        OnUnHighlight();
        player.SetInteractState(InteractState.None);

        PreparedTutorialTravel preparedTravel = PrepareTutorialTravel(player);
        if (ShouldPlayEntrancePresentation(player))
        {
            entranceRoutine = StartCoroutine(PlayEntranceAndTravelRoutine(player, preparedTravel));
            return;
        }

        if (!TryLoadTargetScene())
        {
            preparedTravel.RestorePendingRuntimeState();
            isTransitioning = false;
            player.SetInteractState(InteractState.Idle);
        }
    }

    public override InteractState GetInteractType() => InteractState.Idle;
    public override string GetInteractDescription() => interactPromptText;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    private PreparedTutorialTravel PrepareTutorialTravel(IPlayerInteractor player)
    {
        PlayerRuntimeState previousPlayerState = RunSessionStore.PeekPendingPlayerState();
        SceneTransitionContext previousTransitionContext = RunSessionStore.PeekPendingTransition();
        bool capturedPlayerState = TryCapturePlayerRuntimeState(player);
        bool preparedTransitionContext = TryPrepareTransitionContext();

        return new PreparedTutorialTravel(
            previousPlayerState,
            previousTransitionContext,
            capturedPlayerState,
            preparedTransitionContext);
    }

    private bool ShouldPlayEntrancePresentation(IPlayerInteractor player)
    {
        return playEntrancePresentation &&
               entranceDuration > 0f &&
               player?.Transform != null;
    }

    private IEnumerator PlayEntranceAndTravelRoutine(
        IPlayerInteractor player,
        PreparedTutorialTravel preparedTravel)
    {
        TutorialPortalEntranceSnapshot snapshot = TutorialPortalEntranceSnapshot.Capture(player);
        activeEntranceSnapshot = snapshot;
        hasActiveEntranceSnapshot = snapshot.IsValid;

        AcquireInputBlocker();
        AcquirePlayerCinematicProtection(snapshot.PlayerTransform);

        bool travelStarted = false;
        try
        {
            if (snapshot.IsValid)
            {
                snapshot.StopPhysicsForPresentation();
                yield return PlayEntrancePresentation(snapshot);
                snapshot.RestorePhysics();
            }

            ReleasePlayerCinematicProtection();
            ReleaseInputBlocker();

            travelStarted = TryLoadTargetScene();
            if (!travelStarted)
            {
                preparedTravel.RestorePendingRuntimeState();
                RestoreFailedEntranceTravel(player, snapshot);
                yield break;
            }

            acceptedTravel = true;
            yield return WaitForAcceptedTransitionToFinish();

            RestoreAcceptedEntranceTravel(snapshot);
        }
        finally
        {
            ReleasePlayerCinematicProtection();
            ReleaseInputBlocker();
            entranceRoutine = null;

            if (!travelStarted)
                isTransitioning = false;
        }
    }

    private IEnumerator PlayEntrancePresentation(TutorialPortalEntranceSnapshot snapshot)
    {
        if (!snapshot.IsValid || snapshot.PlayerTransform == null)
            yield break;

        Vector3 targetPosition = ResolveEntranceTargetPosition(snapshot.PlayerTransform);
        Vector3 targetRotation = snapshot.LocalRotation.eulerAngles +
                                 new Vector3(0f, 0f, entranceRotationDegrees);

        yield return PlayEntranceTransformRoutine(snapshot, targetPosition, targetRotation);
    }

    private Vector3 ResolveEntranceTargetPosition(Transform playerTransform)
    {
        Vector3 targetPosition = entranceCenter != null
            ? entranceCenter.position
            : transform.position;

        targetPosition += entranceCenterWorldOffset;
        if (playerTransform != null)
            targetPosition.z = playerTransform.position.z;

        return targetPosition;
    }

    private IEnumerator WaitForAcceptedTransitionToFinish()
    {
        ISceneTransitionHandle transitionCoordinator = SceneTransitionPlayback.Instance;
        while (transitionCoordinator != null && transitionCoordinator.IsTransitionActive)
        {
            yield return null;
            transitionCoordinator = SceneTransitionPlayback.Instance;
        }
    }

    private void RestoreFailedEntranceTravel(IPlayerInteractor player, TutorialPortalEntranceSnapshot snapshot)
    {
        acceptedTravel = false;
        isTransitioning = false;

        if (snapshot.IsValid)
            snapshot.RestoreAll();

        hasActiveEntranceSnapshot = false;
        player?.SetInteractState(InteractState.Idle);
    }

    private void RestoreAcceptedEntranceTravel(TutorialPortalEntranceSnapshot snapshot)
    {
        acceptedTravel = false;
        isTransitioning = false;

        if (snapshot.IsValid)
            snapshot.RestorePresentationOnly();

        hasActiveEntranceSnapshot = false;
    }

    private bool TryLoadTargetScene()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[TutorialScenePortal] Target scene name is empty.", this);
            return false;
        }

        ISceneTransitionHandle transitionCoordinator = SceneTransitionPlayback.EnsureInstance();
        if (transitionCoordinator != null)
        {
            if (transitionCoordinator.TryLoadScene(targetSceneName))
                return true;

            if (transitionCoordinator.IsTransitionActive)
            {
                Debug.LogWarning(
                    $"[TutorialScenePortal] Scene transition is already active. target={targetSceneName}",
                    this);
                return false;
            }
        }

        try
        {
            SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                $"[TutorialScenePortal] Failed to load scene '{targetSceneName}': {ex.Message}",
                this);
            return false;
        }
    }

    private void AcquireInputBlocker()
    {
        entranceInputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        entranceInputBlocker?.Acquire();
    }

    private void ReleaseInputBlocker()
    {
        entranceInputBlocker?.Release();
        entranceInputBlocker = null;
    }

    private void AcquirePlayerCinematicProtection(Transform playerTransform)
    {
        if (playerTransform == null || lockedPlayerProtection != null)
            return;

        lockedPlayerProtection = playerTransform.GetComponent<PlayerCinematicProtection>();
        if (lockedPlayerProtection == null)
            lockedPlayerProtection = playerTransform.gameObject.AddComponent<PlayerCinematicProtection>();

        lockedPlayerProtection.Acquire(this);
    }

    private void ReleasePlayerCinematicProtection()
    {
        if (lockedPlayerProtection != null)
            lockedPlayerProtection.Release(this);

        lockedPlayerProtection = null;
    }

    private void CleanupEntrancePresentation(bool restoreSnapshot)
    {
        if (entranceRoutine != null)
        {
            StopCoroutine(entranceRoutine);
            entranceRoutine = null;
        }

        if (restoreSnapshot && hasActiveEntranceSnapshot)
        {
            if (acceptedTravel)
                activeEntranceSnapshot.RestorePresentationOnly();
            else
                activeEntranceSnapshot.RestoreAll();
        }

        hasActiveEntranceSnapshot = false;
        acceptedTravel = false;
        isTransitioning = false;

        ReleasePlayerCinematicProtection();
        ReleaseInputBlocker();
    }

    private bool TryCapturePlayerRuntimeState(IPlayerInteractor player)
    {
        if (resetPlayerRuntimeStateOnTravel)
            return false;

        if (!RunSessionStore.IsAvailable)
        {
            Debug.LogWarning("[TutorialScenePortal] RunSessionStore backend is missing. Player runtime state was not preserved.", this);
            return false;
        }

        GameObject playerObject = ResolvePlayerObject(player);
        if (playerObject == null)
        {
            Debug.LogWarning("[TutorialScenePortal] Player object was not found. Player runtime state was not preserved.", this);
            return false;
        }

        CleanupPlayerBeforeCapture(playerObject);

        PlayerRuntimeCaptureBridge captureBridge = playerObject.GetComponent<PlayerRuntimeCaptureBridge>();
        if (captureBridge == null)
        {
            Debug.LogWarning("[TutorialScenePortal] PlayerRuntimeCaptureBridge is missing. Player runtime state was not preserved.", playerObject);
            return false;
        }

        RunSessionStore.PreparePlayerState(captureBridge.CaptureRuntimeState());
        return true;
    }

    private bool TryPrepareTransitionContext()
    {
        if (skipTransitionContextPreparation || !RunSessionStore.IsAvailable || string.IsNullOrWhiteSpace(targetSceneName))
            return false;

        var context = new SceneTransitionContext
        {
            fromScene = gameObject.scene.IsValid()
                ? gameObject.scene.name
                : SceneManager.GetActiveScene().name,
            toScene = targetSceneName,
            exitPointId = gameObject.name,
            entryPointId = "Default",
            transitionType = TransitionType.None
        };

        RunSessionStore.PrepareTransition(context);
        return true;
    }

    private static void RestorePendingRuntimeState(
        PlayerRuntimeState previousPlayerState,
        SceneTransitionContext previousTransitionContext,
        bool capturedPlayerState,
        bool preparedTransitionContext)
    {
        if (!RunSessionStore.IsAvailable)
            return;

        if (capturedPlayerState)
            RunSessionStore.PreparePlayerState(previousPlayerState);

        if (preparedTransitionContext)
            RunSessionStore.PrepareTransition(previousTransitionContext);
    }

    private static GameObject ResolvePlayerObject(IPlayerInteractor player)
    {
        if (player?.Transform != null)
            return player.Transform.gameObject;

        Transform registeredPlayer = PlayerRuntimeRegistry.GetPlayerTransform();
        if (registeredPlayer != null)
            return registeredPlayer.gameObject;

        return GameObject.FindGameObjectWithTag("Player");
    }

    private static void CleanupPlayerBeforeCapture(GameObject playerObject)
    {
        if (playerObject == null)
            return;

        AbilitySystem abilitySystem = playerObject.GetComponent<AbilitySystem>();
        abilitySystem?.CancelAllForSceneTransition();
    }

    private void NormalizeLegacyRuntimeStateFlags()
    {
        if (!preservePlayerRuntimeState)
            preservePlayerRuntimeState = true;

        if (!prepareTransitionContext)
            prepareTransitionContext = true;
    }

    private void SetOutline(bool enabled)
    {
        if (spriteRenderer == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(OutlineEnabledId, enabled ? 1f : 0f);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>
    /// 책임: 튜토리얼 포탈 전환 실패 시 이전 pending 플레이어/씬 전환 상태를 되돌리기 위한 스냅샷을 보관한다.
    /// </summary>
    private readonly struct PreparedTutorialTravel
    {
        private readonly PlayerRuntimeState previousPlayerState;
        private readonly SceneTransitionContext previousTransitionContext;
        private readonly bool capturedPlayerState;
        private readonly bool preparedTransitionContext;

        public PreparedTutorialTravel(
            PlayerRuntimeState previousPlayerState,
            SceneTransitionContext previousTransitionContext,
            bool capturedPlayerState,
            bool preparedTransitionContext)
        {
            this.previousPlayerState = previousPlayerState;
            this.previousTransitionContext = previousTransitionContext;
            this.capturedPlayerState = capturedPlayerState;
            this.preparedTransitionContext = preparedTransitionContext;
        }

        public void RestorePendingRuntimeState()
        {
            TutorialScenePortal.RestorePendingRuntimeState(
                previousPlayerState,
                previousTransitionContext,
                capturedPlayerState,
                preparedTransitionContext);
        }
    }

    /// <summary>
    /// 책임: 튜토리얼 포탈 흡입 연출 동안 변경한 플레이어 위치, 회전, 크기, 물리 상태를 원복하기 위한 값을 보관한다.
    /// </summary>
    private readonly struct TutorialPortalEntranceSnapshot
    {
        public readonly Transform PlayerTransform;
        public readonly Rigidbody2D Body;
        public readonly Vector3 Position;
        public readonly Vector3 LocalScale;
        public readonly Quaternion LocalRotation;
        public readonly RigidbodyType2D BodyType;
        public readonly Vector2 LinearVelocity;
        public readonly float AngularVelocity;

        private TutorialPortalEntranceSnapshot(
            Transform playerTransform,
            Rigidbody2D body,
            Vector3 position,
            Vector3 localScale,
            Quaternion localRotation,
            RigidbodyType2D bodyType,
            Vector2 linearVelocity,
            float angularVelocity)
        {
            PlayerTransform = playerTransform;
            Body = body;
            Position = position;
            LocalScale = localScale;
            LocalRotation = localRotation;
            BodyType = bodyType;
            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
        }

        public bool IsValid => PlayerTransform != null;

        public static TutorialPortalEntranceSnapshot Capture(IPlayerInteractor player)
        {
            Transform playerTransform = player?.Transform;
            if (playerTransform == null)
            {
                Transform registeredPlayer = PlayerRuntimeRegistry.GetPlayerTransform();
                if (registeredPlayer != null)
                    playerTransform = registeredPlayer;
            }

            if (playerTransform == null)
                return default;

            Rigidbody2D body = playerTransform.GetComponent<Rigidbody2D>();
            RigidbodyType2D bodyType = body != null ? body.bodyType : RigidbodyType2D.Dynamic;
            Vector2 linearVelocity = body != null ? body.linearVelocity : Vector2.zero;
            float angularVelocity = body != null ? body.angularVelocity : 0f;

            return new TutorialPortalEntranceSnapshot(
                playerTransform,
                body,
                playerTransform.position,
                playerTransform.localScale,
                playerTransform.localRotation,
                bodyType,
                linearVelocity,
                angularVelocity);
        }

        public void StopPhysicsForPresentation()
        {
            if (Body == null)
                return;

            Body.linearVelocity = Vector2.zero;
            Body.angularVelocity = 0f;
            Body.bodyType = RigidbodyType2D.Kinematic;
        }

        public void RestorePhysics()
        {
            if (Body == null)
                return;

            Body.bodyType = BodyType;
            Body.linearVelocity = LinearVelocity;
            Body.angularVelocity = AngularVelocity;
        }

        public void RestorePresentationOnly()
        {
            if (PlayerTransform != null)
            {
                PlayerTransform.localScale = LocalScale;
                PlayerTransform.localRotation = LocalRotation;
            }

            RestorePhysics();
        }

        public void RestoreAll()
        {
            if (PlayerTransform != null)
            {
                PlayerTransform.position = Position;
                PlayerTransform.localScale = LocalScale;
                PlayerTransform.localRotation = LocalRotation;
            }

            RestorePhysics();
        }
    }

    private IEnumerator PlayEntranceTransformRoutine(
        TutorialPortalEntranceSnapshot snapshot,
        Vector3 targetPosition,
        Vector3 targetRotation)
    {
        Transform playerTransform = snapshot.PlayerTransform;
        if (playerTransform == null)
            yield break;

        Vector3 startPosition = playerTransform.position;
        Vector3 startScale = playerTransform.localScale;
        Vector3 startRotation = snapshot.LocalRotation.eulerAngles;
        float elapsed = 0f;

        while (elapsed < entranceDuration && playerTransform != null)
        {
            elapsed += Time.deltaTime;
            float t = entranceDuration > 0f ? elapsed / entranceDuration : 1f;
            ApplyEntranceTransformFrame(
                playerTransform,
                startPosition,
                targetPosition,
                startScale,
                startRotation,
                targetRotation,
                t);
            yield return null;
        }

        if (playerTransform != null)
        {
            playerTransform.position = targetPosition;
            playerTransform.localScale = Vector3.zero;
            playerTransform.localRotation = Quaternion.Euler(targetRotation);
        }
    }

    private static void ApplyEntranceTransformFrame(
        Transform playerTransform,
        Vector3 startPosition,
        Vector3 targetPosition,
        Vector3 startScale,
        Vector3 startRotation,
        Vector3 targetRotation,
        float t)
    {
        playerTransform.position = Vector3.LerpUnclamped(startPosition, targetPosition, EaseOutQuart(t));
        playerTransform.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, EaseInBack(t));
        playerTransform.localRotation = Quaternion.Euler(Vector3.LerpUnclamped(startRotation, targetRotation, EaseInCubic(t)));
    }

    private static float EaseOutQuart(float t)
    {
        t = Mathf.Clamp01(t);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse * inverse;
    }

    private static float EaseInCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t;
    }

    private static float EaseInBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float back = 1.70158f;
        return (back + 1f) * t * t * t - back * t * t;
    }
}

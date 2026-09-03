using System;
using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// 책임 : 플레이어 상호작용을 받아 포탈 이동을 요청하는 진입점이다.
/// 실제 경로 해석은 현재 런 계획 backend를 감싼 RunRoutePlayback에 위임한다.
/// </summary>
public sealed class ScenePortal : InteractableBase
{
    private static readonly SoundRef EnterSound = SoundRef.FromKey("sound_scenePortal_Enter");

    [SerializeField, HideInInspector] private string portalId;

    [Header("Transition Semantic")]
    [SerializeField] private TransitionType transitionType = TransitionType.None;

    [Header("Start Run Route Catalog")]
    [SerializeField] private RunRouteCatalogSO startRunRouteCatalog;

    [Header("Interact")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "이동하기";

    [Header("Optional Visual")]
    [SerializeField] private GameObject highlightTarget;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Entrance Presentation")]
    [SerializeField] private bool playEntrancePresentation = true;
    [SerializeField, Min(0f)] private float entranceDuration = 0.55f;
    [SerializeField] private float entranceRotationDegrees = 720f;
    [SerializeField] private Transform entranceCenter;
    [SerializeField] private Vector3 entranceCenterWorldOffset;

    [Header("Cleanup Before Capture")]
    [SerializeField] private List<GameplayTagSet> sceneTravelCleanupTagSets = new();

    private bool isTransitioning;
    private bool acceptedTravel;
    private bool hasActiveEntranceSnapshot;
    private Coroutine entranceRoutine;
    private PortalEntranceSnapshot activeEntranceSnapshot;
    private PlayerCinematicProtection lockedPlayerProtection;
    private GameFlowInputBlocker entranceInputBlocker;
    private object oneShotDestinationOverrideOwner;
    private string oneShotDestinationOverrideSceneName;
    private IScenePortalAccessRule[] accessRules = Array.Empty<IScenePortalAccessRule>();

    private MaterialPropertyBlock propBlock;
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");

#if UNITY_EDITOR
    private float nextEditorDiagnosticLogTime;
    private string lastEditorDiagnosticMessage;
#endif

    public string PortalId => portalId;
    public TransitionType PortalTransitionType => transitionType;
    public RunRouteCatalogSO StartRunRouteCatalog => startRunRouteCatalog;
    public IReadOnlyList<GameplayTagSet> SceneTravelCleanupTagSets => sceneTravelCleanupTagSets;
    public bool HasOneShotDestinationOverride => !string.IsNullOrWhiteSpace(oneShotDestinationOverrideSceneName);

    private void Awake()
    {
        EnsurePortalId();
        RefreshAccessRules();

        propBlock = new MaterialPropertyBlock();
        OnUnHighlight();
    }

    private void OnEnable()
    {
        RefreshAccessRules();
        EnsurePendingStartRunPlan();
    }

    private void OnDisable()
    {
        CleanupEntrancePresentation(restoreSnapshot: true);
    }

    private void Reset()
    {
        EnsurePortalId();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnValidate()
    {
        EnsurePortalId();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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
        bool hasPlayer = player != null;
        bool isIdle = hasPlayer && player.CurrentState == InteractState.Idle;
        bool transitionIdle = !IsSceneTransitionActive();
        bool canResolve = HasOneShotDestinationOverride ||
                          RunRoutePlayback.CanResolveRoute(this);

        bool canInteract =
            !isTransitioning &&
            transitionIdle &&
            hasPlayer &&
            isIdle &&
            canResolve;

#if UNITY_EDITOR
        EmitEditorDiagnosticIfBlocked(player, hasPlayer, isIdle, canResolve, canInteract);
#endif

        return canInteract;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        if (!CanPassAccessRules(player))
            return;

        isTransitioning = true;
        OnUnHighlight();

        player.SetInteractState(InteractState.None);

        if (ShouldPlayEntrancePresentation(player))
        {
            entranceRoutine = StartCoroutine(PlayEntranceAndTravelRoutine(player));
            return;
        }

        if (!TryStartTravelSafely())
        {
            isTransitioning = false;
            player.SetInteractState(InteractState.Idle);
        }
        else
        {
            PlayEnterSound();
            acceptedTravel = true;
        }
    }

    public override InteractState GetInteractType() => InteractState.Idle;
    public override string GetInteractDescription() => interactPromptText;
    public override Transform GetPromptAnchor() => promptAnchor != null ? promptAnchor : transform;

    /// <summary>
    /// 책임:
    /// 시연/디버그 치트가 기존 포탈 상호작용 흐름을 유지한 채 다음 1회 목적지만 바꿀 수 있게 한다.
    /// </summary>
    public bool SetOneShotDestinationOverride(string sceneName, object owner)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || owner == null)
            return false;

        oneShotDestinationOverrideSceneName = sceneName.Trim();
        oneShotDestinationOverrideOwner = owner;
        return true;
    }

    public bool TryGetOneShotDestinationOverride(out string sceneName)
    {
        sceneName = oneShotDestinationOverrideSceneName;
        return !string.IsNullOrWhiteSpace(sceneName);
    }

    public void ClearOneShotDestinationOverride(object owner)
    {
        if (owner != null && oneShotDestinationOverrideOwner != null && !ReferenceEquals(oneShotDestinationOverrideOwner, owner))
            return;

        oneShotDestinationOverrideSceneName = null;
        oneShotDestinationOverrideOwner = null;
    }

    private void SetOutline(bool enabled)
    {
        if (spriteRenderer == null)
            return;

        spriteRenderer.GetPropertyBlock(propBlock);
        propBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
        spriteRenderer.SetPropertyBlock(propBlock);
    }

    private bool ShouldPlayEntrancePresentation(IPlayerInteractor player)
    {
        return playEntrancePresentation &&
               entranceDuration > 0f &&
               player?.Transform != null;
    }

    private static bool IsSceneTransitionActive()
    {
        return SceneTransitionPlayback.IsTransitionActive;
    }

    private IEnumerator PlayEntranceAndTravelRoutine(IPlayerInteractor player)
    {
        PortalEntranceSnapshot snapshot = PortalEntranceSnapshot.Capture(player);
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

            travelStarted = TryStartTravelSafely();
            if (!travelStarted)
            {
                RestoreFailedTravel(player, snapshot);
                yield break;
            }

            PlayEnterSound();
            acceptedTravel = true;
            yield return WaitForAcceptedTransitionToFinish();

            RestoreFailedTravel(player, snapshot);
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

    private IEnumerator PlayEntrancePresentation(PortalEntranceSnapshot snapshot)
    {
        if (!snapshot.IsValid)
            yield break;

        if (snapshot.PlayerTransform == null)
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
        ISceneTransitionHandle coordinator = SceneTransitionPlayback.Instance;
        while (coordinator != null && coordinator.IsTransitionActive)
        {
            yield return null;
            coordinator = SceneTransitionPlayback.Instance;
        }
    }

    private void RestoreFailedTravel(IPlayerInteractor player, PortalEntranceSnapshot snapshot)
    {
        acceptedTravel = false;
        isTransitioning = false;

        if (snapshot.IsValid)
            snapshot.RestoreAll();

        hasActiveEntranceSnapshot = false;
        player?.SetInteractState(InteractState.Idle);
    }

    private bool TryStartTravelSafely()
    {
        try
        {
            return ScenePortalTravelPlayback.TryTravel(this);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScenePortal] Travel request failed with exception. portal={name}, error={ex}", this);
            return false;
        }
    }

    private void PlayEnterSound()
    {
        SoundPlaybackUtility.Play(EnterSound, causer: gameObject, position: transform.position, sourceObject: this);
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

    private void EnsurePortalId()
    {
        if (string.IsNullOrWhiteSpace(portalId) || HasDuplicatePortalId())
            portalId = Guid.NewGuid().ToString("N");
    }

    private void EnsurePendingStartRunPlan()
    {
        if (transitionType != TransitionType.HubToRunStart)
            return;

        RunRoutePlayback.EnsurePendingPlan(this);
    }

    private void RefreshAccessRules()
    {
        accessRules = GetComponents<IScenePortalAccessRule>();
    }

    private bool CanPassAccessRules(IPlayerInteractor player)
    {
        if (accessRules == null || accessRules.Length == 0)
            return true;

        for (int i = 0; i < accessRules.Length; i++)
        {
            IScenePortalAccessRule rule = accessRules[i];
            if (rule == null ||
                rule is Behaviour { isActiveAndEnabled: false } ||
                rule.CanAccess(this, player))
            {
                continue;
            }

            rule.HandleAccessDenied(this, player);
            return false;
        }

        return true;
    }

    private bool HasDuplicatePortalId()
    {
        if (string.IsNullOrWhiteSpace(portalId))
            return false;

        var portals = FindObjectsByType<ScenePortal>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < portals.Length; i++)
        {
            var other = portals[i];
            if (other == null || other == this)
                continue;

            if (other.portalId == portalId)
                return true;
        }

        return false;
    }

    // 책임: 포탈 입장 연출 실패 시 복구할 플레이어 transform/rigidbody 상태를 보관한다.
    private readonly struct PortalEntranceSnapshot
    {
        public readonly Transform PlayerTransform;
        public readonly Rigidbody2D Body;
        public readonly Vector3 Position;
        public readonly Vector3 LocalScale;
        public readonly Quaternion LocalRotation;
        public readonly RigidbodyType2D BodyType;
        public readonly Vector2 LinearVelocity;
        public readonly float AngularVelocity;

        private PortalEntranceSnapshot(
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

        public static PortalEntranceSnapshot Capture(IPlayerInteractor player)
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

            return new PortalEntranceSnapshot(
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
        PortalEntranceSnapshot snapshot,
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

#if UNITY_EDITOR
    private void EmitEditorDiagnosticIfBlocked(
        IPlayerInteractor player,
        bool hasPlayer,
        bool isIdle,
        bool canResolve,
        bool canInteract)
    {
        if (canInteract || !Application.isPlaying || transitionType != TransitionType.HubToRunStart)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.Equals(activeScene.name, "ProtoTypeHub", StringComparison.OrdinalIgnoreCase))
            return;

        float now = Time.unscaledTime;
        if (now < nextEditorDiagnosticLogTime)
            return;

        string routeDebug = RunRoutePlayback.GetDebugResolveStatus(this);

        string message =
            $"[ScenePortal] Hub start portal blocked. portal={name}, " +
            $"isTransitioning={isTransitioning}, hasPlayer={hasPlayer}, " +
            $"playerState={(hasPlayer ? player.CurrentState.ToString() : "<none>")}, " +
            $"isIdle={isIdle}, canResolve={canResolve}, route={routeDebug}";

        if (string.Equals(lastEditorDiagnosticMessage, message, StringComparison.Ordinal))
        {
            nextEditorDiagnosticLogTime = now + 1f;
            return;
        }

        lastEditorDiagnosticMessage = message;
        nextEditorDiagnosticLogTime = now + 1f;
        Debug.LogWarning(message, this);
    }
#endif
}

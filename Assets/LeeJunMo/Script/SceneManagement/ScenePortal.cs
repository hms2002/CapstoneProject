using System;
using System.Collections;
using System.Collections.Generic;
using CapstoneAudio;
using DG.Tweening;
using UnityEngine;
using UnityGAS;
#if UNITY_EDITOR
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// 책임 : 플레이어 상호작용을 받아 포탈 이동을 요청하는 진입점이다.
/// 실제 경로 해석은 현재 런 계획을 가진 PortalRouteManager에 위임한다.
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
    private Sequence entranceSequence;
    private PortalEntranceSnapshot activeEntranceSnapshot;
    private PlayerCinematicProtection lockedPlayerProtection;
    private GameFlowInputBlocker entranceInputBlocker;
    private object oneShotDestinationOverrideOwner;
    private string oneShotDestinationOverrideSceneName;

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

        propBlock = new MaterialPropertyBlock();
        OnUnHighlight();
    }

    private void OnEnable()
    {
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
        PortalRouteManager routeManager = PortalRouteManager.EnsureInstance();
        bool canResolve = HasOneShotDestinationOverride ||
                          routeManager != null &&
                          routeManager.CanResolveRoute(this);

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
        SceneTransitionCoordinator coordinator = SceneTransitionCoordinator.Instance;
        return coordinator != null && coordinator.IsTransitionActive;
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
            KillEntranceSequence();
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

        KillEntranceSequence();

        Vector3 targetPosition = ResolveEntranceTargetPosition(snapshot.PlayerTransform);
        Vector3 targetRotation = snapshot.LocalRotation.eulerAngles +
                                 new Vector3(0f, 0f, entranceRotationDegrees);

        if (!TryCreateEntranceSequence(snapshot, targetPosition, targetRotation))
            yield break;

        yield return entranceSequence.WaitForCompletion();
        entranceSequence = null;
    }

    private bool TryCreateEntranceSequence(
        PortalEntranceSnapshot snapshot,
        Vector3 targetPosition,
        Vector3 targetRotation)
    {
        try
        {
            entranceSequence = DOTween.Sequence();
            entranceSequence.Join(snapshot.PlayerTransform.DOMove(targetPosition, entranceDuration).SetEase(Ease.OutQuart));
            entranceSequence.Join(snapshot.PlayerTransform.DOScale(Vector3.zero, entranceDuration).SetEase(Ease.InBack));
            entranceSequence.Join(snapshot.PlayerTransform.DOLocalRotate(targetRotation, entranceDuration, RotateMode.FastBeyond360).SetEase(Ease.InCubic));
            return true;
        }
        catch (Exception ex)
        {
            KillEntranceSequence();
            Debug.LogError($"[ScenePortal] Entrance presentation failed. portal={name}, error={ex}", this);
            return false;
        }
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
        SceneTransitionCoordinator coordinator = SceneTransitionCoordinator.Instance;
        while (coordinator != null && coordinator.IsTransitionActive)
        {
            yield return null;
            coordinator = SceneTransitionCoordinator.Instance;
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
            return ScenePortalTravelService.TryTravel(this);
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

        KillEntranceSequence();

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

    private void KillEntranceSequence()
    {
        if (entranceSequence == null)
            return;

        entranceSequence.Kill();
        entranceSequence = null;
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

        PortalRouteManager routeManager = PortalRouteManager.EnsureInstance();
        if (routeManager == null)
            return;

        routeManager.EnsurePendingPlan(this);
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

        PortalRouteManager routeManager = PortalRouteManager.EnsureInstance();
        string routeDebug = routeManager != null
            ? routeManager.GetDebugResolveStatus(this)
            : "manager=null";

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

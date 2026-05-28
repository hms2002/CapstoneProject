using System.Collections;
using System.Collections.Generic;
using Cainos.PixelArtTopDown_Basic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class TutorialBossEncounterSequence : MonoBehaviour
{
    [Header("Playback")]
    [SerializeField] private bool playOnStart;
    [SerializeField] private bool playOnlyOnce = true;

    [Header("Player")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private bool lockPlayerControls = true;
    [SerializeField] private bool blockPlayerTargetability = true;
    [SerializeField] private bool keepPlayerLockedAfterSequence = true;
    [SerializeField] private bool pauseRunTimer = true;

    [Header("Initial Aim")]
    [SerializeField] private bool applyInitialAimOnSequenceStart;
    [SerializeField] private Transform initialAimTarget;
    [SerializeField] private Vector2 fallbackInitialAimDirection = Vector2.right;

    [Header("HUD")]
    [SerializeField] private bool hideDefaultHudDuringSequence = true;

    [Header("Camera")]
    [SerializeField] private CameraPresentationDirector cameraDirector;
    [SerializeField] private bool useCameraPresentationDirector;
    [SerializeField] private Transform bossFocusTarget;
    [SerializeField, Min(0f)] private float cameraFocusWaitSeconds = 0.65f;
    [SerializeField, Min(0f)] private float cameraReturnWaitSeconds = 0.45f;
    [SerializeField] private bool zoomGameplayCameraDuringFocus = true;
    [SerializeField, Min(0.01f)] private float focusOrthographicSize = 4f;
    [SerializeField, Min(0f)] private float cameraZoomInSeconds = 0.35f;
    [SerializeField, Min(0f)] private float cameraZoomOutSeconds = 0.25f;

    [Header("Initial Camera")]
    [SerializeField] private bool focusPlayerBeforeFirstBossFocus = true;
    [SerializeField] private bool waitForSceneTransitionBeforeInitialPlayerFocus = true;
    [SerializeField, Min(0f)] private float initialPlayerFocusWaitSeconds = 0.75f;

    [Header("Laser Camera")]
    [SerializeField] private bool focusPlayerBeforeLaser = true;
    [SerializeField] private Transform playerFocusTarget;
    [SerializeField, Min(0f)] private float playerFocusWaitSeconds = 0.45f;
    [SerializeField, Min(0.01f)] private float playerFocusOrthographicSize = 3.25f;
    [SerializeField] private bool refocusBossAfterLaser = true;
    [SerializeField, Min(0f)] private float bossRefocusWaitSeconds = 0.45f;

    [Header("Letterbox")]
    [SerializeField] private bool useLetterbox = true;
    [SerializeField, Min(0f)] private float letterboxInSeconds = 0.35f;
    [SerializeField, Min(0f)] private float letterboxOutSeconds = 0.25f;
    [SerializeField, Range(0f, 0.45f)] private float letterboxScreenHeightRatio = 0.14f;
    [SerializeField, Range(0f, 1f)] private float uiTargetAlpha = 0f;
    [SerializeField] private bool useCustomFadedLayers;
    [SerializeField] private GlobalCanvasLayer[] fadedLayers;

    [Header("Boss Visual")]
    [SerializeField] private Transform bossVisualRoot;
    [SerializeField] private bool scaleBossVisualOnFocus = true;
    [SerializeField, Min(0.01f)] private float bossFocusScaleMultiplier = 1.15f;
    [SerializeField, Min(0f)] private float bossScaleInSeconds = 0.25f;
    [SerializeField, Min(0f)] private float bossScaleOutSeconds = 0.2f;

    [Header("Dialogue")]
    [SerializeField] private NPCData tutorialBossNpcData;
    [SerializeField] private TextAsset firstDialogueInk;
    [SerializeField] private string firstDialogueStartPath;
    [SerializeField] private TextAsset secondDialogueInk;
    [SerializeField] private string secondDialogueStartPath;

    [Header("Laser And HP")]
    [SerializeField] private TutorialBossLaserPresentation laserPresentation;
    [SerializeField] private TutorialPresentationHpView presentationHpView;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float delayAfterFirstDialogueSeconds = 0.2f;
    [SerializeField, Min(0f)] private float delayAfterLaserSeconds = 0.2f;
    [SerializeField, Min(0f)] private float collapseDelaySeconds = 0.75f;
    [SerializeField, Min(0f)] private float gameOverDelaySeconds = 0.25f;

    [Header("Fake Game Over")]
    [SerializeField] private bool showFakeGameOver = true;
    [SerializeField] private string fakeGameOverCauseName = "마왕";
    [SerializeField] private string fakeGameOverMessageText = "처치자 마왕";
    [SerializeField] private string fakeGameOverLocationName = "마왕의 알현실";
    [SerializeField] private bool hideFakeGameOverTimeText = true;
    [SerializeField] private string fakeGameOverButtonLabel = "추락";
    [SerializeField] private string returnSceneName = "ProtoTypeHub";
    [SerializeField] private bool useSceneTransitionService = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onSequenceStarted = new();
    [SerializeField] private UnityEvent onBeforeFirstDialogue = new();
    [SerializeField] private UnityEvent onFirstDialogueCompleted = new();
    [SerializeField] private UnityEvent onBeforeLaser = new();
    [SerializeField] private UnityEvent onLaserCompleted = new();
    [SerializeField] private UnityEvent onBeforeSecondDialogue = new();
    [SerializeField] private UnityEvent onSecondDialogueCompleted = new();
    [SerializeField] private UnityEvent onPlayerHit = new();
    [SerializeField] private UnityEvent onPlayerCollapse = new();
    [SerializeField] private UnityEvent onBeforeGameOver = new();
    [SerializeField] private UnityEvent onSequenceCompleted = new();
    [SerializeField] private UnityEvent onSequenceCanceled = new();

    private Coroutine sequenceRoutine;
    private CinematicLetterboxOverlay letterboxOverlay;
    private GameFlowInputBlocker inputBlocker;
    private PlayerCinematicProtection playerProtection;
    private PlayerAnimatorController2D playerAnimatorLock;
    private WeaponPresentationRig2D weaponPresentationLock;
    private PlayerTargetabilityBlocker targetabilityBlocker;
    private PlayerHitFeedback2D playerHitFeedback;
    private PlayerDeathPresentation2D playerDeathPresentation;
    private Transform protectedPlayerTransform;
    private Transform targetabilityBlockedPlayerTransform;
    private Transform playerPresentationLockedTransform;
    private Transform cachedPlayerPresentationTransform;
    private CinemachineCamera gameplayCamera;
    private CinemachineBrain cameraBrain;
    private CameraFollow legacyFollowCamera;
    private Transform cachedCameraFollow;
    private Transform cachedCameraLookAt;
    private int cachedCameraPriority;
    private float cachedCameraOrthographicSize;
    private bool cachedLegacyFollowEnabled;
    private bool cachedBrainIgnoreTimeScale;
    private bool hasCachedCameraState;
    private bool hasCachedCameraLens;
    private Vector3 cachedBossVisualScale;
    private bool hasCachedBossVisualScale;
    private bool hasPlayed;
    private bool hasAcquiredPlayerProtection;
    private bool hasAcquiredPlayerPresentationLock;
    private bool hasAcquiredTargetabilityBlock;
    private bool holdsTransitionPlayerUnlockBlock;
    private bool holdsRunTimerPause;
    private bool subscribedToLaserHit;
    private bool subscribedToHpDepleted;
    private bool hasHiddenDefaultHudRoots;
    private bool hasStartedPlayerDeathPresentation;
    private bool hasInvokedPlayerCollapse;
    private Coroutine playerDeathPresentationRoutine;
    private readonly List<HudObjectActiveState> hiddenDefaultHudStates = new();
    private const int PresentationHpSortingOrder = short.MaxValue;
    private static readonly GlobalCanvasLayer[] DefaultHudLayersToHide =
    {
        GlobalCanvasLayer.GameplayHUD,
        GlobalCanvasLayer.BossHUD
    };

    private readonly struct HudObjectActiveState
    {
        public HudObjectActiveState(GameObject gameObject, bool wasActive)
        {
            GameObject = gameObject;
            WasActive = wasActive;
        }

        public GameObject GameObject { get; }
        public bool WasActive { get; }
    }

    public bool IsRunning => sequenceRoutine != null;
    public bool HasPlayed => hasPlayed;

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;
    }

    private void Start()
    {
        presentationHpView?.SetVisible(false);

        if (playOnStart)
            BeginSequence();
    }

    private void LateUpdate()
    {
        if (ShouldMaintainPlayerLock())
            MaintainPlayerLock();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
        CancelSequence(invokeCanceled: false);
    }

    public void BeginSequence()
    {
        if (sequenceRoutine != null)
            return;

        if (playOnlyOnce && hasPlayed)
            return;

        AcquireTransitionPlayerUnlockBlock();
        HideDefaultHudRoots();
        sequenceRoutine = StartCoroutine(SequenceRoutine());
    }

    public void CancelSequence()
    {
        CancelSequence(invokeCanceled: true);
    }

    private IEnumerator SequenceRoutine()
    {
        yield return WaitForPlayerTransformRoutine();
        AcquireSequenceState();
        HideDefaultHudRoots();
        SubscribePresentationEvents();
        hasStartedPlayerDeathPresentation = false;
        hasInvokedPlayerCollapse = false;
        playerDeathPresentationRoutine = null;
        presentationHpView?.ResetToMax();
        presentationHpView?.SetVisible(false);

        onSequenceStarted?.Invoke();

        yield return WaitForSceneTransitionBeforeInitialFocusRoutine();
        if (focusPlayerBeforeFirstBossFocus)
            yield return FocusInitialPlayerRoutine();

        yield return FocusCameraRoutine();
        yield return ScaleBossVisualRoutine(focus: true);

        onBeforeFirstDialogue?.Invoke();
        yield return PlayDialogueRoutine(firstDialogueInk, firstDialogueStartPath, "first");
        onFirstDialogueCompleted?.Invoke();
        yield return WaitForPresentationSeconds(delayAfterFirstDialogueSeconds);

        PrepareLaserHpHud();
        yield return PlayLetterboxInIfNeededRoutine();
        presentationHpView?.SetVisible(true);

        if (focusPlayerBeforeLaser)
            yield return FocusPlayerForLaserRoutine();

        onBeforeLaser?.Invoke();
        presentationHpView?.SetVisible(true);
        if (laserPresentation != null)
            yield return laserPresentation.PlayRoutine();
        onLaserCompleted?.Invoke();
        yield return WaitForPresentationSeconds(delayAfterLaserSeconds);
        presentationHpView?.SetVisible(false);
        yield return WaitForPlayerDeathPresentationIfRunningRoutine();

        yield return PlayLetterboxOutIfNeededRoutine();

        if (refocusBossAfterLaser)
            yield return RefocusBossAfterLaserRoutine();

        onBeforeSecondDialogue?.Invoke();
        yield return PlayDialogueRoutine(secondDialogueInk, secondDialogueStartPath, "second");
        onSecondDialogueCompleted?.Invoke();

        yield return ScaleBossVisualRoutine(focus: false);
        yield return ReturnCameraRoutine();

        yield return WaitForPresentationSeconds(gameOverDelaySeconds);
        onBeforeGameOver?.Invoke();
        if (showFakeGameOver)
            ShowFakeGameOver();

        hasPlayed = true;
        sequenceRoutine = null;
        CleanupSequenceState(releasePlayerLocks: !keepPlayerLockedAfterSequence);
        onSequenceCompleted?.Invoke();
    }

    private IEnumerator PlayDialogueRoutine(TextAsset inkJson, string startPath, string label)
    {
        if (inkJson == null)
            yield break;

        if (tutorialBossNpcData == null)
        {
            Debug.LogError($"[TutorialBossEncounterSequence] Tutorial boss NPCData is missing for {label} dialogue.", this);
            yield break;
        }

        if (DialogueService.Instance == null)
        {
            Debug.LogError("[TutorialBossEncounterSequence] DialogueService instance was not found.", this);
            yield break;
        }

        List<DialogueStorySegment> segments = new()
        {
            new DialogueStorySegment(inkJson, startPath)
        };
        List<NPCData> participants = new() { tutorialBossNpcData };

        if (!DialogueService.Instance.TryStartDialogueSequence(segments, participants))
            yield break;

        yield return new WaitUntil(() => DialogueService.Instance == null || !DialogueService.Instance.IsPlaying);
    }

    private IEnumerator PlayLetterboxInRoutine()
    {
        if (letterboxOverlay == null)
            yield break;

        if (useCustomFadedLayers)
        {
            yield return letterboxOverlay.PlayIn(
                letterboxInSeconds,
                letterboxScreenHeightRatio,
                uiTargetAlpha,
                fadedLayers);
            yield break;
        }

        yield return letterboxOverlay.PlayIn(letterboxInSeconds, letterboxScreenHeightRatio, uiTargetAlpha);
    }

    private void PrepareLaserHpHud()
    {
        if (presentationHpView == null)
            return;

        EnsurePresentationHpViewRenderable();
        presentationHpView.ResetToMax();
        presentationHpView.Refresh();
        presentationHpView.SetVisible(true);
    }

    private void EnsurePresentationHpViewRenderable()
    {
        if (presentationHpView == null)
            return;

        Canvas canvas = presentationHpView.GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            if (!canvas.gameObject.activeSelf)
                canvas.gameObject.SetActive(true);

            canvas.enabled = true;
            canvas.overrideSorting = true;
            canvas.sortingOrder = PresentationHpSortingOrder;
        }

        if (!presentationHpView.gameObject.activeSelf)
            presentationHpView.gameObject.SetActive(true);
    }

    private IEnumerator PlayLetterboxInIfNeededRoutine()
    {
        if (!useLetterbox)
            yield break;

        letterboxOverlay ??= new CinematicLetterboxOverlay();
        yield return PlayLetterboxInRoutine();
    }

    private IEnumerator PlayLetterboxOutIfNeededRoutine()
    {
        if (!useLetterbox || letterboxOverlay == null)
            yield break;

        yield return letterboxOverlay.PlayOut(letterboxOutSeconds);
        letterboxOverlay.Dispose();
        letterboxOverlay = null;
    }

    private IEnumerator WaitForSceneTransitionBeforeInitialFocusRoutine()
    {
        if (!waitForSceneTransitionBeforeInitialPlayerFocus)
            yield break;

        while (IsSceneTransitionActive())
            yield return null;
    }

    private IEnumerator FocusInitialPlayerRoutine()
    {
        if (useCameraPresentationDirector && cameraDirector != null)
        {
            yield return cameraDirector.ReturnToPlayerRoutine();
            yield return WaitForPresentationSeconds(initialPlayerFocusWaitSeconds);
            yield break;
        }

        Transform target = playerFocusTarget != null ? playerFocusTarget : ResolvePlayerTransform();
        if (target == null)
            yield break;

        CacheCameraState();
        CameraBootstrap.CenterGameplayCameraOn(target);
        SetCameraTarget(target);
        yield return ZoomCameraWhileWaitingRoutine(
            playerFocusOrthographicSize,
            cameraZoomInSeconds,
            initialPlayerFocusWaitSeconds);
    }

    private IEnumerator FocusCameraRoutine()
    {
        if (useCameraPresentationDirector && cameraDirector != null)
        {
            yield return cameraDirector.FocusBossWithPhaseLensRoutine();
            yield break;
        }

        Transform focusTarget = bossFocusTarget != null ? bossFocusTarget : transform;
        CacheCameraState();
        SetCameraTarget(focusTarget);
        yield return ZoomCameraWhileWaitingRoutine(focusOrthographicSize, cameraZoomInSeconds, cameraFocusWaitSeconds);
    }

    private IEnumerator FocusPlayerForLaserRoutine()
    {
        if (useCameraPresentationDirector && cameraDirector != null)
        {
            yield return cameraDirector.ReturnToPlayerRoutine();
            yield break;
        }

        CacheCameraState();
        Transform target = playerFocusTarget != null ? playerFocusTarget : ResolvePlayerTransform();
        SetCameraTarget(target);
        yield return ZoomCameraWhileWaitingRoutine(playerFocusOrthographicSize, cameraZoomInSeconds, playerFocusWaitSeconds);
    }

    private IEnumerator RefocusBossAfterLaserRoutine()
    {
        if (useCameraPresentationDirector && cameraDirector != null)
        {
            yield return cameraDirector.FocusBossWithPhaseLensRoutine();
            yield break;
        }

        CacheCameraState();
        Transform target = bossFocusTarget != null ? bossFocusTarget : transform;
        SetCameraTarget(target);
        yield return ZoomCameraWhileWaitingRoutine(focusOrthographicSize, cameraZoomInSeconds, bossRefocusWaitSeconds);
    }

    private IEnumerator ReturnCameraRoutine()
    {
        if (useCameraPresentationDirector && cameraDirector != null)
        {
            yield return cameraDirector.ReturnToPlayerRoutine();
            yield break;
        }

        Transform restoreTarget = ResolvePlayerTransform();
        SetCameraTarget(restoreTarget);

        float restoreOrthographicSize = hasCachedCameraLens
            ? cachedCameraOrthographicSize
            : focusOrthographicSize;
        yield return ZoomCameraWhileWaitingRoutine(restoreOrthographicSize, cameraZoomOutSeconds, cameraReturnWaitSeconds);

        RestoreCameraState(restoreTarget);
    }

    private IEnumerator ScaleBossVisualRoutine(bool focus)
    {
        if (!scaleBossVisualOnFocus || bossVisualRoot == null)
            yield break;

        if (!hasCachedBossVisualScale)
        {
            cachedBossVisualScale = bossVisualRoot.localScale;
            hasCachedBossVisualScale = true;
        }

        Vector3 start = bossVisualRoot.localScale;
        Vector3 target = focus
            ? cachedBossVisualScale * Mathf.Max(0.01f, bossFocusScaleMultiplier)
            : cachedBossVisualScale;
        float duration = focus ? bossScaleInSeconds : bossScaleOutSeconds;

        if (duration <= 0f)
        {
            bossVisualRoot.localScale = target;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && bossVisualRoot != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            bossVisualRoot.localScale = Vector3.LerpUnclamped(start, target, t);
            yield return null;
        }

        if (bossVisualRoot != null)
            bossVisualRoot.localScale = target;
    }

    private IEnumerator ZoomCameraWhileWaitingRoutine(
        float targetOrthographicSize,
        float zoomDuration,
        float minimumWaitSeconds)
    {
        float waitDuration = Mathf.Max(0f, minimumWaitSeconds);
        if (!zoomGameplayCameraDuringFocus || gameplayCamera == null)
        {
            yield return WaitForPresentationSeconds(waitDuration);
            yield break;
        }

        float startOrthographicSize = GetCameraOrthographicSize(gameplayCamera, targetOrthographicSize);
        float clampedTargetSize = Mathf.Max(0.01f, targetOrthographicSize);
        float clampedZoomDuration = Mathf.Max(0f, zoomDuration);
        float totalDuration = Mathf.Max(waitDuration, clampedZoomDuration);

        if (totalDuration <= 0f)
        {
            SetCameraOrthographicSize(gameplayCamera, clampedTargetSize);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (clampedZoomDuration > 0f)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / clampedZoomDuration));
                SetCameraOrthographicSize(
                    gameplayCamera,
                    Mathf.Lerp(startOrthographicSize, clampedTargetSize, t));
            }

            yield return null;
        }

        SetCameraOrthographicSize(gameplayCamera, clampedTargetSize);
    }

    private IEnumerator WaitForPresentationSeconds(float seconds)
    {
        float duration = Mathf.Max(0f, seconds);
        if (duration <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForPlayerTransformRoutine()
    {
        if (ResolvePlayerTransform() != null)
            yield break;

        float elapsed = 0f;
        bool warned = false;
        while (ResolvePlayerTransform() == null)
        {
            elapsed += Time.unscaledDeltaTime;
            if (!warned && elapsed >= 2f)
            {
                Debug.LogWarning(
                    "[TutorialBossEncounterSequence] Waiting for player registration before starting tutorial boss sequence.",
                    this);
                warned = true;
            }

            yield return null;
        }
    }

    private static bool IsSceneTransitionActive()
    {
        SceneTransitionCoordinator transitionCoordinator = SceneTransitionCoordinator.Instance;
        if (transitionCoordinator != null && transitionCoordinator.IsTransitionActive)
            return true;

        SceneFadeTransitionService fadeService = SceneFadeTransitionService.Instance;
        return fadeService != null && fadeService.IsTransitionActive;
    }

    private IEnumerator PlayPlayerCollapsePresentationRoutine()
    {
        if (hasStartedPlayerDeathPresentation)
        {
            yield return WaitForPlayerDeathPresentationIfRunningRoutine();

            yield break;
        }

        yield return PlayPlayerDeathPresentationRoutine();
    }

    private IEnumerator WaitForPlayerDeathPresentationIfRunningRoutine()
    {
        while (playerDeathPresentationRoutine != null)
            yield return null;
    }

    private IEnumerator PlayPlayerDeathPresentationRoutine()
    {
        hasStartedPlayerDeathPresentation = true;
        Transform resolvedPlayer = ResolvePlayerTransform();
        if (resolvedPlayer != null)
        {
            CachePlayerPresentationComponents(resolvedPlayer);
            playerHitFeedback?.ForceEndReaction();

            if (playerDeathPresentation != null)
            {
                yield return playerDeathPresentation.Play();
                yield break;
            }
        }

        yield return WaitForPresentationSeconds(collapseDelaySeconds);
    }

    private void StartPlayerDeathPresentationIfNeeded()
    {
        if (hasStartedPlayerDeathPresentation)
            return;

        InvokePlayerCollapseOnce();
        playerDeathPresentationRoutine = StartCoroutine(PlayerDeathPresentationHandleRoutine());
    }

    private IEnumerator PlayerDeathPresentationHandleRoutine()
    {
        yield return PlayPlayerDeathPresentationRoutine();
        playerDeathPresentationRoutine = null;
    }

    private void ShowFakeGameOver()
    {
        HubIntroProgressGate.MarkDarkLordTutorialForcedDefeatCompleted();

        Transform resolvedPlayer = ResolvePlayerTransform();
        GameOverPresentationRequest request = GameOverPresentationRequest.Defeat(
            resolvedPlayer,
            ResolveFakeGameOverText(fakeGameOverCauseName, "Tutorial Boss", "마왕"),
            GameOverCauseKind.Monster,
            returnSceneName,
            useSceneTransitionService);
        request.EndRunOnReturn = false;
        request.ReturnButtonLabel = fakeGameOverButtonLabel;
        request.MessageTextOverride = ResolveFakeGameOverText(fakeGameOverMessageText, null, "처치자 마왕");
        request.LocationName = ResolveFakeGameOverText(fakeGameOverLocationName, null, "마왕의 알현실");
        request.HideTimeText = hideFakeGameOverTimeText;

        GameOverPresentationController.TryShow(request);
    }

    private static string ResolveFakeGameOverText(string value, string legacyValue, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (!string.IsNullOrWhiteSpace(legacyValue) &&
            string.Equals(value, legacyValue, System.StringComparison.Ordinal))
        {
            return fallback;
        }

        return value;
    }

    private void AcquireSequenceState()
    {
        AcquireTransitionPlayerUnlockBlock();

        inputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        inputBlocker?.Acquire();

        if (pauseRunTimer && RunTimeLimitSystem.Instance != null)
        {
            RunTimeLimitSystem.Instance.SetExternalPause(this, true);
            holdsRunTimerPause = true;
        }

        if (lockPlayerControls)
            AcquirePlayerProtection();

        if (blockPlayerTargetability)
            AcquireTargetabilityBlock();
    }

    private void CleanupSequenceState(bool releasePlayerLocks = true)
    {
        UnsubscribePresentationEvents();
        laserPresentation?.Cancel();
        presentationHpView?.SetVisible(false);

        if (releasePlayerLocks && playerDeathPresentationRoutine != null)
        {
            StopCoroutine(playerDeathPresentationRoutine);
            playerDeathPresentationRoutine = null;
        }

        if (letterboxOverlay != null)
        {
            letterboxOverlay.Dispose();
            letterboxOverlay = null;
        }

        RestoreBossVisualScaleImmediate();

        if (!useCameraPresentationDirector)
            RestoreCameraState(ResolvePlayerTransform());
        else
            cameraDirector?.RestoreDefaultState();

        if (releasePlayerLocks)
        {
            RestoreDefaultHudRoots();
            ReleaseTargetabilityBlock();
            ReleasePlayerProtection();
            ReleaseTransitionPlayerUnlockBlock();
        }

        inputBlocker?.Release();
        inputBlocker = null;

        if (holdsRunTimerPause && RunTimeLimitSystem.Instance != null)
            RunTimeLimitSystem.Instance.SetExternalPause(this, false);

        holdsRunTimerPause = false;
    }

    private void HideDefaultHudRoots()
    {
        if (!hideDefaultHudDuringSequence || hasHiddenDefaultHudRoots)
            return;

        for (int i = 0; i < DefaultHudLayersToHide.Length; i++)
        {
            Canvas canvas = GlobalUIRoot.GetCanvas(DefaultHudLayersToHide[i]);
            if (canvas == null)
                continue;

            CaptureAndSetHudObjectActive(canvas.gameObject, false);
            HideDefaultHudComponentRoots(canvas.transform);
        }

        hasHiddenDefaultHudRoots = hiddenDefaultHudStates.Count > 0;
    }

    private void HideDefaultHudComponentRoots(Transform canvasRoot)
    {
        if (canvasRoot == null)
            return;

        HideHudComponentRoots<PlayerHealthHeartHUD>(canvasRoot);
        HideHudComponentRoots<WeaponSkillHUD2D>(canvasRoot);
        HideHudComponentRoots<PlayerConsumableHUD2D>(canvasRoot);
        HideHudComponentRoots<StatusHudPresenter>(canvasRoot);
        HideHudComponentRoots<BossHealthBarUI>(canvasRoot);
    }

    private void HideHudComponentRoots<T>(Transform canvasRoot) where T : Component
    {
        T[] components = canvasRoot.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null)
                CaptureAndSetHudObjectActive(component.gameObject, false);
        }
    }

    private void CaptureAndSetHudObjectActive(GameObject target, bool active)
    {
        if (target == null || HasCapturedHudObject(target))
            return;

        hiddenDefaultHudStates.Add(new HudObjectActiveState(target, target.activeSelf));
        if (target.activeSelf != active)
            target.SetActive(active);
    }

    private bool HasCapturedHudObject(GameObject target)
    {
        for (int i = 0; i < hiddenDefaultHudStates.Count; i++)
        {
            if (hiddenDefaultHudStates[i].GameObject == target)
                return true;
        }

        return false;
    }

    private void RestoreDefaultHudRoots()
    {
        for (int i = hiddenDefaultHudStates.Count - 1; i >= 0; i--)
        {
            HudObjectActiveState state = hiddenDefaultHudStates[i];
            if (state.GameObject != null && state.GameObject.activeSelf != state.WasActive)
                state.GameObject.SetActive(state.WasActive);
        }

        hiddenDefaultHudStates.Clear();
        hasHiddenDefaultHudRoots = false;
    }

    private void CancelSequence(bool invokeCanceled)
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        CleanupSequenceState(releasePlayerLocks: true);

        if (invokeCanceled)
            onSequenceCanceled?.Invoke();
    }

    private void SubscribePresentationEvents()
    {
        SubscribeLaserEvents();
        SubscribePresentationHpEvents();
    }

    private void UnsubscribePresentationEvents()
    {
        UnsubscribeLaserEvents();
        UnsubscribePresentationHpEvents();
    }

    private void SubscribeLaserEvents()
    {
        if (laserPresentation == null || subscribedToLaserHit)
            return;

        laserPresentation.OnStepHit.AddListener(HandleLaserStepHit);
        subscribedToLaserHit = true;
    }

    private void UnsubscribeLaserEvents()
    {
        if (laserPresentation == null || !subscribedToLaserHit)
            return;

        laserPresentation.OnStepHit.RemoveListener(HandleLaserStepHit);
        subscribedToLaserHit = false;
    }

    private void SubscribePresentationHpEvents()
    {
        if (presentationHpView == null || subscribedToHpDepleted)
            return;

        presentationHpView.OnDepleted.AddListener(HandlePresentationHpDepleted);
        subscribedToHpDepleted = true;
    }

    private void UnsubscribePresentationHpEvents()
    {
        if (presentationHpView == null || !subscribedToHpDepleted)
            return;

        presentationHpView.OnDepleted.RemoveListener(HandlePresentationHpDepleted);
        subscribedToHpDepleted = false;
    }

    private void HandleLaserStepHit(int _)
    {
        PlayPlayerHitPresentation();

        if (presentationHpView != null && presentationHpView.CurrentHp <= 0)
            StartPlayerDeathPresentationIfNeeded();

        onPlayerHit?.Invoke();
    }

    private void HandlePresentationHpDepleted()
    {
        StartPlayerDeathPresentationIfNeeded();
    }

    private void InvokePlayerCollapseOnce()
    {
        if (hasInvokedPlayerCollapse)
            return;

        hasInvokedPlayerCollapse = true;
        onPlayerCollapse?.Invoke();
    }

    private void PlayPlayerHitPresentation()
    {
        Transform resolvedPlayer = ResolvePlayerTransform();
        if (resolvedPlayer == null)
            return;

        CachePlayerPresentationComponents(resolvedPlayer);

        GameObject causer = bossVisualRoot != null ? bossVisualRoot.gameObject : gameObject;
        playerHitFeedback?.OnHitFeedback(new HitFeedbackPayload(causer, 0f, 0f));
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        playerTransform = player.transform;
        ClearPlayerPresentationComponentCache();

        if (ShouldMaintainPlayerLock())
            MaintainPlayerLock();
    }

    private void HandlePlayerUnregistered(PlayerInteractor2D player)
    {
        if (player == null || player.transform != playerTransform)
            return;

        playerTransform = null;
        ClearPlayerPresentationComponentCache();
    }

    private bool ShouldMaintainPlayerLock()
    {
        if (!lockPlayerControls && !blockPlayerTargetability)
            return false;

        return sequenceRoutine != null || (keepPlayerLockedAfterSequence && hasAcquiredPlayerProtection);
    }

    private void MaintainPlayerLock()
    {
        Transform resolvedPlayer = ResolvePlayerTransform();
        if (resolvedPlayer == null)
            return;

        if (lockPlayerControls)
            AcquirePlayerProtection();

        if (blockPlayerTargetability)
            AcquireTargetabilityBlock();

        PlayerInteractor2D interactor = resolvedPlayer.GetComponent<PlayerInteractor2D>();
        if (interactor != null && interactor.CurrentState != InteractState.None)
            interactor.SetInteractState(InteractState.None);

        MovementMotor2D movementMotor = resolvedPlayer.GetComponent<MovementMotor2D>();
        movementMotor?.StopAllMotion();

        Rigidbody2D playerBody = resolvedPlayer.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }
    }

    private void CachePlayerPresentationComponents(Transform resolvedPlayer)
    {
        if (resolvedPlayer == null)
            return;

        if (cachedPlayerPresentationTransform == resolvedPlayer)
            return;

        cachedPlayerPresentationTransform = resolvedPlayer;
        playerHitFeedback = resolvedPlayer.GetComponent<PlayerHitFeedback2D>();
        playerDeathPresentation = resolvedPlayer.GetComponent<PlayerDeathPresentation2D>();
    }

    private void ClearPlayerPresentationComponentCache()
    {
        cachedPlayerPresentationTransform = null;
        playerHitFeedback = null;
        playerDeathPresentation = null;
    }

    private Transform ResolvePlayerTransform()
    {
        Transform registeredPlayer = PlayerRuntimeRegistry.GetPlayerTransform();
        if (registeredPlayer != null)
        {
            if (playerTransform != registeredPlayer)
            {
                playerTransform = registeredPlayer;
                ClearPlayerPresentationComponentCache();
            }

            return registeredPlayer;
        }

        return playerTransform;
    }

    private void AcquirePlayerProtection()
    {
        Transform resolvedPlayer = ResolvePlayerTransform();
        if (resolvedPlayer == null)
            return;

        if (hasAcquiredPlayerProtection && protectedPlayerTransform == resolvedPlayer)
        {
            AcquirePlayerPresentationLock(resolvedPlayer);
            return;
        }

        if (hasAcquiredPlayerProtection)
            ReleasePlayerProtection();

        playerProtection = resolvedPlayer.GetComponent<PlayerCinematicProtection>();

        if (playerProtection == null)
            playerProtection = resolvedPlayer.gameObject.AddComponent<PlayerCinematicProtection>();

        ApplyInitialAimPose(resolvedPlayer);
        playerProtection.Acquire(this);
        protectedPlayerTransform = resolvedPlayer;
        AcquirePlayerPresentationLock(resolvedPlayer);
        hasAcquiredPlayerProtection = true;
    }

    private void ApplyInitialAimPose(Transform resolvedPlayer)
    {
        if (!applyInitialAimOnSequenceStart || resolvedPlayer == null)
            return;

        if (!TryResolveInitialAimDirection(resolvedPlayer, out Vector2 direction))
            return;

        PlayerAim2D aim = resolvedPlayer.GetComponent<PlayerAim2D>();
        aim?.SetAimDirectionForPresentation(direction);

        PlayerAnimatorController2D animatorController = resolvedPlayer.GetComponent<PlayerAnimatorController2D>();
        animatorController?.ApplyFacingDirectionForPresentation(direction);

        WeaponPresentationRig2D presentationRig = resolvedPlayer.GetComponentInChildren<WeaponPresentationRig2D>(true);
        presentationRig?.RefreshNow();
    }

    private bool TryResolveInitialAimDirection(Transform resolvedPlayer, out Vector2 direction)
    {
        direction = Vector2.zero;

        if (resolvedPlayer == null)
            return false;

        if (initialAimTarget != null)
            direction = (Vector2)(initialAimTarget.position - resolvedPlayer.position);

        if (direction.sqrMagnitude <= 0.0001f)
            direction = fallbackInitialAimDirection;

        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        direction.Normalize();
        return true;
    }

    private void ReleasePlayerProtection()
    {
        if (!hasAcquiredPlayerProtection)
        {
            ReleasePlayerPresentationLock();
            return;
        }

        playerProtection?.Release(this);
        playerProtection = null;
        protectedPlayerTransform = null;
        ReleasePlayerPresentationLock();
        hasAcquiredPlayerProtection = false;
    }

    private void AcquirePlayerPresentationLock(Transform resolvedPlayer)
    {
        if (resolvedPlayer == null)
            return;

        if (hasAcquiredPlayerPresentationLock && playerPresentationLockedTransform != resolvedPlayer)
            ReleasePlayerPresentationLock();

        if (playerAnimatorLock == null)
            playerAnimatorLock = resolvedPlayer.GetComponent<PlayerAnimatorController2D>();

        playerAnimatorLock?.AcquireCinematicFacingLock(this);

        if (weaponPresentationLock == null)
            weaponPresentationLock = resolvedPlayer.GetComponentInChildren<WeaponPresentationRig2D>(true);

        weaponPresentationLock?.AcquireCinematicPresentationLock(this);
        playerPresentationLockedTransform = resolvedPlayer;
        hasAcquiredPlayerPresentationLock = playerAnimatorLock != null || weaponPresentationLock != null;
    }

    private void ReleasePlayerPresentationLock()
    {
        if (!hasAcquiredPlayerPresentationLock)
            return;

        playerAnimatorLock?.ReleaseCinematicFacingLock(this);
        playerAnimatorLock = null;

        weaponPresentationLock?.ReleaseCinematicPresentationLock(this);
        weaponPresentationLock = null;
        playerPresentationLockedTransform = null;
        hasAcquiredPlayerPresentationLock = false;
    }

    private void AcquireTargetabilityBlock()
    {
        Transform resolvedPlayer = ResolvePlayerTransform();
        if (resolvedPlayer == null)
            return;

        if (hasAcquiredTargetabilityBlock && targetabilityBlockedPlayerTransform == resolvedPlayer)
            return;

        if (hasAcquiredTargetabilityBlock)
            ReleaseTargetabilityBlock();

        targetabilityBlocker = PlayerTargetabilityBlocker.GetOrAdd(resolvedPlayer);
        targetabilityBlocker?.Acquire(this);
        targetabilityBlockedPlayerTransform = resolvedPlayer;
        hasAcquiredTargetabilityBlock = true;
    }

    private void ReleaseTargetabilityBlock()
    {
        if (!hasAcquiredTargetabilityBlock)
            return;

        targetabilityBlocker?.Release(this);
        targetabilityBlocker = null;
        targetabilityBlockedPlayerTransform = null;
        hasAcquiredTargetabilityBlock = false;
    }

    private void AcquireTransitionPlayerUnlockBlock()
    {
        if (holdsTransitionPlayerUnlockBlock)
            return;

        SceneFadeTransitionService transitionService = SceneFadeTransitionService.EnsureInstance();
        if (transitionService == null)
            return;

        transitionService.SetPlayerUnlockBlocked(this, true);
        holdsTransitionPlayerUnlockBlock = true;
    }

    private void ReleaseTransitionPlayerUnlockBlock()
    {
        if (!holdsTransitionPlayerUnlockBlock)
            return;

        SceneFadeTransitionService transitionService = SceneFadeTransitionService.Instance;
        if (transitionService != null)
            transitionService.SetPlayerUnlockBlocked(this, false);

        holdsTransitionPlayerUnlockBlock = false;
    }

    private void CacheCameraState()
    {
        if (hasCachedCameraState)
            return;

        CameraBootstrap.EnsureRuntimeRigForCurrentScene();

        gameplayCamera = CameraBootstrap.GetPlayerCamera();
        cameraBrain = CameraBootstrap.GetBrain();
        legacyFollowCamera = CameraBootstrap.GetLegacyFollow();

        if (gameplayCamera != null)
        {
            cachedCameraFollow = gameplayCamera.Follow;
            cachedCameraLookAt = gameplayCamera.LookAt;
            cachedCameraPriority = gameplayCamera.Priority;
            cachedCameraOrthographicSize = GetCameraOrthographicSize(gameplayCamera, focusOrthographicSize);
            hasCachedCameraLens = true;
        }

        if (legacyFollowCamera != null)
            cachedLegacyFollowEnabled = legacyFollowCamera.enabled;

        if (cameraBrain != null)
            cachedBrainIgnoreTimeScale = cameraBrain.IgnoreTimeScale;

        hasCachedCameraState = true;
    }

    private void SetCameraTarget(Transform target)
    {
        if (target == null)
            return;

        if (cameraBrain != null)
            cameraBrain.IgnoreTimeScale = true;

        if (legacyFollowCamera != null)
            legacyFollowCamera.enabled = false;

        if (gameplayCamera == null)
            return;

        gameplayCamera.Follow = target;
        gameplayCamera.LookAt = target;
    }

    private void RestoreCameraState(Transform preferredTarget)
    {
        if (!hasCachedCameraState)
            return;

        Transform restoreFollow = preferredTarget != null ? preferredTarget : cachedCameraFollow;
        Transform restoreLookAt = preferredTarget != null ? preferredTarget : cachedCameraLookAt;

        if (gameplayCamera != null)
        {
            gameplayCamera.Follow = restoreFollow;
            gameplayCamera.LookAt = restoreLookAt;
            gameplayCamera.Priority = cachedCameraPriority;

            if (hasCachedCameraLens)
                SetCameraOrthographicSize(gameplayCamera, cachedCameraOrthographicSize);
        }

        if (legacyFollowCamera != null)
        {
            if (restoreFollow != null)
                legacyFollowCamera.BindTarget(restoreFollow, snap: false);

            legacyFollowCamera.enabled = cachedLegacyFollowEnabled;
        }

        if (cameraBrain != null)
            cameraBrain.IgnoreTimeScale = cachedBrainIgnoreTimeScale;

        cachedCameraFollow = null;
        cachedCameraLookAt = null;
        hasCachedCameraState = false;
        hasCachedCameraLens = false;
    }

    private void RestoreBossVisualScaleImmediate()
    {
        if (!hasCachedBossVisualScale || bossVisualRoot == null)
            return;

        bossVisualRoot.localScale = cachedBossVisualScale;
        hasCachedBossVisualScale = false;
    }

    private static float GetCameraOrthographicSize(CinemachineCamera camera, float fallbackOrthographicSize)
    {
        if (camera == null)
            return Mathf.Max(0.01f, fallbackOrthographicSize);

        var lens = camera.Lens;
        return Mathf.Max(0.01f, lens.OrthographicSize);
    }

    private static void SetCameraOrthographicSize(CinemachineCamera camera, float orthographicSize)
    {
        if (camera == null)
            return;

        var lens = camera.Lens;
        lens.OrthographicSize = Mathf.Max(0.01f, orthographicSize);
        camera.Lens = lens;
    }
}

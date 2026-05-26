using System.Collections;
using System.Collections.Generic;
using Cainos.PixelArtTopDown_Basic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

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
    [SerializeField] private bool pauseRunTimer = true;

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
    [SerializeField] private string fakeGameOverCauseName = "Tutorial Boss";
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
    private PlayerTargetabilityBlocker targetabilityBlocker;
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
    private bool hasAcquiredTargetabilityBlock;
    private bool holdsRunTimerPause;
    private bool subscribedToLaserHit;

    public bool IsRunning => sequenceRoutine != null;
    public bool HasPlayed => hasPlayed;

    private void Start()
    {
        if (playOnStart)
            BeginSequence();
    }

    private void OnDisable()
    {
        CancelSequence(invokeCanceled: false);
    }

    public void BeginSequence()
    {
        if (sequenceRoutine != null)
            return;

        if (playOnlyOnce && hasPlayed)
            return;

        sequenceRoutine = StartCoroutine(SequenceRoutine());
    }

    public void CancelSequence()
    {
        CancelSequence(invokeCanceled: true);
    }

    private IEnumerator SequenceRoutine()
    {
        ResolvePlayerTransform();
        AcquireSequenceState();
        SubscribeLaserEvents();
        presentationHpView?.ResetToMax();

        onSequenceStarted?.Invoke();

        if (useLetterbox)
        {
            letterboxOverlay = new CinematicLetterboxOverlay();
            yield return PlayLetterboxInRoutine();
        }

        yield return FocusCameraRoutine();
        yield return ScaleBossVisualRoutine(focus: true);

        onBeforeFirstDialogue?.Invoke();
        yield return PlayDialogueRoutine(firstDialogueInk, firstDialogueStartPath, "first");
        onFirstDialogueCompleted?.Invoke();
        yield return WaitForPresentationSeconds(delayAfterFirstDialogueSeconds);

        onBeforeLaser?.Invoke();
        if (laserPresentation != null)
            yield return laserPresentation.PlayRoutine();
        onLaserCompleted?.Invoke();
        yield return WaitForPresentationSeconds(delayAfterLaserSeconds);

        onBeforeSecondDialogue?.Invoke();
        yield return PlayDialogueRoutine(secondDialogueInk, secondDialogueStartPath, "second");
        onSecondDialogueCompleted?.Invoke();

        onPlayerCollapse?.Invoke();
        yield return WaitForPresentationSeconds(collapseDelaySeconds);

        yield return ScaleBossVisualRoutine(focus: false);
        yield return ReturnCameraRoutine();

        if (useLetterbox && letterboxOverlay != null)
            yield return letterboxOverlay.PlayOut(letterboxOutSeconds);

        letterboxOverlay?.Dispose();
        letterboxOverlay = null;

        yield return WaitForPresentationSeconds(gameOverDelaySeconds);
        onBeforeGameOver?.Invoke();
        if (showFakeGameOver)
            ShowFakeGameOver();

        hasPlayed = true;
        sequenceRoutine = null;
        CleanupSequenceState();
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

    private void ShowFakeGameOver()
    {
        Transform resolvedPlayer = ResolvePlayerTransform();
        GameOverPresentationRequest request = GameOverPresentationRequest.Defeat(
            resolvedPlayer,
            fakeGameOverCauseName,
            GameOverCauseKind.Monster,
            returnSceneName,
            useSceneTransitionService);
        request.EndRunOnReturn = false;

        GameOverPresentationController.TryShow(request);
    }

    private void AcquireSequenceState()
    {
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

    private void CleanupSequenceState()
    {
        UnsubscribeLaserEvents();
        laserPresentation?.Cancel();

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

        ReleaseTargetabilityBlock();
        ReleasePlayerProtection();
        inputBlocker?.Release();
        inputBlocker = null;

        if (holdsRunTimerPause && RunTimeLimitSystem.Instance != null)
            RunTimeLimitSystem.Instance.SetExternalPause(this, false);

        holdsRunTimerPause = false;
    }

    private void CancelSequence(bool invokeCanceled)
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        CleanupSequenceState();

        if (invokeCanceled)
            onSequenceCanceled?.Invoke();
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

    private void HandleLaserStepHit(int _)
    {
        onPlayerHit?.Invoke();
    }

    private Transform ResolvePlayerTransform()
    {
        if (playerTransform == null)
            playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();

        return playerTransform;
    }

    private void AcquirePlayerProtection()
    {
        Transform resolvedPlayer = ResolvePlayerTransform();
        if (resolvedPlayer == null)
            return;

        if (playerProtection == null)
            playerProtection = resolvedPlayer.GetComponent<PlayerCinematicProtection>();

        if (playerProtection == null)
            playerProtection = resolvedPlayer.gameObject.AddComponent<PlayerCinematicProtection>();

        playerProtection.Acquire(this);
        hasAcquiredPlayerProtection = true;
    }

    private void ReleasePlayerProtection()
    {
        if (!hasAcquiredPlayerProtection)
            return;

        playerProtection?.Release(this);
        hasAcquiredPlayerProtection = false;
    }

    private void AcquireTargetabilityBlock()
    {
        Transform resolvedPlayer = ResolvePlayerTransform();
        if (resolvedPlayer == null)
            return;

        targetabilityBlocker = PlayerTargetabilityBlocker.GetOrAdd(resolvedPlayer);
        targetabilityBlocker?.Acquire(this);
        hasAcquiredTargetabilityBlock = true;
    }

    private void ReleaseTargetabilityBlock()
    {
        if (!hasAcquiredTargetabilityBlock)
            return;

        targetabilityBlocker?.Release(this);
        hasAcquiredTargetabilityBlock = false;
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

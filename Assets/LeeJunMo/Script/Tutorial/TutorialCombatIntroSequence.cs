using System.Collections;
using Cainos.PixelArtTopDown_Basic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class TutorialCombatIntroSequence : MonoBehaviour
{
    [Header("Playback")]
    [SerializeField] private bool playOnlyOnce = true;
    [SerializeField] private TutorialDoorClosedTrigger doorClosedTrigger;

    [Header("Player")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private bool lockPlayerControls = true;
    [SerializeField] private bool blockPlayerTargetability = true;

    [Header("Camera")]
    [SerializeField] private Transform focusTarget;
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
    [SerializeField] private bool fadeGlobalUiDuringLetterbox;
    [SerializeField, Range(0f, 1f)] private float uiTargetAlpha = 0f;
    [SerializeField] private bool useCustomFadedLayers;
    [SerializeField] private GlobalCanvasLayer[] fadedLayers;

    [Header("Prompt")]
    [SerializeField] private TutorialInfoTrigger attackSkillTutorialTrigger;
    [SerializeField] private TutorialInfoPanel infoPanel;
    [SerializeField] private bool waitForPromptClose = true;
    [SerializeField, Min(0f)] private float promptOpenGraceSeconds = 0.1f;

    [Header("Events")]
    [SerializeField] private UnityEvent onSequenceStarted = new();
    [SerializeField] private UnityEvent onCameraFocused = new();
    [SerializeField] private UnityEvent onPromptRequested = new();
    [SerializeField] private UnityEvent onGameplayReleased = new();
    [SerializeField] private UnityEvent onSequenceCompleted = new();
    [SerializeField] private UnityEvent onSequenceCanceled = new();

    private Coroutine sequenceRoutine;
    private CinematicLetterboxOverlay letterboxOverlay;
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
    private bool hasPlayed;
    private bool hasAcquiredPlayerProtection;
    private bool hasAcquiredTargetabilityBlock;

    public bool IsRunning => sequenceRoutine != null;
    public bool HasPlayed => hasPlayed;
    public UnityEvent OnSequenceStarted => onSequenceStarted;
    public UnityEvent OnCameraFocused => onCameraFocused;
    public UnityEvent OnPromptRequested => onPromptRequested;
    public UnityEvent OnGameplayReleased => onGameplayReleased;
    public UnityEvent OnSequenceCompleted => onSequenceCompleted;
    public UnityEvent OnSequenceCanceled => onSequenceCanceled;

    private void OnEnable()
    {
        if (doorClosedTrigger != null)
            doorClosedTrigger.OnDoorClosed.AddListener(BeginSequence);
    }

    private void OnDisable()
    {
        if (doorClosedTrigger != null)
            doorClosedTrigger.OnDoorClosed.RemoveListener(BeginSequence);

        CancelSequence(invokeCanceled: false);
    }

    public void Begin()
    {
        BeginSequence();
    }

    public void BeginSequence()
    {
        if (!isActiveAndEnabled || sequenceRoutine != null)
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
        onSequenceStarted?.Invoke();

        if (useLetterbox)
        {
            letterboxOverlay = new CinematicLetterboxOverlay();
            yield return PlayLetterboxInRoutine();
        }

        yield return FocusCameraRoutine();
        onCameraFocused?.Invoke();

        RequestPrompt();
        if (waitForPromptClose)
            yield return WaitForPromptCloseRoutine();

        yield return ReturnCameraRoutine();

        ReleaseSequenceState(invokeGameplayReleased: true);
        hasPlayed = true;

        if (useLetterbox)
            yield return PlayLetterboxOutRoutine();

        sequenceRoutine = null;
        onSequenceCompleted?.Invoke();
    }

    private IEnumerator PlayLetterboxInRoutine()
    {
        if (letterboxOverlay == null)
            yield break;

        if (!fadeGlobalUiDuringLetterbox)
        {
            yield return letterboxOverlay.PlayIn(
                letterboxInSeconds,
                letterboxScreenHeightRatio,
                uiTargetAlpha: 1f,
                captureGlobalUiLayers: false);
            yield break;
        }

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

    private IEnumerator PlayLetterboxOutRoutine()
    {
        if (letterboxOverlay == null)
            yield break;

        yield return letterboxOverlay.PlayOut(letterboxOutSeconds);
        DisposeLetterboxOverlay();
    }

    private void RequestPrompt()
    {
        attackSkillTutorialTrigger?.FireNow();
        onPromptRequested?.Invoke();
    }

    private IEnumerator WaitForPromptCloseRoutine()
    {
        TutorialInfoPanel panel = ResolveInfoPanel();
        if (panel == null)
            yield break;

        float elapsed = 0f;
        while (!panel.IsOpen && elapsed < promptOpenGraceSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        while (panel != null && panel.IsOpen)
            yield return null;
    }

    private IEnumerator FocusCameraRoutine()
    {
        Transform target = focusTarget != null ? focusTarget : transform;
        CacheCameraState();
        SetCameraTarget(target);
        yield return ZoomCameraWhileWaitingRoutine(focusOrthographicSize, cameraZoomInSeconds, cameraFocusWaitSeconds);
    }

    private IEnumerator ReturnCameraRoutine()
    {
        Transform restoreTarget = ResolvePlayerTransform();
        SetCameraTarget(restoreTarget);

        float restoreOrthographicSize = hasCachedCameraLens
            ? cachedCameraOrthographicSize
            : focusOrthographicSize;

        yield return ZoomCameraWhileWaitingRoutine(restoreOrthographicSize, cameraZoomOutSeconds, cameraReturnWaitSeconds);
        RestoreCameraState(restoreTarget);
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

    private static IEnumerator WaitForPresentationSeconds(float seconds)
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

    private void AcquireSequenceState()
    {
        if (lockPlayerControls)
            AcquirePlayerProtection();

        if (blockPlayerTargetability)
            AcquireTargetabilityBlock();
    }

    private void ReleaseSequenceState(bool invokeGameplayReleased)
    {
        ReleaseTargetabilityBlock();
        ReleasePlayerProtection();

        if (invokeGameplayReleased)
            onGameplayReleased?.Invoke();
    }

    private void CancelSequence(bool invokeCanceled)
    {
        bool wasRunning = sequenceRoutine != null;
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        RestoreCameraState(ResolvePlayerTransform());
        ReleaseSequenceState(invokeGameplayReleased: false);
        DisposeLetterboxOverlay();

        if (invokeCanceled && wasRunning)
            onSequenceCanceled?.Invoke();
    }

    private void DisposeLetterboxOverlay()
    {
        if (letterboxOverlay == null)
            return;

        letterboxOverlay.Dispose();
        letterboxOverlay = null;
    }

    private Transform ResolvePlayerTransform()
    {
        if (playerTransform == null)
            playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();

        return playerTransform;
    }

    private TutorialInfoPanel ResolveInfoPanel()
    {
        if (infoPanel != null)
            return infoPanel;

#if UNITY_2023_1_OR_NEWER
        infoPanel = FindAnyObjectByType<TutorialInfoPanel>(FindObjectsInactive.Include);
#else
        infoPanel = FindObjectOfType<TutorialInfoPanel>(true);
#endif
        return infoPanel;
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
        if (targetabilityBlocker == null)
            return;

        targetabilityBlocker.Acquire(this);
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

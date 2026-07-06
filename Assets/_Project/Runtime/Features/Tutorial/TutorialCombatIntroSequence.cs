using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
/// <summary>
/// 책임 : 전투 튜토리얼 시작 컷씬의 카메라, 대사, 입력 잠금, 레터박스 흐름을 조율한다.
/// </summary>
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
    [SerializeField] private MonoBehaviour infoPanel;
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
    private ICinematicLetterboxOverlayHandle letterboxOverlay;
    private GameFlowInputBlocker inputBlocker;
    private PlayerCinematicProtection playerProtection;
    private PlayerTargetabilityBlocker targetabilityBlocker;
    private IGameplayCameraFocusSession cameraFocusSession;
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
            letterboxOverlay = CinematicLetterboxPlayback.CreateOverlay();
            yield return PlayLetterboxInRoutine();
        }

        yield return FocusCameraRoutine();
        onCameraFocused?.Invoke();

        RequestPrompt();
        if (waitForPromptClose)
            yield return WaitForPromptCloseRoutine();

        yield return ReturnCameraRoutine();

        if (useLetterbox)
            yield return PlayLetterboxOutRoutine();

        ReleaseSequenceState(invokeGameplayReleased: true);
        hasPlayed = true;

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
        ITutorialInfoPanel panel = ResolveInfoPanel();
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
        yield return ZoomCameraWhileWaitingRoutine(
            focusOrthographicSize,
            cameraZoomInSeconds,
            cameraFocusWaitSeconds,
            target);
    }

    private IEnumerator ReturnCameraRoutine()
    {
        Transform restoreTarget = ResolvePlayerTransform();
        SetCameraTarget(restoreTarget);

        float restoreOrthographicSize = cameraFocusSession != null && cameraFocusSession.HasOrthographicSize
            ? cameraFocusSession.CachedOrthographicSize
            : focusOrthographicSize;

        yield return ZoomCameraWhileWaitingRoutine(
            restoreOrthographicSize,
            cameraZoomOutSeconds,
            cameraReturnWaitSeconds,
            restoreTarget);
        RestoreCameraState(restoreTarget);
    }

    private IEnumerator ZoomCameraWhileWaitingRoutine(
        float targetOrthographicSize,
        float zoomDuration,
        float minimumWaitSeconds,
        Transform settleTarget)
    {
        float waitDuration = Mathf.Max(0f, minimumWaitSeconds);
        if (!zoomGameplayCameraDuringFocus ||
            cameraFocusSession == null ||
            !cameraFocusSession.HasOrthographicSize)
        {
            yield return WaitForPresentationSeconds(waitDuration);
            if (cameraFocusSession != null)
                yield return cameraFocusSession.WaitForSettle(settleTarget);
            yield break;
        }

        float startOrthographicSize = cameraFocusSession.CurrentOrthographicSize;
        float clampedTargetSize = Mathf.Max(0.01f, targetOrthographicSize);
        float clampedZoomDuration = Mathf.Max(0f, zoomDuration);
        float totalDuration = Mathf.Max(waitDuration, clampedZoomDuration);

        if (totalDuration <= 0f)
        {
            cameraFocusSession.SetOrthographicSize(clampedTargetSize);
            yield return cameraFocusSession.WaitForSettle(settleTarget);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (clampedZoomDuration > 0f)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / clampedZoomDuration));
                cameraFocusSession.SetOrthographicSize(
                    Mathf.Lerp(startOrthographicSize, clampedTargetSize, t));
            }

            yield return null;
        }

        cameraFocusSession.SetOrthographicSize(clampedTargetSize);
        yield return cameraFocusSession.WaitForSettle(settleTarget);
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
        AcquireInputBlocker();

        if (lockPlayerControls)
            AcquirePlayerProtection();

        if (blockPlayerTargetability)
            AcquireTargetabilityBlock();
    }

    private void ReleaseSequenceState(bool invokeGameplayReleased)
    {
        ReleaseTargetabilityBlock();
        ReleasePlayerProtection();
        ReleaseInputBlocker();

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

        if (playerTransform == null && PlayerInteractor2D.Instance != null)
            playerTransform = PlayerInteractor2D.Instance.transform;

        return playerTransform;
    }

    private ITutorialInfoPanel ResolveInfoPanel()
    {
        if (TryGetInfoPanel(infoPanel, out ITutorialInfoPanel panel))
            return panel;

        MonoBehaviour foundPanel = FindInfoPanelBehaviour();
        if (!TryGetInfoPanel(foundPanel, out panel))
            return null;

        infoPanel = foundPanel;
        return panel;
    }

    private static bool TryGetInfoPanel(MonoBehaviour source, out ITutorialInfoPanel panel)
    {
        panel = source as ITutorialInfoPanel;
        return panel != null;
    }

    private static MonoBehaviour FindInfoPanelBehaviour()
    {
#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
#endif
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is ITutorialInfoPanel)
                return behaviour;
        }

        return null;
    }

    private void AcquireInputBlocker()
    {
        if (inputBlocker != null && inputBlocker.IsBlocking)
            return;

        inputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        inputBlocker?.Acquire();
    }

    private void ReleaseInputBlocker()
    {
        inputBlocker?.Release();
        inputBlocker = null;
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
        if (cameraFocusSession != null)
            return;

        cameraFocusSession = GameplayCameraFocusPlayback.Capture(this);
    }

    private void SetCameraTarget(Transform target)
    {
        cameraFocusSession?.SetTarget(target);
    }

    private void RestoreCameraState(Transform preferredTarget)
    {
        if (cameraFocusSession == null)
            return;

        cameraFocusSession.Restore(preferredTarget);
        cameraFocusSession = null;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
/// <summary>
/// 책임 : 허브 인트로 컷씬에서 포커스 단계, 대사, 입력 잠금, UI 레터박스 흐름을 순차 실행한다.
/// </summary>
public sealed class HubIntroAfterDarkLordSequence : MonoBehaviour
{
    [System.Serializable]
    public sealed class HubIntroFocusStep
    {
        [SerializeField] private string label;
        [SerializeField] private Transform focusTarget;
        [SerializeField] private TextAsset dialogueInk;
        [SerializeField] private string dialogueStartPath;
        [SerializeField, Min(0f)] private float focusWaitSeconds = 0.35f;
        [SerializeField] private bool overrideOrthographicSize;
        [SerializeField, Min(0.01f)] private float orthographicSize = 4f;

        public HubIntroFocusStep()
        {
            focusWaitSeconds = 0.35f;
            orthographicSize = 4f;
        }

        public HubIntroFocusStep(string label) : this()
        {
            this.label = label;
        }

        public string Label => label;
        public Transform FocusTarget => focusTarget;
        public TextAsset DialogueInk => dialogueInk;
        public string DialogueStartPath => dialogueStartPath;
        public float FocusWaitSeconds => Mathf.Max(0f, focusWaitSeconds);
        public bool OverrideOrthographicSize => overrideOrthographicSize;
        public float OrthographicSize => Mathf.Max(0.01f, orthographicSize);
    }

    private const string DefaultHubSceneName = "ProtoTypeHub";

    private static readonly GlobalCanvasLayer[] PresentationFadedLayers =
    {
        GlobalCanvasLayer.Popup,
        GlobalCanvasLayer.Hover,
        GlobalCanvasLayer.Prompt,
        GlobalCanvasLayer.Reward,
        GlobalCanvasLayer.DamagePopup,
        GlobalCanvasLayer.BossHUD,
    };

    [Header("Playback")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool playOnlyOncePerScene = true;
    [SerializeField] private string hubSceneName = DefaultHubSceneName;
    [SerializeField] private bool waitForHubSpawnPresentation = true;
    [SerializeField, Min(0f)] private float playerWaitWarningSeconds = 2f;

    [Header("Gate")]
    [SerializeField] private string darkLordTutorialCompletionId = HubIntroProgressGate.DefaultDarkLordTutorialCompletionId;
    [SerializeField] private string hubIntroSeenId = HubIntroProgressGate.DefaultHubIntroSeenId;
    [SerializeField] private bool allowEditorBypassTutorialCompletion;
    [SerializeField] private bool markSeenOnComplete = true;

    [Header("HUD And Control")]
    [SerializeField] private bool hideGameplayHud = true;
    [SerializeField] private bool blockExternalInput = true;
    [SerializeField] private bool lockPlayerControls = true;

    [Header("Letterbox")]
    [SerializeField] private bool useLetterbox = true;
    [SerializeField, Min(0f)] private float letterboxInSeconds = 0.35f;
    [SerializeField, Min(0f)] private float letterboxOutSeconds = 0.25f;
    [SerializeField, Range(0f, 0.45f)] private float letterboxScreenHeightRatio = 0.14f;
    [SerializeField] private bool fadeGlobalUiDuringLetterbox = true;
    [SerializeField, Range(0f, 1f)] private float uiTargetAlpha = 0f;

    [Header("NPC")]
    [SerializeField] private MonoBehaviour npcSpeechBubble;
    [SerializeField] private Transform npcFocusTarget;
    [SerializeField] private NPCData narratorNpcData;
    [SerializeField] private string openingSpeechText;
    [SerializeField, Min(0.05f)] private float openingSpeechDuration = 2.5f;
    [SerializeField] private SpeechBubbleThemeSettings openingSpeechTheme;
    [SerializeField, Min(0f)] private float speechBubbleFallbackPaddingSeconds = 1f;

    [Header("Opening Speech Bubble Layout")]
    [SerializeField] private bool preSizeOpeningSpeechBubbleBeforeTyping = true;
    [SerializeField, Min(1f)] private float openingSpeechBubbleMinTextWidth = 160f;
    [SerializeField, Min(1f)] private float openingSpeechBubbleMaxTextWidth = 360f;
    [SerializeField, Min(1f)] private float openingSpeechBubbleMinTextHeight = 32f;

    [Header("Focus Dialogue")]
    [SerializeField] private HubIntroFocusStep[] focusSteps =
    {
        new("Junk"),
        new("Training Dummy"),
        new("Gate"),
    };

    [Header("Final Dialogue")]
    [SerializeField] private Transform finalNpcFocusTarget;
    [SerializeField] private TextAsset finalDialogueInk;
    [SerializeField] private string finalDialogueStartPath;
    [SerializeField, Min(0f)] private float finalNpcFocusWaitSeconds = 0.35f;
    [SerializeField] private bool finalDialogueUsesFastSilhouette = true;
    [SerializeField, Min(0f)] private float finalSilhouetteFadeSeconds = 0.25f;
    [SerializeField] private string finalSilhouettePosition = "center";
    [SerializeField] private bool finalSilhouetteColorize;
    [SerializeField] private bool finalDialogueBoxOnly = true;

    [Header("Camera")]
    [SerializeField, Min(0f)] private float npcFocusWaitSeconds = 0.35f;
    [SerializeField] private bool zoomGameplayCameraDuringFocus = true;
    [SerializeField, Min(0.01f)] private float defaultFocusOrthographicSize = 4f;
    [SerializeField, Min(0f)] private float cameraZoomInSeconds = 0.35f;
    [SerializeField, Min(0f)] private float cameraZoomOutSeconds = 0.25f;
    [SerializeField, Min(0f)] private float cameraReturnWaitSeconds = 0.35f;

    [Header("Events")]
    [SerializeField] private UnityEvent onSequenceStarted = new();
    [SerializeField] private UnityEvent onSequenceCompleted = new();
    [SerializeField] private UnityEvent onSequenceCanceled = new();

    private Coroutine sequenceRoutine;
    private GameFlowInputBlocker inputBlocker;
    private ICinematicLetterboxOverlayHandle letterboxOverlay;
    private PlayerCinematicProtection playerProtection;
    private Transform protectedPlayerTransform;
    private Canvas gameplayHudCanvas;
    private IGameplayCameraFocusSession cameraFocusSession;
    private bool hasCachedGameplayHudState;
    private bool cachedGameplayHudEnabled;
    private bool hasAcquiredPlayerProtection;
    private bool hasPlayedThisScene;
    private bool hasDialoguePlaybackFailure;

    public bool IsRunning => sequenceRoutine != null;
    public bool HasPlayedThisScene => hasPlayedThisScene;
    public UnityEvent OnSequenceStarted => onSequenceStarted;
    public UnityEvent OnSequenceCompleted => onSequenceCompleted;
    public UnityEvent OnSequenceCanceled => onSequenceCanceled;

    private void Start()
    {
        if (playOnStart)
            BeginSequence();
    }

    private void OnDisable()
    {
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

        if (playOnlyOncePerScene && hasPlayedThisScene)
            return;

        if (!IsHubScene())
            return;

        if (EditorDirectSceneStartContext.IsDirectHubStart(gameObject.scene))
            return;

        if (!HubIntroProgressGate.ShouldPlayAfterDarkLordTutorial(
                darkLordTutorialCompletionId,
                hubIntroSeenId,
                allowEditorBypassTutorialCompletion))
        {
            return;
        }

        sequenceRoutine = StartCoroutine(SequenceRoutine());
    }

    public void CancelSequence()
    {
        CancelSequence(invokeCanceled: true);
    }

    private IEnumerator SequenceRoutine()
    {
        hasDialoguePlaybackFailure = false;
        AcquireGlobalPresentationState();
        onSequenceStarted?.Invoke();

        yield return WaitForPlayerTransformRoutine();

        if (waitForHubSpawnPresentation)
            yield return WaitForHubSpawnPresentationRoutine();

        AcquirePlayerProtection();

        if (useLetterbox)
        {
            letterboxOverlay = CinematicLetterboxPlayback.CreateOverlay();
            yield return PlayLetterboxInRoutine();
        }

        yield return FocusCameraRoutine(ResolveNpcFocusTarget(), defaultFocusOrthographicSize, npcFocusWaitSeconds);
        yield return PlayOpeningSpeechRoutine();
        yield return PlayLetterboxOutRoutine();

        if (focusSteps != null)
        {
            for (int i = 0; i < focusSteps.Length; i++)
                yield return PlayFocusStepRoutine(focusSteps[i]);
        }

        Transform finalFocusTarget = finalNpcFocusTarget != null ? finalNpcFocusTarget : ResolveNpcFocusTarget();
        yield return FocusCameraRoutine(finalFocusTarget, defaultFocusOrthographicSize, finalNpcFocusWaitSeconds);
        yield return PlayFinalDialogueRoutine();

        yield return ReturnCameraRoutine();

        if (markSeenOnComplete && !hasDialoguePlaybackFailure)
        {
            HubIntroProgressGate.MarkHubIntroSeen(hubIntroSeenId);
            PresentationPreloadPlayback.RefreshFirstRunIntroWindow("Hub intro seen");
        }
        else if (markSeenOnComplete)
        {
            Debug.LogError(
                "[HubIntroAfterDarkLordSequence] Hub intro was not marked as seen because one or more dialogues failed to play.",
                this);
        }

        hasPlayedThisScene = true;
        sequenceRoutine = null;
        CleanupSequenceState();
        onSequenceCompleted?.Invoke();
    }

    private IEnumerator PlayFocusStepRoutine(HubIntroFocusStep step)
    {
        if (step == null)
            yield break;

        Transform target = step.FocusTarget != null ? step.FocusTarget : transform;
        float targetSize = step.OverrideOrthographicSize
            ? step.OrthographicSize
            : defaultFocusOrthographicSize;

        yield return FocusCameraRoutine(target, targetSize, step.FocusWaitSeconds);
        yield return PlayDialogueRoutine(
            step.DialogueInk,
            step.DialogueStartPath,
            DialoguePresentationOptions.WithoutPortraits,
            step.Label);
    }

    private IEnumerator PlayFinalDialogueRoutine()
    {
        DialoguePresentationOptions options = finalDialogueUsesFastSilhouette
            ? DialoguePresentationOptions.FastSilhouette(
                finalSilhouetteFadeSeconds,
                finalSilhouettePosition,
                finalSilhouetteColorize,
                finalDialogueBoxOnly)
            : DialoguePresentationOptions.Default;

        yield return PlayDialogueRoutine(finalDialogueInk, finalDialogueStartPath, options, "final");
    }

    private IEnumerator PlayDialogueRoutine(
        TextAsset inkJson,
        string startPath,
        DialoguePresentationOptions presentationOptions,
        string label)
    {
        if (inkJson == null)
        {
            ReportDialoguePlaybackFailure(label, "Ink JSON is missing.");
            yield break;
        }

        if (narratorNpcData == null)
        {
            ReportDialoguePlaybackFailure(label, "Narrator NPCData is missing.");
            yield break;
        }

        if (!DialoguePlayback.IsAvailable)
        {
            ReportDialoguePlaybackFailure(label, "Dialogue playback backend was not found.");
            yield break;
        }

        List<DialogueStorySegment> segments = new()
        {
            new DialogueStorySegment(inkJson, startPath)
        };
        List<NPCData> participants = new() { narratorNpcData };

        if (!DialoguePlayback.TryStartDialogueSequence(segments, participants, null, presentationOptions))
        {
            ReportDialoguePlaybackFailure(label, "Dialogue playback request was rejected.");
            yield break;
        }

        if (!DialoguePlayback.IsPlaying)
        {
            ReportDialoguePlaybackFailure(label, "Dialogue request was accepted, but playback did not start.");
            yield break;
        }

        yield return new WaitUntil(() => !DialoguePlayback.IsPlaying);
    }

    private void ReportDialoguePlaybackFailure(string label, string reason)
    {
        hasDialoguePlaybackFailure = true;
        Debug.LogError($"[HubIntroAfterDarkLordSequence] {label} dialogue failed. {reason}", this);
    }

    private IEnumerator PlayOpeningSpeechRoutine()
    {
        ISpeechBubblePlayback bubblePlayback = ResolveNpcSpeechBubble();
        if (bubblePlayback == null || string.IsNullOrWhiteSpace(openingSpeechText))
            yield break;

        bool hidden = false;
        float duration = Mathf.Max(0.05f, openingSpeechDuration);
        SpeakOpeningSpeech(duration, () => hidden = true);

        float elapsed = 0f;
        float fallbackSeconds = duration + Mathf.Max(0f, speechBubbleFallbackPaddingSeconds);
        while (!hidden && elapsed < fallbackSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SpeakOpeningSpeech(float duration, System.Action onHidden)
    {
        ISpeechBubblePlayback bubblePlayback = ResolveNpcSpeechBubble();
        if (bubblePlayback == null)
            return;

        if (preSizeOpeningSpeechBubbleBeforeTyping)
        {
            float minWidth = Mathf.Max(1f, openingSpeechBubbleMinTextWidth);
            float maxWidth = Mathf.Max(minWidth, openingSpeechBubbleMaxTextWidth);
            float minHeight = Mathf.Max(1f, openingSpeechBubbleMinTextHeight);
            bubblePlayback.SpeakWithPreSizedLayout(
                openingSpeechText,
                duration,
                openingSpeechTheme,
                onHidden,
                minWidth,
                maxWidth,
                minHeight);
            return;
        }

        bubblePlayback.Speak(openingSpeechText, duration, openingSpeechTheme, onHidden);
    }

    private IEnumerator PlayLetterboxInRoutine()
    {
        if (letterboxOverlay == null)
            yield break;

        if (fadeGlobalUiDuringLetterbox)
        {
            yield return letterboxOverlay.PlayIn(
                letterboxInSeconds,
                letterboxScreenHeightRatio,
                uiTargetAlpha,
                PresentationFadedLayers);
            yield break;
        }

        yield return letterboxOverlay.PlayIn(
            letterboxInSeconds,
            letterboxScreenHeightRatio,
            uiTargetAlpha: 1f,
            captureGlobalUiLayers: false);
    }

    private IEnumerator PlayLetterboxOutRoutine()
    {
        if (letterboxOverlay == null)
            yield break;

        yield return letterboxOverlay.PlayOut(letterboxOutSeconds);
        DisposeLetterboxOverlay();
    }

    private IEnumerator FocusCameraRoutine(Transform target, float orthographicSize, float waitSeconds)
    {
        Transform focusTarget = target != null ? target : transform;
        CacheCameraState();
        SetCameraTarget(focusTarget);
        yield return ZoomCameraWhileWaitingRoutine(
            orthographicSize,
            cameraZoomInSeconds,
            waitSeconds,
            focusTarget);
    }

    private IEnumerator ReturnCameraRoutine()
    {
        if (cameraFocusSession == null)
            yield break;

        Transform restoreTarget = ResolvePlayerTransform();
        SetCameraTarget(restoreTarget);

        float restoreOrthographicSize = cameraFocusSession.HasOrthographicSize
            ? cameraFocusSession.CachedOrthographicSize
            : defaultFocusOrthographicSize;

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

    private IEnumerator WaitForPlayerTransformRoutine()
    {
        if (ResolvePlayerTransform() != null)
            yield break;

        float elapsed = 0f;
        bool warned = false;
        while (ResolvePlayerTransform() == null)
        {
            elapsed += Time.unscaledDeltaTime;
            if (!warned && elapsed >= playerWaitWarningSeconds)
            {
                Debug.LogWarning(
                    "[HubIntroAfterDarkLordSequence] Waiting for player registration before starting Hub intro.",
                    this);
                warned = true;
            }

            yield return null;
        }
    }

    private IEnumerator WaitForHubSpawnPresentationRoutine()
    {
        yield return null;

        Transform playerTransform = ResolvePlayerTransform();
        PlayerHubSpawnPresentation2D spawnPresentation = playerTransform != null
            ? playerTransform.GetComponent<PlayerHubSpawnPresentation2D>()
            : null;

        while (spawnPresentation != null && spawnPresentation.IsPlaying)
            yield return null;
    }

    private void AcquireGlobalPresentationState()
    {
        CacheAndHideGameplayHud();
        UiCommandPlayback.HideWorldPrompt();
        UiCommandPlayback.HideHoverImmediate();

        if (!blockExternalInput)
            return;

        inputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        inputBlocker?.Acquire();
    }

    private void CleanupSequenceState()
    {
        ResolveNpcSpeechBubble()?.HideActive();
        DisposeLetterboxOverlay();
        RestoreCameraState(ResolvePlayerTransform());
        ReleasePlayerProtection();

        inputBlocker?.Release();
        inputBlocker = null;

        RestoreGameplayHud();
    }

    private void CancelSequence(bool invokeCanceled)
    {
        bool wasRunning = sequenceRoutine != null;
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        CleanupSequenceState();

        if (invokeCanceled && wasRunning)
            onSequenceCanceled?.Invoke();
    }

    private void CacheAndHideGameplayHud()
    {
        if (!hideGameplayHud || hasCachedGameplayHudState)
            return;

        gameplayHudCanvas = GlobalCanvasPlayback.GetCanvas(GlobalCanvasLayer.GameplayHUD);
        if (gameplayHudCanvas == null)
            return;

        cachedGameplayHudEnabled = gameplayHudCanvas.enabled;
        gameplayHudCanvas.enabled = false;
        hasCachedGameplayHudState = true;
    }

    private void RestoreGameplayHud()
    {
        if (!hasCachedGameplayHudState)
            return;

        if (gameplayHudCanvas != null)
            gameplayHudCanvas.enabled = cachedGameplayHudEnabled;

        gameplayHudCanvas = null;
        hasCachedGameplayHudState = false;
    }

    private void DisposeLetterboxOverlay()
    {
        if (letterboxOverlay == null)
            return;

        letterboxOverlay.Dispose();
        letterboxOverlay = null;
    }

    private Transform ResolveNpcFocusTarget()
    {
        if (npcFocusTarget != null)
            return npcFocusTarget;

        ISpeechBubblePlayback bubblePlayback = ResolveNpcSpeechBubble();
        if (bubblePlayback != null)
            return bubblePlayback.BubbleTransform;

        return transform;
    }

    private ISpeechBubblePlayback ResolveNpcSpeechBubble()
    {
        if (npcSpeechBubble is ISpeechBubblePlayback existing)
            return existing;

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is ISpeechBubblePlayback playback)
            {
                npcSpeechBubble = behaviour;
                return playback;
            }
        }

        npcSpeechBubble = null;
        return null;
    }

    private Transform ResolvePlayerTransform()
    {
        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        if (playerTransform == null && PlayerInteractor2D.Instance != null)
            playerTransform = PlayerInteractor2D.Instance.transform;

        return playerTransform;
    }

    private void AcquirePlayerProtection()
    {
        if (!lockPlayerControls)
            return;

        Transform resolvedPlayer = ResolvePlayerTransform();
        if (resolvedPlayer == null)
            return;

        if (hasAcquiredPlayerProtection && protectedPlayerTransform == resolvedPlayer)
            return;

        if (hasAcquiredPlayerProtection)
            ReleasePlayerProtection();

        playerProtection = resolvedPlayer.GetComponent<PlayerCinematicProtection>();
        if (playerProtection == null)
            playerProtection = resolvedPlayer.gameObject.AddComponent<PlayerCinematicProtection>();

        playerProtection.Acquire(this);
        protectedPlayerTransform = resolvedPlayer;
        hasAcquiredPlayerProtection = true;
    }

    private void ReleasePlayerProtection()
    {
        if (!hasAcquiredPlayerProtection)
            return;

        playerProtection?.Release(this);
        playerProtection = null;
        protectedPlayerTransform = null;
        hasAcquiredPlayerProtection = false;
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

    private bool IsHubScene()
    {
        Scene ownerScene = gameObject.scene;
        if (!ownerScene.IsValid())
            ownerScene = SceneManager.GetActiveScene();

        return string.Equals(ownerScene.name, hubSceneName, System.StringComparison.Ordinal);
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

}

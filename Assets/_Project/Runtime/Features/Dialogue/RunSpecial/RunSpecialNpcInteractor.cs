using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
/// <summary>
/// 책임:
/// - 런 특수 NPC의 상호작용, 선택지 표시, 기능 실행 전후 컷씬 프레젠테이션을 조율한다.
/// - 씬 전환으로 전역 선택지 UI가 교체되면 파괴 참조를 버리고 현재 Presenter에 다시 연결한다.
/// </summary>
public sealed class RunSpecialNpcInteractor : InteractableBase
{
    [Header("Prompt")]
    [SerializeField] private Transform promptAnchor;
    [SerializeField] private string interactPromptText = "Talk";

    [Header("Speech")]
    [SerializeField] private MonoBehaviour speechBubble;
    [SerializeField] private RunSpecialNpcDialogueSetSO dialogueSet;
    [SerializeField] private RunSpecialNpcFeatureBase primaryFeature;

    [Header("Choices")]
    [SerializeField] private MonoBehaviour choicePresenter;
    [SerializeField] private bool executeSingleChoiceWithoutPresenter;
    [SerializeField, Min(0f)] private float choiceInputGuardSeconds = 0.15f;
    [SerializeField] private bool allowNumberKeySelection = true;

    [Header("Legacy Migration Source")]
    [SerializeField, HideInInspector] private RunSpecialNpcLine[] openingLines = System.Array.Empty<RunSpecialNpcLine>();
    [SerializeField, HideInInspector] private RunSpecialNpcLine[] noAvailableChoiceLines = System.Array.Empty<RunSpecialNpcLine>();
    [SerializeField, HideInInspector] private RunSpecialNpcChoice[] choices = System.Array.Empty<RunSpecialNpcChoice>();

    [Header("Flow")]
    [SerializeField] private bool blockExternalInput = true;
    [SerializeField] private bool pauseRunTimer = true;
    [SerializeField] private bool pauseTimeScale = true;
    [SerializeField] private bool setPlayerTalkingState = true;
    [SerializeField] private bool allowLineSkip = true;
    [SerializeField, Min(0f)] private float lineSkipInputGuardSeconds = 0.12f;

    [Header("Speech Bubble Layout")]
    [SerializeField] private bool preSizeSpeechBubbleBeforeTyping = true;
    [SerializeField, Min(1f)] private float speechBubbleMinTextWidth = 160f;
    [SerializeField, Min(1f)] private float speechBubbleMaxTextWidth = 360f;
    [SerializeField, Min(1f)] private float speechBubbleMinTextHeight = 32f;

    [Header("Presentation")]
    [SerializeField] private bool showLetterbox = true;
    [SerializeField, Min(0f)] private float letterboxInDuration = 0.18f;
    [SerializeField, Min(0f)] private float letterboxOutDuration = 0.14f;
    [SerializeField, Range(0f, 0.45f)] private float letterboxScreenHeightRatio = 0.12f;
    [SerializeField] private bool fadeHudDuringPresentation = true;
    [SerializeField, Range(0f, 1f)] private float hudTargetAlpha = 0f;

    [Header("Camera Focus")]
    [SerializeField] private bool focusCameraOnNpc = true;
    [SerializeField] private Transform cameraFocusTarget;
    [SerializeField, Min(0f)] private float cameraFocusWaitSeconds = 0.35f;
    [SerializeField, Min(0f)] private float cameraReturnWaitSeconds = 0.25f;
    [SerializeField] private bool restoreCameraAfterDialogue = true;

    [Header("Highlight")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private readonly List<RunSpecialNpcChoiceDefinition> visibleChoices = new();
    private readonly List<string> visibleChoiceLabels = new();

    private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
    private static readonly GlobalCanvasLayer[] PresentationFadedLayers =
    {
        GlobalCanvasLayer.GameplayHUD,
        GlobalCanvasLayer.Popup,
        GlobalCanvasLayer.Hover,
        GlobalCanvasLayer.Prompt,
        GlobalCanvasLayer.Reward,
        GlobalCanvasLayer.DamagePopup,
        GlobalCanvasLayer.BossHUD,
    };

    private MaterialPropertyBlock propertyBlock;
    private Coroutine activeFlow;
    private IPlayerInteractor activePlayer;
    private IRunSpecialNpcChoicePresenter resolvedChoicePresenter;
    private IRunSpecialNpcChoiceAnchorFollower choiceAnchorFollower;
    private InteractState previousPlayerState = InteractState.Idle;
    private GameFlowInputBlocker inputBlocker;
    private ICinematicLetterboxOverlayHandle letterboxOverlay;
    private IGameplayCameraFocusSession cameraFocusSession;
    private bool isFlowActive;
    private bool holdsRunTimerPause;
    private bool holdsTimeScalePause;
    private RunSpecialNpcFeatureBase featureToExecuteAfterPresentationClose;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        ResolveSpeechBubble();

        ResolvePrimaryFeature();
        ResolveChoicePresenter();
        ResolveChoiceAnchorFollower();

        OnUnHighlight();
    }

    private void OnDisable()
    {
        StopActiveFlow();
        OnUnHighlight();
    }

    private void OnDestroy()
    {
        StopActiveFlow();
    }

    public override bool CanInteract(IPlayerInteractor player)
    {
        return player != null &&
               player.CurrentState == InteractState.Idle &&
               !isFlowActive &&
               ResolveSpeechBubble() != null;
    }

    public override void OnPlayerInteract(IPlayerInteractor player)
    {
        if (!CanInteract(player))
            return;

        activeFlow = StartCoroutine(RunFlow(player));
    }

    public override InteractState GetInteractType()
    {
        return InteractState.Talking;
    }

    public override string GetInteractDescription()
    {
        return interactPromptText;
    }

    public override Transform GetPromptAnchor()
    {
        return promptAnchor != null ? promptAnchor : transform;
    }

    public override void OnHighlight()
    {
        SetOutlineEnabled(true);
    }

    public override void OnUnHighlight()
    {
        SetOutlineEnabled(false);
    }

    private IEnumerator RunFlow(IPlayerInteractor player)
    {
        BeginFlowState(player);
        RunSpecialNpcFeatureContext context = new(this, player);
        featureToExecuteAfterPresentationClose = null;

        try
        {
            yield return PlayLetterboxIn();
            yield return PlayCameraFocusIn();
            yield return RunInteractionBody(context, player);
            if (featureToExecuteAfterPresentationClose != null)
                HideSpeechBubbleIfAlive();

            yield return PlayCameraReturn();
            yield return PlayLetterboxOut();
            yield return ExecuteFeatureAfterPresentationCloseIfNeeded(context);
        }
        finally
        {
            HideChoicePresenterIfAlive();
            ClearChoiceFollowTargetIfAlive();
            DisposeLetterboxOverlay();
            featureToExecuteAfterPresentationClose = null;
            EndFlowState();
            activeFlow = null;
        }
    }

    private IEnumerator RunInteractionBody(RunSpecialNpcFeatureContext context, IPlayerInteractor player)
    {
        RunSpecialNpcBranch branch = BuildCurrentBranch(context);
        ApplyBranchChoices(branch, context);
        ResolveChoicePresenter();
        ResolveChoiceAnchorFollower();

        yield return PlayLines(branch.Lines, context);

        if (branch.EndAfterLines || visibleChoices.Count == 0)
        {
            yield break;
        }

        int selectedIndex = -1;
        if (resolvedChoicePresenter == null)
        {
            if (!executeSingleChoiceWithoutPresenter || visibleChoices.Count != 1)
            {
                Debug.LogWarning("[RunSpecialNpcInteractor] Choice presenter is missing.", this);
                yield break;
            }

            selectedIndex = 0;
        }
        else
        {
            yield return PlayCameraReturn();

            ResolveChoicePresenter();
            ResolveChoiceAnchorFollower();
            IRunSpecialNpcChoicePresenter activePresenter = resolvedChoicePresenter;
            if (!IsUnityObjectAlive(activePresenter))
            {
                Debug.LogWarning(
                    "[RunSpecialNpcInteractor] Choice presenter was destroyed before choices could be shown.",
                    this);
                yield break;
            }

            if (IsUnityObjectAlive(choiceAnchorFollower))
                choiceAnchorFollower.SetFollowTarget(player?.Transform);

            activePresenter.Show(
                visibleChoiceLabels,
                index => selectedIndex = index,
                choiceInputGuardSeconds);

            while (selectedIndex < 0 && isFlowActive)
            {
                if (!IsUnityObjectAlive(activePresenter))
                {
                    ResolveChoicePresenter();
                    ResolveChoiceAnchorFollower();
                    activePresenter = resolvedChoicePresenter;
                    if (!IsUnityObjectAlive(activePresenter))
                    {
                        Debug.LogWarning(
                            "[RunSpecialNpcInteractor] Choice presenter was destroyed while choices were visible.",
                            this);
                        break;
                    }

                    if (IsUnityObjectAlive(choiceAnchorFollower))
                        choiceAnchorFollower.SetFollowTarget(player?.Transform);
                    activePresenter.Show(
                        visibleChoiceLabels,
                        index => selectedIndex = index,
                        choiceInputGuardSeconds);
                }

                if (allowNumberKeySelection && activePresenter.CanAcceptInput)
                    TryConfirmNumberKeyChoice(activePresenter, visibleChoices.Count);

                yield return null;
            }

            if (IsUnityObjectAlive(activePresenter))
                activePresenter.Hide();
        }

        if (selectedIndex < 0 || selectedIndex >= visibleChoices.Count)
            yield break;

        RunSpecialNpcChoiceDefinition selectedChoice = visibleChoices[selectedIndex];
        RunSpecialNpcFeatureBase feature = ResolveChoiceActionFeature(selectedChoice);
        if (feature != null && !feature.CanExecute(context))
        {
            if (HasAnyPlayableLine(selectedChoice.UnavailableResponseLines))
            {
                yield return PlayCameraFocusIn();
                yield return PlayLines(selectedChoice.UnavailableResponseLines, context);
            }

            string reason = feature.GetUnavailableReason(context);
            if (!string.IsNullOrWhiteSpace(reason))
                Debug.Log($"[RunSpecialNpcInteractor] Feature unavailable: {reason}", feature);

            yield break;
        }

        if (HasAnyPlayableLine(selectedChoice.ResponseLines))
            yield return PlayCameraFocusIn();

        yield return PlayLines(selectedChoice.ResponseLines, context);

        if (feature == null)
            yield break;

        if (feature.ExecuteAfterRunSpecialPresentationClose)
        {
            featureToExecuteAfterPresentationClose = feature;
            yield break;
        }

        yield return feature.Execute(context);
    }

    private IEnumerator ExecuteFeatureAfterPresentationCloseIfNeeded(RunSpecialNpcFeatureContext context)
    {
        RunSpecialNpcFeatureBase feature = featureToExecuteAfterPresentationClose;
        featureToExecuteAfterPresentationClose = null;
        if (feature == null)
            yield break;

        ResumeGameplayClockForFeatureExecution();
        yield return feature.Execute(context);
    }

    private RunSpecialNpcBranch BuildCurrentBranch(RunSpecialNpcFeatureContext context)
    {
        ResolvePrimaryFeature();

        if (dialogueSet == null)
        {
            Debug.LogWarning("[RunSpecialNpcInteractor] Dialogue set is missing.", this);
            return RunSpecialNpcBranch.Empty;
        }

        if (primaryFeature == null)
        {
            Debug.LogWarning("[RunSpecialNpcInteractor] Primary feature is missing.", this);
            return RunSpecialNpcBranch.Empty;
        }

        if (dialogueSet.FeatureKind != primaryFeature.DialogueFeatureKind)
        {
            Debug.LogWarning(
                $"[RunSpecialNpcInteractor] Dialogue set kind '{dialogueSet.FeatureKind}' does not match primary feature kind '{primaryFeature.DialogueFeatureKind}'.",
                this);
            return RunSpecialNpcBranch.Empty;
        }

        RunSpecialNpcDialogueBranchKey branchKey = primaryFeature.GetDialogueBranchKey(context);
        return RunSpecialNpcBranch.FromDefinition(dialogueSet.GetBranch(branchKey));
    }

    private void ApplyBranchChoices(RunSpecialNpcBranch branch, RunSpecialNpcFeatureContext context)
    {
        visibleChoices.Clear();
        visibleChoiceLabels.Clear();

        if (branch?.Choices == null)
            return;

        RunSpecialNpcChoiceDefinition[] branchChoices = branch.Choices;
        for (int i = 0; i < branchChoices.Length; i++)
        {
            RunSpecialNpcChoiceDefinition choice = branchChoices[i];
            if (choice == null || !choice.ShouldShow(primaryFeature, context))
                continue;

            visibleChoices.Add(choice);
            visibleChoiceLabels.Add(choice.Label);
        }
    }

    private IEnumerator PlayLines(RunSpecialNpcLine[] lines, RunSpecialNpcFeatureContext context)
    {
        ISpeechBubblePlayback bubblePlayback = ResolveSpeechBubble();
        if (lines == null || bubblePlayback == null)
            yield break;

        for (int i = 0; i < lines.Length; i++)
        {
            RunSpecialNpcLine line = lines[i];
            string lineText = ResolveLineText(line, context);
            if (string.IsNullOrWhiteSpace(lineText))
                continue;

            foreach (string lineSegment in EnumerateLineSegments(lineText))
            {
                if (!isFlowActive)
                    yield break;

                bool hidden = false;
                float duration = Mathf.Max(0.05f, line.Duration);
                SpeakLine(line, lineSegment, duration, () => hidden = true);

                float fallbackSeconds = duration + 1f;
                float elapsed = 0f;
                float skipInputReadyAt = Time.unscaledTime + lineSkipInputGuardSeconds;
                while (!hidden && elapsed < fallbackSeconds && isFlowActive)
                {
                    if (allowLineSkip &&
                        Time.unscaledTime >= skipInputReadyAt &&
                        WasLineSkipPressed())
                    {
                        if (bubblePlayback.TryAdvanceActive())
                            skipInputReadyAt = Time.unscaledTime + lineSkipInputGuardSeconds;
                    }

                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!isFlowActive)
                    yield break;
            }
        }
    }

    private void SpeakLine(RunSpecialNpcLine line, string lineText, float duration, System.Action onHidden)
    {
        ISpeechBubblePlayback bubblePlayback = ResolveSpeechBubble();
        if (bubblePlayback == null)
            return;

        if (preSizeSpeechBubbleBeforeTyping)
        {
            bubblePlayback.SpeakWithPreSizedLayout(
                lineText,
                duration,
                line.Theme,
                onHidden,
                speechBubbleMinTextWidth,
                speechBubbleMaxTextWidth,
                speechBubbleMinTextHeight);
            return;
        }

        bubblePlayback.Speak(lineText, duration, line.Theme, onHidden);
    }

    private string ResolveLineText(RunSpecialNpcLine line, RunSpecialNpcFeatureContext context)
    {
        if (line == null)
            return string.Empty;

        string text = line.Text;
        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (primaryFeature == null)
            return text;

        return primaryFeature.ResolveDialogueLineText(text, context);
    }

    private RunSpecialNpcFeatureBase ResolveChoiceActionFeature(RunSpecialNpcChoiceDefinition choice)
    {
        if (choice == null)
            return null;

        return choice.Action == RunSpecialNpcChoiceAction.ExecutePrimaryFeature
            ? primaryFeature
            : null;
    }

    private static bool HasAnyPlayableLine(RunSpecialNpcLine[] lines)
    {
        if (lines == null)
            return false;

        for (int i = 0; i < lines.Length; i++)
        {
            RunSpecialNpcLine line = lines[i];
            if (line != null && !string.IsNullOrWhiteSpace(line.Text))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> EnumerateLineSegments(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        string normalizedText = text.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] segments = normalizedText.Split('\n');
        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i].Trim();
            if (!string.IsNullOrWhiteSpace(segment))
                yield return segment;
        }
    }

    private void BeginFlowState(IPlayerInteractor player)
    {
        isFlowActive = true;
        activePlayer = player;
        previousPlayerState = player != null ? player.CurrentState : InteractState.Idle;

        UiCommandPlayback.HideWorldPrompt();

        if (setPlayerTalkingState && player != null)
            player.SetInteractState(InteractState.Talking);

        if (blockExternalInput)
        {
            inputBlocker = GameFlowInputBlocker.GetOrAdd(this);
            inputBlocker?.Acquire();
        }

        if (pauseRunTimer && RunTimeLimitSystem.Instance != null)
        {
            RunTimeLimitSystem.Instance.SetExternalPause(this, true);
            holdsRunTimerPause = true;
        }

        PauseTimeScaleIfNeeded();
    }

    private void EndFlowState()
    {
        if (!isFlowActive)
        {
            DisposeLetterboxOverlay();
            RestoreTimeScaleIfNeeded();
            RestoreCameraState(PlayerRuntimeRegistry.GetPlayerTransform());
            return;
        }

        isFlowActive = false;
        HideChoicePresenterIfAlive();
        ClearChoiceFollowTargetIfAlive();
        HideSpeechBubbleIfAlive();
        DisposeLetterboxOverlay();
        RestoreCameraState(PlayerRuntimeRegistry.GetPlayerTransform());

        if (holdsRunTimerPause && RunTimeLimitSystem.Instance != null)
            RunTimeLimitSystem.Instance.SetExternalPause(this, false);
        holdsRunTimerPause = false;

        RestoreTimeScaleIfNeeded();

        inputBlocker?.Release();

        if (setPlayerTalkingState && activePlayer != null)
            activePlayer.SetInteractState(NormalizeRestoredPlayerState(previousPlayerState));

        activePlayer = null;
        previousPlayerState = InteractState.Idle;
    }

    private void StopActiveFlow()
    {
        if (activeFlow != null)
        {
            Coroutine runningFlow = activeFlow;
            activeFlow = null;
            StopCoroutine(runningFlow);
        }

        EndFlowState();
    }

    private IEnumerator PlayLetterboxIn()
    {
        if (!showLetterbox)
            yield break;

        letterboxOverlay ??= CinematicLetterboxPlayback.CreateOverlay();
        if (fadeHudDuringPresentation)
        {
            yield return letterboxOverlay.PlayIn(
                letterboxInDuration,
                letterboxScreenHeightRatio,
                hudTargetAlpha,
                PresentationFadedLayers);
            yield break;
        }

        yield return letterboxOverlay.PlayIn(
            letterboxInDuration,
            letterboxScreenHeightRatio,
            uiTargetAlpha: 1f,
            captureGlobalUiLayers: false);
    }

    private IEnumerator PlayLetterboxOut()
    {
        if (letterboxOverlay == null)
            yield break;

        yield return letterboxOverlay.PlayOut(letterboxOutDuration);
        DisposeLetterboxOverlay();
    }

    private void DisposeLetterboxOverlay()
    {
        if (letterboxOverlay == null)
            return;

        letterboxOverlay.Dispose();
        letterboxOverlay = null;
    }

    private IEnumerator PlayCameraFocusIn()
    {
        if (!focusCameraOnNpc)
            yield break;

        Transform focusTarget = ResolveCameraFocusTarget();
        if (focusTarget == null)
            yield break;

        CacheCameraState();
        SetCameraTarget(focusTarget);
        yield return WaitForPresentationSeconds(cameraFocusWaitSeconds);
        if (cameraFocusSession != null)
            yield return cameraFocusSession.WaitForSettle(focusTarget);
    }

    private IEnumerator PlayCameraReturn()
    {
        if (!restoreCameraAfterDialogue || cameraFocusSession == null)
            yield break;

        Transform playerTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        Transform restoreTarget = playerTransform != null ? playerTransform : cameraFocusSession.CachedFollow;
        SetCameraTarget(restoreTarget);
        yield return WaitForPresentationSeconds(cameraReturnWaitSeconds);
        yield return cameraFocusSession.WaitForSettle(restoreTarget);
        RestoreCameraState(restoreTarget);
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

    private Transform ResolveCameraFocusTarget()
    {
        if (cameraFocusTarget != null)
            return cameraFocusTarget;

        ISpeechBubblePlayback bubblePlayback = ResolveSpeechBubble();
        if (bubblePlayback != null)
            return bubblePlayback.BubbleTransform;

        return transform;
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

    private void HideChoicePresenterIfAlive()
    {
        if (IsUnityObjectAlive(resolvedChoicePresenter))
            resolvedChoicePresenter.Hide();
        else
            resolvedChoicePresenter = null;
    }

    private void ClearChoiceFollowTargetIfAlive()
    {
        if (IsUnityObjectAlive(choiceAnchorFollower))
            choiceAnchorFollower.ClearFollowTarget();
        else
            choiceAnchorFollower = null;
    }

    private void HideSpeechBubbleIfAlive()
    {
        ResolveSpeechBubble()?.HideActive();
    }

    private ISpeechBubblePlayback ResolveSpeechBubble()
    {
        if (speechBubble != null && speechBubble is ISpeechBubblePlayback existing)
            return existing;

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is ISpeechBubblePlayback playback)
            {
                speechBubble = behaviour;
                return playback;
            }
        }

        speechBubble = null;
        return null;
    }

    private void PauseTimeScaleIfNeeded()
    {
        if (!pauseTimeScale || holdsTimeScalePause)
            return;

        TimeScalePausePlayback.Acquire(this);
        holdsTimeScalePause = true;
    }

    private void RestoreTimeScaleIfNeeded()
    {
        if (!holdsTimeScalePause)
            return;

        TimeScalePausePlayback.Release(this);
        holdsTimeScalePause = false;
    }

    private void ResumeGameplayClockForFeatureExecution()
    {
        if (holdsRunTimerPause && RunTimeLimitSystem.Instance != null)
            RunTimeLimitSystem.Instance.SetExternalPause(this, false);
        holdsRunTimerPause = false;

        RestoreTimeScaleIfNeeded();
    }

    private void ResolvePrimaryFeature()
    {
        if (primaryFeature != null)
            return;

        primaryFeature = GetComponentInChildren<RunSpecialNpcFeatureBase>(includeInactive: true);
    }

    private void ResolveChoicePresenter()
    {
        if (IsUnityObjectAlive(resolvedChoicePresenter))
            return;

        resolvedChoicePresenter = null;
        if (choicePresenter == null)
            choicePresenter = null;
        else if (choicePresenter is IRunSpecialNpcChoicePresenter serializedPresenter &&
                 IsUnityObjectAlive(serializedPresenter))
        {
            resolvedChoicePresenter = serializedPresenter;
            return;
        }

        if (TryResolvePresenter(GetComponentsInChildren<MonoBehaviour>(includeInactive: true), out IRunSpecialNpcChoicePresenter localPresenter))
        {
            resolvedChoicePresenter = localPresenter;
            choicePresenter = localPresenter.PresenterComponent as MonoBehaviour;
            return;
        }

        MonoBehaviour[] presenters = Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < presenters.Length; i++)
        {
            if (presenters[i] is not IRunSpecialNpcChoicePresenter presenter)
                continue;

            if (IsUnityObjectAlive(presenter) && presenter.AllowGlobalLookup)
            {
                resolvedChoicePresenter = presenter;
                choicePresenter = presenter.PresenterComponent as MonoBehaviour;
                return;
            }
        }
    }

    private void ResolveChoiceAnchorFollower()
    {
        if (!IsUnityObjectAlive(resolvedChoicePresenter))
        {
            choiceAnchorFollower = null;
            return;
        }

        Component presenterComponent = resolvedChoicePresenter.PresenterComponent;
        choiceAnchorFollower = presenterComponent != null
            ? presenterComponent.GetComponent<IRunSpecialNpcChoiceAnchorFollower>()
            : null;
    }

    private static void TryConfirmNumberKeyChoice(
        IRunSpecialNpcChoicePresenter presenter,
        int choiceCount)
    {
        int max = Mathf.Min(choiceCount, 9);
        for (int i = 0; i < max; i++)
        {
            KeyCode alphaKey = (KeyCode)((int)KeyCode.Alpha1 + i);
            KeyCode keypadKey = (KeyCode)((int)KeyCode.Keypad1 + i);
            if (Input.GetKeyDown(alphaKey) || Input.GetKeyDown(keypadKey))
            {
                presenter.ConfirmChoiceAt(i);
                return;
            }
        }
    }

    private static bool TryResolvePresenter(
        MonoBehaviour[] candidates,
        out IRunSpecialNpcChoicePresenter presenter)
    {
        presenter = null;
        if (candidates == null)
            return false;

        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null &&
                candidates[i] is IRunSpecialNpcChoicePresenter candidate &&
                IsUnityObjectAlive(candidate))
            {
                presenter = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool IsUnityObjectAlive(object value)
    {
        return value != null &&
               (value is not Object unityObject || unityObject != null);
    }

    private static bool WasLineSkipPressed()
    {
        return Input.GetMouseButtonDown(0) ||
               InputActionQuery.WasPressedThisFrame(InputActionId.DialogueAdvance);
    }

    private void SetOutlineEnabled(bool enabled)
    {
        if (spriteRenderer == null)
            return;

        propertyBlock ??= new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(OutlineEnabledId, enabled ? 1f : 0f);
        spriteRenderer.SetPropertyBlock(propertyBlock);
    }

    private static InteractState NormalizeRestoredPlayerState(InteractState state)
    {
        return state == InteractState.None || state == InteractState.Talking
            ? InteractState.Idle
            : state;
    }
}

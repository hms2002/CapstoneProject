using System;
using System.Collections.Generic;
using Ink.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임:
/// - Ink 대화 세션을 실제 UI, 초상화, 태그 처리, 선택지 입력으로 재생한다.
/// - DialogueService에 자신을 등록해 전역 대화 시작 요청을 현재 씬의 대화 UI로 연결한다.
/// </summary>
public class DialogueController : MonoBehaviour
{
    public static DialogueController Instance { get; private set; }

    [Header("Connected Systems")]
    [SerializeField] private DialogueView view;
    [SerializeField] private CinematicDirector director;
    [SerializeField] private PortraitController portraitController;
    [SerializeField] private DialogueTagHandler tagHandler;
    [SerializeField] private ChoiceFailureScreenEffect choiceFailureEffect;

    [Header("Choice Input")]
    [SerializeField, Min(0f)] private float choiceInputGuardDuration = 0.18f;

    public bool isPlaying => sessionState.IsPlaying;

    private readonly DialogueSessionState sessionState = new DialogueSessionState();
    private readonly DialogueParticipantRegistry participantRegistry = new DialogueParticipantRegistry();
    private readonly Queue<DialogueStorySegment> pendingStorySegments = new Queue<DialogueStorySegment>();

    private Story currentStory;
    private string currentStoryJson;
    private NPCFeatureController currentFeatureController;
    private DialoguePresentationOptions currentPresentationOptions;
    private bool pendingBossChoiceAffectionCheck;
    private bool warnedChoicePreviewFailure;
    private float choiceInputGuardUntil;
    private bool waitingForChoiceConfirmRelease;
    private bool choiceInputReady;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveRuntimeReferences();
        DialogueService.EnsureInstance()?.RegisterController(this);
        BindTagHandler(tagHandler);
    }

    private void OnEnable()
    {
        DialogueService.EnsureInstance()?.RegisterController(this);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        DialogueService.Instance?.UnregisterController(this);
        BindTagHandler(null, tagHandler);

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!sessionState.IsPlaying || sessionState.IsTransitioning || sessionState.IsWaitingForCallback)
            return;

        InputBindingService input = InputBindingService.EnsureInstance();

        if (sessionState.IsChoosing)
        {
            HandleChoiceInput(input);
            return;
        }

        if (!Input.GetMouseButtonDown(0) &&
            !input.WasPressedThisFrame(InputActionId.DialogueAdvance))
            return;

        if (sessionState.IsTyping)
        {
            view.SkipTyping(sessionState.CurrentText);
            sessionState.EndTyping();
            DisplayChoicesIfNeeded();
            return;
        }

        ContinueStory();
    }

    public void EnterDialogueMode(
        TextAsset inkJSON,
        List<NPCData> participants,
        NPCFeatureController featureController = null,
        string startPath = null)
    {
        EnterDialogueSequence(
            new List<DialogueStorySegment> { new DialogueStorySegment(inkJSON, startPath) },
            participants,
            featureController);
    }

    public void EnterDialogueSequence(
        IReadOnlyList<DialogueStorySegment> storySegments,
        List<NPCData> participants,
        NPCFeatureController featureController = null)
    {
        EnterDialogueSequence(
            storySegments,
            participants,
            featureController,
            DialoguePresentationOptions.Default);
    }

    public void EnterDialogueSequence(
        IReadOnlyList<DialogueStorySegment> storySegments,
        List<NPCData> participants,
        DialoguePresentationOptions presentationOptions)
    {
        EnterDialogueSequence(storySegments, participants, null, presentationOptions);
    }

    public void EnterDialogueSequence(
        IReadOnlyList<DialogueStorySegment> storySegments,
        List<NPCData> participants,
        NPCFeatureController featureController,
        DialoguePresentationOptions presentationOptions)
    {
        ResolveRuntimeReferences();

        if (sessionState.IsPlaying)
            return;

        if (!ValidateDialogueSetup(storySegments, participants))
            return;

        DialogueStorySegment firstSegment = GetFirstValidSegment(storySegments);
        List<NPCData> validParticipants = BuildValidParticipants(participants);
        if (validParticipants.Count == 0)
        {
            Debug.LogError("[DialogueController] No valid dialogue participants were provided.", this);
            return;
        }

        participantRegistry.Initialize(validParticipants);
        sessionState.BeginSession();
        currentPresentationOptions = presentationOptions;
        pendingBossChoiceAffectionCheck = false;
        QueuePendingStorySegments(storySegments, firstSegment);

        UIManager.Instance?.HideWorldPrompt();
        UIManager.Instance?.HideHoverImmediate();
        view.ClearText();

        currentFeatureController = featureController;
        if (AffectionManager.Instance != null && participantRegistry.CurrentNPCData != null)
            AffectionManager.Instance.SetCurrentNPC(participantRegistry.CurrentNPCData.id);

        view.ApplyTheme(participantRegistry.CurrentNPCData != null ? participantRegistry.CurrentNPCData.DialogueTheme : null, true);

        if (currentFeatureController != null)
        {
            currentFeatureController.RequestDialogueExit -= ExitDialogueMode;
            currentFeatureController.RequestDialogueExit += ExitDialogueMode;
        }

        if (!TryLoadStorySegment(firstSegment))
        {
            AbortDialogueStart();
            return;
        }

        string openingPortraitLabel = PreviewOpeningPortraitLabel(participantRegistry.CurrentNPCData);
        DialoguePresentationSequencer.PlayOpening(
            view,
            director,
            validParticipants,
            participantRegistry.CurrentNPCData != null && participantRegistry.CurrentNPCData.isBoss,
            currentPresentationOptions,
            openingPortraitLabel,
            () =>
            {
                sessionState.EndTransition();
                ContinueStory();
            });
    }

    private bool TryLoadStorySegment(DialogueStorySegment segment)
    {
        if (!segment.IsValid)
            return false;

        currentStoryJson = segment.InkJSON.text;
        currentStory = new Story(segment.InkJSON.text);
        return TryApplyStartPath(segment.StartPath);
    }

    private bool TryApplyStartPath(string startPath)
    {
        if (currentStory == null || string.IsNullOrWhiteSpace(startPath))
            return true;

        try
        {
            currentStory.ChoosePathString(startPath.Trim());
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[DialogueController] Failed to start dialogue at path '{startPath}'. Falling back to default root. {exception.Message}",
                this);
            return true;
        }
    }

    private string PreviewOpeningPortraitLabel(NPCData primaryParticipant)
    {
        if (currentStory == null || string.IsNullOrWhiteSpace(currentStoryJson))
            return null;

        string storyStateJson = currentStory.state?.ToJson();
        if (string.IsNullOrWhiteSpace(storyStateJson))
            return null;

        try
        {
            Story previewStory = new Story(currentStoryJson);
            previewStory.state.LoadJson(storyStateJson);
            int primaryParticipantId = primaryParticipant != null ? primaryParticipant.id : -1;

            for (int i = 0; i < 8 && previewStory.canContinue; i++)
            {
                string previewText = previewStory.Continue();
                if (TryGetFaceLabel(previewStory.currentTags, primaryParticipantId, out string label))
                    return label;

                if (!string.IsNullOrWhiteSpace(previewText))
                    break;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[DialogueController] Failed to preview opening portrait face tag. {exception.Message}",
                this);
        }

        return null;
    }

    private void AbortDialogueStart()
    {
        if (currentFeatureController != null)
            currentFeatureController.RequestDialogueExit -= ExitDialogueMode;

        view.ResetTheme();
        currentStory = null;
        currentStoryJson = null;
        currentFeatureController = null;
        currentPresentationOptions = DialoguePresentationOptions.Default;
        warnedChoicePreviewFailure = false;
        pendingStorySegments.Clear();
        participantRegistry.Clear();
        sessionState.EndSession();
    }

    public void ResumeDialogue()
    {
        sessionState.EndWaiting();
        if (!sessionState.IsPlaying || sessionState.IsTransitioning || currentStory == null)
            return;

        if (!sessionState.IsTyping && !sessionState.IsChoosing)
            ContinueStory();
    }

    private void ContinueStory()
    {
        if (!sessionState.IsPlaying || sessionState.IsTransitioning || currentStory == null)
            return;

        if (currentStory.canContinue)
        {
            string currentText = currentStory.Continue();
            participantRegistry.HandleSpeakerTag(currentStory.currentTags);
            DialogueAnimType animType = ResolveDialogueAnimType(currentStory.currentTags);
            DialogueCameraShakePreset cameraShakePreset = ResolveDialogueCameraShakePreset(currentStory.currentTags);
            ApplyCurrentSpeakerTheme();
            ApplyDialogueEffectTag(currentStory.currentTags);

            if (portraitController != null)
                portraitController.HighlightSpeaker(participantRegistry.CurrentSpeakerId);

            sessionState.BeginWaiting();
            ResolvePendingBossChoiceFailure(currentText, currentStory.currentTags);

            bool isBlocking = tagHandler != null &&
                              tagHandler.ProcessTags(currentStory.currentTags, participantRegistry.CurrentNPCData, ResumeDialogue);

            if (!isBlocking)
                PlayCurrentLine(currentText, animType, cameraShakePreset);

            return;
        }

        if (currentStory.currentChoices.Count > 0)
        {
            DisplayChoicesIfNeeded();
            return;
        }

        if (TryContinueQueuedStory())
            return;

        ExitDialogueMode();
    }

    private bool TryContinueQueuedStory()
    {
        while (pendingStorySegments.Count > 0)
        {
            DialogueStorySegment nextSegment = pendingStorySegments.Dequeue();
            if (!nextSegment.IsValid)
                continue;

            sessionState.ResetInteractionFlags();
            ResetChoiceInputGate();
            pendingBossChoiceAffectionCheck = false;

            if (!TryLoadStorySegment(nextSegment))
                return false;

            ContinueStory();
            return true;
        }

        return false;
    }

    private void DisplayChoicesIfNeeded()
    {
        if (currentStory == null || currentStory.currentChoices.Count == 0)
            return;

        ChoiceSetAffectionState choiceSetState = EvaluateCurrentChoiceSetAffectionState();
        bool didShowChoices = view != null && view.ShowChoices(currentStory.currentChoices, choiceIndex =>
        {
            pendingBossChoiceAffectionCheck = ShouldCheckBossChoiceFailure(choiceSetState, choiceIndex);
            currentStory.ChooseChoiceIndex(choiceIndex);
            sessionState.EndChoosing();
            ContinueStory();
        });

        if (didShowChoices)
        {
            sessionState.BeginChoosing();
            BeginChoiceInputGate();
            return;
        }

        Debug.LogError("[DialogueController] Failed to show dialogue choices. Exiting dialogue.", this);
        sessionState.EndChoosing();
        ExitDialogueMode();
    }

    private void ExitDialogueMode()
    {
        if (!sessionState.IsPlaying)
            return;

        sessionState.BeginTransition();
        sessionState.ResetInteractionFlags();
        ResetChoiceInputGate();
        pendingBossChoiceAffectionCheck = false;

        if (currentFeatureController != null)
            currentFeatureController.RequestDialogueExit -= ExitDialogueMode;

        DialoguePresentationSequencer.PlayClosing(view, director, currentPresentationOptions, () =>
        {
            view.ResetTheme();
            currentStory = null;
            currentStoryJson = null;
            currentFeatureController = null;
            currentPresentationOptions = DialoguePresentationOptions.Default;
            warnedChoicePreviewFailure = false;
            pendingStorySegments.Clear();
            participantRegistry.Clear();
            sessionState.EndSession();
        });
    }

    private void ApplyCurrentSpeakerTheme()
    {
        NPCData themeOwner = participantRegistry.CurrentSpeakerNPCData != null
            ? participantRegistry.CurrentSpeakerNPCData
            : participantRegistry.CurrentNPCData;

        view.ApplyTheme(themeOwner != null ? themeOwner.DialogueTheme : null, false);
    }

    private void ApplyDialogueEffectTag(List<string> tags)
    {
        if (!TryGetDialogueEffectTarget(tags, out string target))
            return;

        if (!TryResolveDialogueEffectOwner(target, out NPCData themeOwner))
            return;

        view.ApplyDialogueEffectTheme(themeOwner != null ? themeOwner.DialogueTheme : null);
        // TODO: Replay the DialogueEffect Intro state here when mid-dialogue effect transitions are authored.
    }

    private bool TryResolveDialogueEffectOwner(string target, out NPCData themeOwner)
    {
        themeOwner = null;
        if (string.IsNullOrWhiteSpace(target))
            return false;

        string normalizedTarget = target.Trim();
        switch (normalizedTarget.ToLowerInvariant())
        {
            case "default":
                themeOwner = participantRegistry.CurrentNPCData;
                return true;

            case "speaker":
                themeOwner = participantRegistry.CurrentSpeakerNPCData != null
                    ? participantRegistry.CurrentSpeakerNPCData
                    : participantRegistry.CurrentNPCData;
                return true;

            default:
                if (!int.TryParse(normalizedTarget, out _))
                {
                    Debug.LogWarning($"[DialogueController] Unsupported dialogue effect target '{target}'.", this);
                    return false;
                }

                themeOwner = participantRegistry.GetOrLoadNPC(normalizedTarget);
                if (themeOwner == null)
                {
                    Debug.LogWarning($"[DialogueController] Dialogue effect target NPC '{target}' was not found.", this);
                    return false;
                }

                return true;
        }
    }

    private bool ValidateDialogueSetup(IReadOnlyList<DialogueStorySegment> storySegments, List<NPCData> participants)
    {
        ResolveRuntimeReferences();

        if (GetFirstValidSegment(storySegments).InkJSON == null)
        {
            Debug.LogError("[DialogueController] inkJSON is missing. Dialogue cannot start.", this);
            return false;
        }

        if (participants == null || participants.Count == 0)
        {
            Debug.LogError("[DialogueController] Participants are missing. Dialogue cannot start.", this);
            return false;
        }

        if (view == null)
        {
            Debug.LogError("[DialogueController] DialogueView reference is missing. Dialogue cannot start.", this);
            return false;
        }

        if (director == null)
        {
            Debug.LogError("[DialogueController] CinematicDirector reference is missing. Dialogue cannot start.", this);
            return false;
        }

        return true;
    }

    private static DialogueStorySegment GetFirstValidSegment(IReadOnlyList<DialogueStorySegment> storySegments)
    {
        if (storySegments == null)
            return default;

        for (int i = 0; i < storySegments.Count; i++)
        {
            DialogueStorySegment segment = storySegments[i];
            if (segment.IsValid)
                return segment;
        }

        return default;
    }

    private void QueuePendingStorySegments(
        IReadOnlyList<DialogueStorySegment> storySegments,
        DialogueStorySegment firstSegment)
    {
        pendingStorySegments.Clear();

        if (storySegments == null)
            return;

        bool skippedFirst = false;
        for (int i = 0; i < storySegments.Count; i++)
        {
            DialogueStorySegment segment = storySegments[i];
            if (!segment.IsValid)
                continue;

            if (!skippedFirst && segment.InkJSON == firstSegment.InkJSON && segment.StartPath == firstSegment.StartPath)
            {
                skippedFirst = true;
                continue;
            }

            pendingStorySegments.Enqueue(segment);
        }
    }

    private List<NPCData> BuildValidParticipants(List<NPCData> participants)
    {
        List<NPCData> validParticipants = new List<NPCData>();

        foreach (NPCData npc in participants)
        {
            if (npc == null)
            {
                Debug.LogWarning("[DialogueController] Ignoring a null NPC participant.", this);
                continue;
            }

            validParticipants.Add(npc);
        }

        return validParticipants;
    }

    private void HandleChoiceInput(InputBindingService input)
    {
        if (!UpdateChoiceInputGate(input))
            return;

        if (TryHandleChoiceShortcutInput())
            return;

        if (input.WasPressedThisFrame(InputActionId.MoveUp))
            view.ChangeChoiceSelection(-1);
        else if (input.WasPressedThisFrame(InputActionId.MoveDown))
            view.ChangeChoiceSelection(1);
        else if (input.WasPressedThisFrame(InputActionId.DialogueAdvance))
            view.ConfirmChoice();
    }

    private bool TryHandleChoiceShortcutInput()
    {
        for (int i = 0; i < 3; i++)
        {
            if (!WasChoiceShortcutPressed(i))
                continue;

            view.ConfirmChoiceAt(i);
            return true;
        }

        return false;
    }

    private bool WasChoiceShortcutPressed(int zeroBasedIndex)
    {
        return InputKeyCompatibility.WasPressedThisFrame(GetChoiceNumberKey(zeroBasedIndex)) ||
               InputKeyCompatibility.WasPressedThisFrame(GetChoiceKeypadNumberKey(zeroBasedIndex));
    }

    private static KeyCode GetChoiceNumberKey(int zeroBasedIndex)
    {
        return zeroBasedIndex switch
        {
            0 => KeyCode.Alpha1,
            1 => KeyCode.Alpha2,
            2 => KeyCode.Alpha3,
            _ => KeyCode.None,
        };
    }

    private static KeyCode GetChoiceKeypadNumberKey(int zeroBasedIndex)
    {
        return zeroBasedIndex switch
        {
            0 => KeyCode.Keypad1,
            1 => KeyCode.Keypad2,
            2 => KeyCode.Keypad3,
            _ => KeyCode.None,
        };
    }

    private void BeginChoiceInputGate()
    {
        choiceInputReady = false;
        waitingForChoiceConfirmRelease = true;
        choiceInputGuardUntil = Time.unscaledTime + choiceInputGuardDuration;
        view?.SetChoiceInputEnabled(false);
    }

    private bool UpdateChoiceInputGate(InputBindingService input)
    {
        if (choiceInputReady)
            return true;

        if (Time.unscaledTime < choiceInputGuardUntil)
            return false;

        if (waitingForChoiceConfirmRelease)
        {
            if (IsChoiceConfirmInputHeld(input))
                return false;

            waitingForChoiceConfirmRelease = false;
        }

        choiceInputReady = true;
        view?.SetChoiceInputEnabled(true);
        return true;
    }

    private void ResetChoiceInputGate()
    {
        choiceInputReady = false;
        waitingForChoiceConfirmRelease = false;
        choiceInputGuardUntil = 0f;
        view?.SetChoiceInputEnabled(false);
    }

    private bool IsChoiceConfirmInputHeld(InputBindingService input)
    {
        return Input.GetMouseButton(0) ||
               (input != null && input.IsPressed(InputActionId.DialogueAdvance));
    }

    private void PlayCurrentLine(
        string currentText,
        DialogueAnimType animType,
        DialogueCameraShakePreset cameraShakePreset)
    {
        sessionState.EndWaiting();
        sessionState.BeginTyping(currentText);
        view.TypeText(participantRegistry.CurrentSpeakerName, currentText, animType, cameraShakePreset, () =>
        {
            sessionState.EndTyping();
            DisplayChoicesIfNeeded();
        });
    }

    private void HandlePortraitEnter(string id, string val)
    {
        NPCData target = participantRegistry.GetOrLoadNPC(id);
        if (target != null && portraitController != null)
        {
            portraitController.SetInitialPosition(target, val);
            portraitController.EnterAnimation(target);
        }
    }

    private void HandlePortraitFace(string id, string val)
    {
        NPCData target = participantRegistry.GetOrLoadNPC(id);
        if (target != null && portraitController != null)
            portraitController.DoCrossFade(target, val, 0.1f, 0.25f);
    }

    private void HandlePortraitEmote(string id, string val)
    {
        NPCData target = participantRegistry.GetOrLoadNPC(id);
        if (target != null && portraitController != null)
            portraitController.ShowEmote(target, val);
    }

    private void HandlePortraitMove(string id, string val)
    {
        NPCData target = participantRegistry.GetOrLoadNPC(id);
        if (target != null && portraitController != null)
            portraitController.MovePosition(target, val);
    }

    private void HandlePortraitAction(string id, string val)
    {
        NPCData target = participantRegistry.GetOrLoadNPC(id);
        if (target != null && portraitController != null)
            portraitController.PlayAction(target, val);
    }

    private void HandlePortraitExit(string id)
    {
        NPCData target = participantRegistry.GetOrLoadNPC(id);
        if (target != null && portraitController != null)
            portraitController.ExitAnimationAndDestroy(target);
    }

    private void HandleFeature(string val, Action onComplete)
    {
        if (currentFeatureController != null)
            currentFeatureController.ExecuteFeature(val, onComplete);
        else
            onComplete?.Invoke();
    }

    private void HandleAffection(NPCData npcData, int amount, Action onComplete)
    {
        NPCData targetNpc = npcData != null ? npcData : participantRegistry.CurrentNPCData;
        pendingBossChoiceAffectionCheck = false;

        if (AffectionManager.Instance != null)
            AffectionManager.Instance.AddAffection(targetNpc, amount, onComplete);
        else
            onComplete?.Invoke();
    }

    private bool ShouldCheckBossChoiceFailure(ChoiceSetAffectionState choiceSetState, int choiceIndex)
    {
        NPCData currentNpc = participantRegistry.CurrentNPCData;
        return currentNpc != null &&
               currentNpc.isBoss &&
               choiceSetState.HasAnyAddAffectionChoice &&
               !choiceSetState.ChoiceHasAddAffection(choiceIndex);
    }

    private void ResolvePendingBossChoiceFailure(string currentText, List<string> tags)
    {
        if (!pendingBossChoiceAffectionCheck)
            return;

        AffectionChoiceTagState tagState = EvaluateAffectionChoiceTags(tags);
        if (tagState.HasAddAffection || tagState.HasExplicitFailure)
        {
            pendingBossChoiceAffectionCheck = false;
            return;
        }

        if (!HasChoiceResultContent(currentText, tags))
            return;

        pendingBossChoiceAffectionCheck = false;
        PlayChoiceFailureEffect();
    }

    private void HandleChoiceFailure(Action onComplete)
    {
        pendingBossChoiceAffectionCheck = false;
        PlayChoiceFailureEffect(onComplete);
    }

    private void PlayChoiceFailureEffect(Action onComplete = null)
    {
        ResolveChoiceFailureEffect();

        if (choiceFailureEffect != null)
        {
            choiceFailureEffect.Play(onComplete);
            return;
        }

        onComplete?.Invoke();
    }

    private void ResolveChoiceFailureEffect()
    {
        if (choiceFailureEffect != null)
            return;

        choiceFailureEffect = ChoiceFailureScreenEffect.PrepareSceneInstance();
    }

    private ChoiceSetAffectionState EvaluateCurrentChoiceSetAffectionState()
    {
        ChoiceSetAffectionState state = default;
        if (currentStory == null || currentStory.currentChoices == null || currentStory.currentChoices.Count == 0)
            return state;

        string storyStateJson = null;
        if (!string.IsNullOrWhiteSpace(currentStoryJson) && currentStory.state != null)
            storyStateJson = currentStory.state.ToJson();

        for (int i = 0; i < currentStory.currentChoices.Count; i++)
        {
            Choice choice = currentStory.currentChoices[i];
            if (choice == null)
                continue;

            if (HasAddAffectionTag(choice.tags) ||
                PreviewChoiceResultHasAddAffection(storyStateJson, choice.index))
            {
                state.MarkChoiceHasAddAffection(choice.index);
            }
        }

        return state;
    }

    private bool PreviewChoiceResultHasAddAffection(string storyStateJson, int choiceIndex)
    {
        if (string.IsNullOrWhiteSpace(currentStoryJson) || string.IsNullOrWhiteSpace(storyStateJson))
            return false;

        try
        {
            Story previewStory = new Story(currentStoryJson);
            previewStory.state.LoadJson(storyStateJson);
            if (choiceIndex < 0 || choiceIndex >= previewStory.currentChoices.Count)
                return false;

            previewStory.ChooseChoiceIndex(choiceIndex);
            if (!previewStory.canContinue)
                return false;

            previewStory.Continue();
            return HasAddAffectionTag(previewStory.currentTags);
        }
        catch (Exception exception)
        {
            if (!warnedChoicePreviewFailure)
            {
                warnedChoicePreviewFailure = true;
                Debug.LogWarning(
                    $"[DialogueController] Failed to preview dialogue choice tags. {exception.Message}",
                    this);
            }

            return false;
        }
    }

    private static bool HasChoiceResultContent(string currentText, List<string> tags)
    {
        if (!string.IsNullOrWhiteSpace(currentText))
            return true;

        return tags != null && tags.Count > 0;
    }

    private static bool HasAddAffectionTag(List<string> tags)
    {
        return EvaluateAffectionChoiceTags(tags).HasAddAffection;
    }

    private static AffectionChoiceTagState EvaluateAffectionChoiceTags(List<string> tags)
    {
        AffectionChoiceTagState state = default;
        if (tags == null)
            return state;

        for (int i = 0; i < tags.Count; i++)
        {
            if (!TryReadTagCommand(tags[i], out string command, out _))
                continue;

            switch (command)
            {
                case "add_aff":
                    state.HasAddAffection = true;
                    break;

                case "choice_fail":
                case "aff_fail":
                case "fail_aff":
                    state.HasExplicitFailure = true;
                    break;
            }
        }

        return state;
    }

    private static DialogueAnimType ResolveDialogueAnimType(List<string> tags)
    {
        if (tags == null)
            return DialogueAnimType.Normal;

        for (int i = 0; i < tags.Count; i++)
        {
            if (!TryReadTagCommand(tags[i], out string command, out string value))
                continue;

            if ((command == "anim" || command == "dialogue_anim") &&
                DialogueTextRevealUtility.TryParseAnimType(value, out DialogueAnimType animType))
            {
                return animType;
            }
        }

        return DialogueAnimType.Normal;
    }

    private static DialogueCameraShakePreset ResolveDialogueCameraShakePreset(List<string> tags)
    {
        if (tags == null)
            return DialogueCameraShakePreset.None;

        for (int i = 0; i < tags.Count; i++)
        {
            if (!TryReadTagCommand(tags[i], out string command, out string value))
                continue;

            if (command == "camerashake" &&
                TryParseDialogueCameraShakePreset(value, out DialogueCameraShakePreset preset))
            {
                return preset;
            }
        }

        return DialogueCameraShakePreset.None;
    }

    private static bool TryParseDialogueCameraShakePreset(
        string value,
        out DialogueCameraShakePreset preset)
    {
        preset = DialogueCameraShakePreset.None;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "low":
                preset = DialogueCameraShakePreset.Low;
                return true;
            case "middle":
                preset = DialogueCameraShakePreset.Middle;
                return true;
            case "high":
                preset = DialogueCameraShakePreset.High;
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetDialogueEffectTarget(List<string> tags, out string target)
    {
        target = null;
        if (tags == null)
            return false;

        for (int i = 0; i < tags.Count; i++)
        {
            if (!TryReadTagCommand(tags[i], out string command, out string value))
                continue;

            if (command != "effect")
                continue;

            if (string.IsNullOrWhiteSpace(value))
                continue;

            target = value;
            return true;
        }

        return false;
    }

    private static bool TryReadTagCommand(string tag, out string command, out string value)
    {
        command = string.Empty;
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(tag))
            return false;

        string[] split = tag.Split(':');
        if (split.Length == 0)
            return false;

        command = split[0].Trim().ToLowerInvariant();
        if (split.Length >= 2)
            value = split[split.Length - 1].Trim();

        return !string.IsNullOrWhiteSpace(command);
    }

    private static bool TryGetFaceLabel(List<string> tags, int primaryParticipantId, out string label)
    {
        label = null;
        if (tags == null)
            return false;

        for (int i = 0; i < tags.Count; i++)
        {
            string tag = tags[i];
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            string[] split = tag.Split(':');
            if (split.Length < 2 || split[0].Trim().ToLowerInvariant() != "face")
                continue;

            if (split.Length == 2)
            {
                label = split[1].Trim();
                return !string.IsNullOrWhiteSpace(label);
            }

            string targetId = split[1].Trim();
            if (primaryParticipantId >= 0 && !string.Equals(targetId, primaryParticipantId.ToString(), StringComparison.Ordinal))
                continue;

            label = split[2].Trim();
            return !string.IsNullOrWhiteSpace(label);
        }

        return false;
    }

    private struct AffectionChoiceTagState
    {
        public bool HasAddAffection;
        public bool HasExplicitFailure;
    }

    private struct ChoiceSetAffectionState
    {
        private HashSet<int> addAffectionChoiceIndices;

        public bool HasAnyAddAffectionChoice { get; private set; }

        public void MarkChoiceHasAddAffection(int choiceIndex)
        {
            HasAnyAddAffectionChoice = true;
            addAffectionChoiceIndices ??= new HashSet<int>();
            addAffectionChoiceIndices.Add(choiceIndex);
        }

        public bool ChoiceHasAddAffection(int choiceIndex)
        {
            return addAffectionChoiceIndices != null && addAffectionChoiceIndices.Contains(choiceIndex);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveRuntimeReferences();
    }

    private void ResolveRuntimeReferences()
    {
        DialogueTagHandler previousTagHandler = tagHandler;
        DialogueResolvedReferences resolved = DialogueRuntimeReferenceResolver.Resolve(this, view, director, portraitController, tagHandler);
        view = resolved.View;
        director = resolved.Director;
        portraitController = resolved.PortraitController;
        tagHandler = resolved.TagHandler;

        if (previousTagHandler != tagHandler)
            BindTagHandler(tagHandler, previousTagHandler);
    }

    private void BindTagHandler(DialogueTagHandler newTagHandler, DialogueTagHandler previousTagHandler = null)
    {
        if (previousTagHandler != null)
        {
            previousTagHandler.OnPortraitEnterRequested -= HandlePortraitEnter;
            previousTagHandler.OnPortraitFaceRequested -= HandlePortraitFace;
            previousTagHandler.OnPortraitEmoteRequested -= HandlePortraitEmote;
            previousTagHandler.OnPortraitActionRequested -= HandlePortraitAction;
            previousTagHandler.OnPortraitMoveRequested -= HandlePortraitMove;
            previousTagHandler.OnPortraitExitRequested -= HandlePortraitExit;
            previousTagHandler.OnFeatureRequested -= HandleFeature;
            previousTagHandler.OnAffectionRequested -= HandleAffection;
            previousTagHandler.OnChoiceFailureRequested -= HandleChoiceFailure;
        }

        tagHandler = newTagHandler;
        if (tagHandler == null)
            return;

        tagHandler.OnPortraitEnterRequested -= HandlePortraitEnter;
        tagHandler.OnPortraitFaceRequested -= HandlePortraitFace;
        tagHandler.OnPortraitEmoteRequested -= HandlePortraitEmote;
        tagHandler.OnPortraitActionRequested -= HandlePortraitAction;
        tagHandler.OnPortraitMoveRequested -= HandlePortraitMove;
        tagHandler.OnPortraitExitRequested -= HandlePortraitExit;
        tagHandler.OnFeatureRequested -= HandleFeature;
        tagHandler.OnAffectionRequested -= HandleAffection;
        tagHandler.OnChoiceFailureRequested -= HandleChoiceFailure;

        tagHandler.OnPortraitEnterRequested += HandlePortraitEnter;
        tagHandler.OnPortraitFaceRequested += HandlePortraitFace;
        tagHandler.OnPortraitEmoteRequested += HandlePortraitEmote;
        tagHandler.OnPortraitActionRequested += HandlePortraitAction;
        tagHandler.OnPortraitMoveRequested += HandlePortraitMove;
        tagHandler.OnPortraitExitRequested += HandlePortraitExit;
        tagHandler.OnFeatureRequested += HandleFeature;
        tagHandler.OnAffectionRequested += HandleAffection;
        tagHandler.OnChoiceFailureRequested += HandleChoiceFailure;
    }
}

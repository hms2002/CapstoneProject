using System;
using System.Collections.Generic;
using Ink.Runtime;
using UnityEngine;

public class DialogueController : MonoBehaviour
{
    public static DialogueController Instance { get; private set; }

    [Header("Connected Systems")]
    [SerializeField] private DialogueView view;
    [SerializeField] private CinematicDirector director;
    [SerializeField] private PortraitController portraitController;
    [SerializeField] private DialogueTagHandler tagHandler;

    [Header("Input")]
    [SerializeField] private KeyCode skipKey = KeyCode.Space;
    [SerializeField] private KeyCode skipKeyAlt = KeyCode.F;

    public bool isPlaying => sessionState.IsPlaying;

    private readonly DialogueSessionState sessionState = new DialogueSessionState();
    private readonly DialogueParticipantRegistry participantRegistry = new DialogueParticipantRegistry();

    private Story currentStory;
    private NPCFeatureController currentFeatureController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DialogueService.Instance?.RegisterController(this);

        if (tagHandler != null)
        {
            tagHandler.OnPortraitEnterRequested += HandlePortraitEnter;
            tagHandler.OnPortraitFaceRequested += HandlePortraitFace;
            tagHandler.OnPortraitEmoteRequested += HandlePortraitEmote;
            tagHandler.OnPortraitActionRequested += HandlePortraitAction;
            tagHandler.OnPortraitMoveRequested += HandlePortraitMove;
            tagHandler.OnPortraitExitRequested += HandlePortraitExit;
            tagHandler.OnFeatureRequested += HandleFeature;
            tagHandler.OnAffectionRequested += HandleAffection;
        }
    }

    private void OnDestroy()
    {
        DialogueService.Instance?.UnregisterController(this);

        if (tagHandler != null)
        {
            tagHandler.OnPortraitEnterRequested -= HandlePortraitEnter;
            tagHandler.OnPortraitFaceRequested -= HandlePortraitFace;
            tagHandler.OnPortraitEmoteRequested -= HandlePortraitEmote;
            tagHandler.OnPortraitActionRequested -= HandlePortraitAction;
            tagHandler.OnPortraitMoveRequested -= HandlePortraitMove;
            tagHandler.OnPortraitExitRequested -= HandlePortraitExit;
            tagHandler.OnFeatureRequested -= HandleFeature;
            tagHandler.OnAffectionRequested -= HandleAffection;
        }

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!sessionState.IsPlaying || sessionState.IsTransitioning || sessionState.IsWaitingForCallback)
            return;

        if (sessionState.IsChoosing)
        {
            HandleChoiceInput();
            return;
        }

        if (!Input.GetMouseButtonDown(0) && !Input.GetKeyDown(skipKey) && !Input.GetKeyDown(skipKeyAlt))
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

    public void EnterDialogueMode(TextAsset inkJSON, List<NPCData> participants, NPCFeatureController featureController = null)
    {
        if (sessionState.IsPlaying)
            return;

        if (!ValidateDialogueSetup(inkJSON, participants))
            return;

        List<NPCData> validParticipants = BuildValidParticipants(participants);
        if (validParticipants.Count == 0)
        {
            Debug.LogError("[DialogueController] No valid dialogue participants were provided.", this);
            return;
        }

        participantRegistry.Initialize(validParticipants);
        sessionState.BeginSession();

        UIManager.Instance?.HideWorldPrompt();
        UIManager.Instance?.HideHoverImmediate();
        view.ClearText();

        currentFeatureController = featureController;
        if (AffectionManager.Instance != null && participantRegistry.CurrentNPCData != null)
            AffectionManager.Instance.SetCurrentNPC(participantRegistry.CurrentNPCData.id);

        if (currentFeatureController != null)
        {
            currentFeatureController.RequestDialogueExit -= ExitDialogueMode;
            currentFeatureController.RequestDialogueExit += ExitDialogueMode;
        }

        currentStory = new Story(inkJSON.text);

        director.PlayIntro(validParticipants, () =>
        {
            view.ShowUI(participantRegistry.CurrentNPCData.isBoss, () =>
            {
                sessionState.EndTransition();
                ContinueStory();
            });
        });
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

            if (portraitController != null)
                portraitController.HighlightSpeaker(participantRegistry.CurrentSpeakerId);

            sessionState.BeginWaiting();
            bool isBlocking = tagHandler != null &&
                              tagHandler.ProcessTags(currentStory.currentTags, participantRegistry.CurrentNPCData, ResumeDialogue);

            if (!isBlocking)
                PlayCurrentLine(currentText);

            return;
        }

        if (currentStory.currentChoices.Count > 0)
        {
            DisplayChoicesIfNeeded();
            return;
        }

        ExitDialogueMode();
    }

    private void DisplayChoicesIfNeeded()
    {
        if (currentStory == null || currentStory.currentChoices.Count == 0)
            return;

        bool didShowChoices = view != null && view.ShowChoices(currentStory.currentChoices, choiceIndex =>
        {
            currentStory.ChooseChoiceIndex(choiceIndex);
            sessionState.EndChoosing();
            ContinueStory();
        });

        if (didShowChoices)
        {
            sessionState.BeginChoosing();
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

        if (currentFeatureController != null)
            currentFeatureController.RequestDialogueExit -= ExitDialogueMode;

        view.HideUI(() =>
        {
            director.PlayOutro(() =>
            {
                currentStory = null;
                currentFeatureController = null;
                participantRegistry.Clear();
                sessionState.EndSession();
            });
        });
    }

    private bool ValidateDialogueSetup(TextAsset inkJSON, List<NPCData> participants)
    {
        if (inkJSON == null)
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

    private void HandleChoiceInput()
    {
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            view.ChangeChoiceSelection(-1);
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            view.ChangeChoiceSelection(1);
        else if (Input.GetKeyDown(skipKey) || Input.GetKeyDown(skipKeyAlt) || Input.GetKeyDown(KeyCode.Return))
            view.ConfirmChoice();
    }

    private void PlayCurrentLine(string currentText)
    {
        sessionState.EndWaiting();
        sessionState.BeginTyping(currentText);
        view.TypeText(participantRegistry.CurrentSpeakerName, currentText, () =>
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
        if (AffectionManager.Instance != null)
            AffectionManager.Instance.AddAffection(targetNpc, amount, onComplete);
        else
            onComplete?.Invoke();
    }
}

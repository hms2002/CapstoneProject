using UnityEngine;
using System.Collections.Generic;
using Ink.Runtime;
using System;

public class DialogueController : MonoBehaviour
{
    public static DialogueController Instance { get; private set; }

    [Header("연결된 시스템 (MVC)")]
    [SerializeField] private DialogueView view;
    [SerializeField] private CinematicDirector director;
    [SerializeField] private PortraitController portraitController;
    [SerializeField] private DialogueTagHandler tagHandler;

    [Header("입력 설정")]
    [SerializeField] private KeyCode skipKey = KeyCode.Space;
    [SerializeField] private KeyCode skipKeyAlt = KeyCode.F;

    public bool isPlaying { get; private set; } = false;
    private Story currentStory;
    private bool isTyping = false;
    private bool isChoosing = false;
    private bool isTransitioning = false;
    private bool isWaitingForCallback = false;

    private int currentSpeakerId = -1;
    private string currentSpeakerName = "";
    private string currentText = "";

    private Dictionary<int, NPCData> activeNPCs = new Dictionary<int, NPCData>();
    private NPCData currentNPCData;
    private NPCFeatureController currentFeatureController;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;

        if (tagHandler != null)
        {
            tagHandler.OnPortraitEnterRequested += HandlePortraitEnter;
            tagHandler.OnPortraitFaceRequested += HandlePortraitFace;
            tagHandler.OnPortraitEmoteRequested += HandlePortraitEmote;
            tagHandler.OnPortraitActionRequested += HandlePortraitAction;
            tagHandler.OnPortraitMoveRequested += HandlePortraitMove;
            tagHandler.OnPortraitExitRequested += HandlePortraitExit;
            tagHandler.OnFeatureRequested += HandleFeature; // 서명 일치됨
            tagHandler.OnAffectionRequested += HandleAffection;
        }
    }

    // Update문은 기존과 100% 동일하여 생략 없이 유지
    private void Update()
    {
        if (!isPlaying || isTransitioning || isWaitingForCallback) return;

        if (isChoosing)
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) view.ChangeChoiceSelection(-1);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) view.ChangeChoiceSelection(1);
            else if (Input.GetKeyDown(skipKey) || Input.GetKeyDown(skipKeyAlt) || Input.GetKeyDown(KeyCode.Return)) view.ConfirmChoice();
            return;
        }

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(skipKey) || Input.GetKeyDown(skipKeyAlt))
        {
            if (isTyping)
            {
                view.SkipTyping(currentText);
                isTyping = false;
                DisplayChoicesIfNeeded();
            }
            else ContinueStory();
        }
    }

    public void EnterDialogueMode(TextAsset inkJSON, List<NPCData> participants, NPCFeatureController featureController = null)
    {
        if (isPlaying || participants == null || participants.Count == 0) return;

        activeNPCs.Clear();
        foreach (var npc in participants)
        {
            if (!activeNPCs.ContainsKey(npc.id)) activeNPCs.Add(npc.id, npc);
        }

        isPlaying = true;
        isTyping = false;
        isChoosing = false;
        isTransitioning = true;
        isWaitingForCallback = false;

        // [핵심 해결] 이전 대화의 잔상이 보이지 않도록 즉시 비워줍니다!
        view.ClearText();

        currentNPCData = participants[0];
        currentFeatureController = featureController;

        currentSpeakerId = currentNPCData.id;
        currentSpeakerName = currentNPCData.npcName;

        if (AffectionManager.Instance != null) AffectionManager.Instance.SetCurrentNPC(currentNPCData.id);

        if (currentFeatureController != null)
        {
            currentFeatureController.RequestDialogueExit -= ExitDialogueMode;
            currentFeatureController.RequestDialogueExit += ExitDialogueMode;
        }

        currentStory = new Story(inkJSON.text);

        director.PlayIntro(participants, () =>
        {
            view.ShowUI(currentNPCData.isBoss, () =>
            {
                isTransitioning = false;
                ContinueStory();
            });
        });
    }

    private void ContinueStory()
    {
        if (currentStory.canContinue)
        {
            currentText = currentStory.Continue();
            HandleSpeakerTag(currentStory.currentTags);

            if (portraitController != null) portraitController.HighlightSpeaker(currentSpeakerId);

            isWaitingForCallback = true;
            bool isBlocking = false;

            if (tagHandler != null)
                isBlocking = tagHandler.ProcessTags(currentStory.currentTags, currentNPCData, ResumeDialogue);

            if (!isBlocking)
            {
                isWaitingForCallback = false;
                isTyping = true;
                view.TypeText(currentSpeakerName, currentText, () =>
                {
                    isTyping = false;
                    DisplayChoicesIfNeeded();
                });
            }
        }
        else if (currentStory.currentChoices.Count > 0) DisplayChoicesIfNeeded();
        else ExitDialogueMode();
    }

    private void HandleSpeakerTag(List<string> currentTags)
    {
        foreach (string tag in currentTags)
        {
            string[] splitTag = tag.Split(':');
            if (splitTag.Length != 2) continue;

            if (splitTag[0].Trim().ToLower() == "speaker")
            {
                string speakerVal = splitTag[1].Trim();
                if (int.TryParse(speakerVal, out int id))
                {
                    currentSpeakerId = id;
                    NPCData data = GetOrLoadNPC(speakerVal);
                    if (data != null) currentSpeakerName = data.npcName;
                    else currentSpeakerName = "???";
                }
                else
                {
                    currentSpeakerId = -1;
                    currentSpeakerName = speakerVal;
                }
                break;
            }
        }
    }

    private void DisplayChoicesIfNeeded()
    {
        if (currentStory.currentChoices.Count > 0)
        {
            isChoosing = true;
            view.ShowChoices(currentStory.currentChoices, (choiceIndex) =>
            {
                currentStory.ChooseChoiceIndex(choiceIndex);
                isChoosing = false;
                ContinueStory();
            });
        }
    }

    private void ExitDialogueMode()
    {
        isTransitioning = true;
        if (currentFeatureController != null) currentFeatureController.RequestDialogueExit -= ExitDialogueMode;

        view.HideUI(() =>
        {
            director.PlayOutro(() =>
            {
                currentStory = null;
                currentFeatureController = null;
                activeNPCs.Clear();
                isTransitioning = false;
                isPlaying = false;
            });
        });
    }

    public void ResumeDialogue()
    {
        isWaitingForCallback = false;
        if (isPlaying && !isTyping && !isChoosing) ContinueStory();
    }

    private NPCData GetOrLoadNPC(string idStr)
    {
        if (int.TryParse(idStr, out int npcId))
        {
            if (activeNPCs.TryGetValue(npcId, out NPCData data)) return data;
            NPCData newData = NPCManager.Instance?.GetNPCData(npcId);
            if (newData != null)
            {
                activeNPCs.Add(npcId, newData);
                return newData;
            }
        }
        return null;
    }

    // ---------------- 태그 핸들러 ----------------
    private void HandlePortraitEnter(string id, string val) { NPCData target = GetOrLoadNPC(id); if (target != null && portraitController != null) { portraitController.SetInitialPosition(target, val); portraitController.EnterAnimation(target); } }
    private void HandlePortraitFace(string id, string val) { NPCData target = GetOrLoadNPC(id); if (target != null && portraitController != null) portraitController.DoCrossFade(target, val, 0.1f, 0.25f); }
    private void HandlePortraitEmote(string id, string val) { NPCData target = GetOrLoadNPC(id); if (target != null && portraitController != null) portraitController.ShowEmote(target, val); }
    private void HandlePortraitMove(string id, string val) { NPCData target = GetOrLoadNPC(id); if (target != null && portraitController != null) portraitController.MovePosition(target, val); }
    private void HandlePortraitAction(string id, string val) { NPCData target = GetOrLoadNPC(id); if (target != null && portraitController != null) portraitController.PlayAction(target, val); }
    private void HandlePortraitExit(string id) { NPCData target = GetOrLoadNPC(id); if (target != null && portraitController != null) portraitController.ExitAnimationAndDestroy(target); }

    // [핵심 수정] 서명이 Action<string, Action>으로 변경되어 onComplete를 받아 넘깁니다!
    private void HandleFeature(string val, Action onComplete)
    {
        if (currentFeatureController != null)
            currentFeatureController.ExecuteFeature(val, onComplete);
        else
            onComplete?.Invoke(); // 컨트롤러가 없으면 대화 멈추지 않게 즉시 진행!
    }

    private void HandleAffection(NPCData npcData, int amount, Action onComplete)
    {
        if (AffectionManager.Instance != null) AffectionManager.Instance.AddAffection(npcData, amount, onComplete);
        else onComplete?.Invoke();
    }

    private void OnDestroy()
    {
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
    }
}
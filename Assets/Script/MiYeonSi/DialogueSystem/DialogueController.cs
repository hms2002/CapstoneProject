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

    // ID 기반 화자 관리 변수
    private int currentSpeakerId = -1;
    private string currentSpeakerName = "";
    private string currentText = "";

    // 현재 대화 명단 (동적 난입 지원)
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
            tagHandler.OnFeatureRequested += HandleFeature;
            tagHandler.OnAffectionRequested += HandleAffection;
        }
    }

    private void Update()
    {
        // 연출 중이거나 콜백 대기 중일 땐 입력 무시
        if (!isPlaying || isTransitioning || isWaitingForCallback) return;

        // 선택지 고르는 중
        if (isChoosing)
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                view.ChangeChoiceSelection(-1);
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                view.ChangeChoiceSelection(1);
            else if (Input.GetKeyDown(skipKey) || Input.GetKeyDown(skipKeyAlt) || Input.GetKeyDown(KeyCode.Return))
                view.ConfirmChoice();
            return;
        }

        // 일반 대화 진행 (클릭 또는 스킵 키)
        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(skipKey) || Input.GetKeyDown(skipKeyAlt))
        {
            if (isTyping)
            {
                view.SkipTyping(currentText);
                isTyping = false;
                DisplayChoicesIfNeeded();
            }
            else
            {
                ContinueStory();
            }
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

        currentNPCData = participants[0];
        currentFeatureController = featureController;

        // 초기 화자 설정 (첫 번째 인물)
        currentSpeakerId = currentNPCData.id;
        currentSpeakerName = currentNPCData.npcName;

        if (AffectionManager.Instance != null) AffectionManager.Instance.SetCurrentNPC(currentNPCData.id);

        if (currentFeatureController != null)
        {
            currentFeatureController.RequestDialogueExit -= ExitDialogueMode;
            currentFeatureController.RequestDialogueExit += ExitDialogueMode;
        }

        currentStory = new Story(inkJSON.text);

        // 감독님에게 자동 배치 지시
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

            // 1. 화자 태그 파싱 (ID 및 이름 세팅)
            HandleSpeakerTag(currentStory.currentTags);

            // 2. 조명 연출 지시 (ID 기반)
            if (portraitController != null) portraitController.HighlightSpeaker(currentSpeakerId);

            isWaitingForCallback = true;

            // 3. 기타 태그 처리
            bool isBlocking = false;
            if (tagHandler != null)
                isBlocking = tagHandler.ProcessTags(currentStory.currentTags, currentNPCData, ResumeDialogue);

            if (!isBlocking)
            {
                isWaitingForCallback = false;
                isTyping = true;

                // 4. UI에는 파싱된 '이름' 출력
                view.TypeText(currentSpeakerName, currentText, () =>
                {
                    isTyping = false;
                    DisplayChoicesIfNeeded();
                });
            }
        }
        else if (currentStory.currentChoices.Count > 0)
        {
            DisplayChoicesIfNeeded();
        }
        else
        {
            ExitDialogueMode();
        }
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

                // 태그가 숫자(ID)라면 DB에서 이름 찾기
                if (int.TryParse(speakerVal, out int id))
                {
                    currentSpeakerId = id;
                    NPCData data = GetOrLoadNPC(speakerVal);

                    if (data != null) currentSpeakerName = data.npcName;
                    else currentSpeakerName = "???"; // DB에 없을 때
                }
                // 나레이션이나 시스템 메시지 등 일반 텍스트일 경우
                else
                {
                    currentSpeakerId = -1; // 모든 액터 조명 끔
                    currentSpeakerName = speakerVal; // 텍스트 그대로 출력
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

    // =================================================================
    // 동적 영입 헬퍼 함수
    // =================================================================
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

    // =================================================================
    // 태그 이벤트 핸들러들
    // =================================================================
    private void HandlePortraitEnter(string id, string val)
    {
        NPCData targetData = GetOrLoadNPC(id);
        if (targetData != null && portraitController != null)
        {
            portraitController.SetInitialPosition(targetData, val);
            portraitController.EnterAnimation(targetData);
        }
    }

    private void HandlePortraitFace(string id, string val)
    {
        NPCData targetData = GetOrLoadNPC(id);
        if (targetData != null && portraitController != null)
            portraitController.DoCrossFade(targetData, val, 0.1f, 0.25f);
    }

    private void HandlePortraitEmote(string id, string val)
    {
        NPCData targetData = GetOrLoadNPC(id);
        if (targetData != null && portraitController != null)
            portraitController.ShowEmote(targetData, val);
    }

    private void HandlePortraitMove(string id, string val)
    {
        NPCData targetData = GetOrLoadNPC(id);
        if (targetData != null && portraitController != null)
            portraitController.MovePosition(targetData, val);
    }

    private void HandlePortraitAction(string id, string val)
    {
        NPCData targetData = GetOrLoadNPC(id);
        if (targetData != null && portraitController != null)
            portraitController.PlayAction(targetData, val);
    }

    private void HandlePortraitExit(string id)
    {
        NPCData targetData = GetOrLoadNPC(id);
        if (targetData != null && portraitController != null)
            portraitController.ExitAnimationAndDestroy(targetData);
    }

    private void HandleFeature(string val)
    {
        if (currentFeatureController != null) currentFeatureController.ExecuteFeature(val);
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
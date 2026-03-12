using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;          // [추가] 버튼 제어를 위해 필요
using UnityEngine.EventSystems;// [추가] 현재 선택된 오브젝트 확인을 위해 필요
using Ink.Runtime;
using TMPro;

public enum DialogueState
{
    Idle,           // 대화 종료
    Typing,         // 텍스트 타이핑 중
    WaitingForInput,// 타이핑 끝, 클릭 대기
    EventPaused     // 연출/UI 실행 중 (입력 차단, 흐름 정지)
}

public class DialogueManager : MonoBehaviour
{
    public enum Direction { Left, Right, Up, Down }

    [System.Serializable]
    public class DialogueUI
    {
        public GameObject panelObj;
        public TextMeshProUGUI dialogueText;
        public TextMeshProUGUI nameText;
        public CanvasGroup canvasGroup;
        public RectTransform dialogueTextCon;
        public RectTransform speakerFrame;
        public GameObject continueIcon;
        [HideInInspector] public Vector3 iconStartPos;
        public GameObject[] choiceButtons;
        [HideInInspector] public TextMeshProUGUI[] choicesText;
        [HideInInspector] public UnityEngine.UI.Button[] buttons;
    }

    [System.Serializable]
    public class BossDialogueUI : DialogueUI
    {
        public AffectionUI affectionUI;
        public RectTransform portraitFrame;
        public RectTransform dialogueEffectGroup;
        public RectTransform topCinematicPanel;
        public RectTransform bottomCinematicPanel;
        public RectTransform shardUp, shardUpRight, shardRight, shardDownRight, shardDown, shardDownLeft, shardLeft, shardUpLeft;
        [HideInInspector] public RectTransform[] shardsArray;
        [HideInInspector] public Vector2[] shardTargetAnchoredPositions;
    }

    private static DialogueManager instance;
    public static DialogueManager GetInstance() => instance;

    [Header("컴포넌트")]
    [SerializeField] private DialogueUIManager uiManager;
    [SerializeField] private DialogueTagHandler tagHandler;

    [Header("설정")]
    [SerializeField] private TextAsset loadGlobalsJSON;

    private Story currentStory;
    private DialogueVariables dialogueVariables;
    private DialogueState currentState = DialogueState.Idle;
    private NPCData currentNPC;

    // [핵심] 연출 때문에 출력이 보류된 대사
    private string pendingLineContent = "";

    public bool dialogueIsPlaying => currentState != DialogueState.Idle;

    private void Awake()
    {
        instance = this;
        dialogueVariables = new DialogueVariables(loadGlobalsJSON);
    }

    private void Update()
    {
        if (currentState == DialogueState.Idle || currentState == DialogueState.EventPaused) return;
        if (UIManager.Instance != null && UIManager.Instance.IsAnyBlockingUIOpen) return;

        // [입력 키 통합] F, Enter, Space를 모두 '결정/진행' 키로 사용
        bool submitInput = Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);

        // 1. 타이핑 중일 때 -> 스킵
        if (currentState == DialogueState.Typing && submitInput)
        {
            uiManager.CompleteTyping();
            return;
        }

        // 2. 선택지가 떠 있을 때 -> 키보드로 선택
        if (currentState == DialogueState.WaitingForInput && currentStory.currentChoices.Count > 0)
        {
            if (submitInput)
            {
                // 현재 EventSystem이 선택하고 있는 오브젝트(버튼)를 가져옴
                GameObject selectedObj = EventSystem.current.currentSelectedGameObject;
                if (selectedObj != null)
                {
                    Button btn = selectedObj.GetComponent<Button>();
                    if (btn != null)
                    {
                        // 버튼의 클릭 이벤트를 강제로 실행 (MakeChoice 호출됨)
                        btn.onClick.Invoke();
                    }
                }
            }
            return;
        }

        // 3. 일반 대화 대기 중일 때 -> 다음 대사 진행
        if (currentState == DialogueState.WaitingForInput && currentStory.currentChoices.Count == 0 && submitInput)
        {
            ContinueStory();
        }
    }

    public void EnterDialogueMode(TextAsset inkJSON, NPCData data, Direction bossDir = Direction.Left, NPCFeatureController featureController = null)
    {
        currentNPC = data;
        currentStory = new Story(inkJSON.text);
        dialogueVariables.StartListening(currentStory);

        currentStory.BindExternalFunction("GetAffection", () => AffectionManager.Instance.GetAffection());
        currentStory.BindExternalFunction("CallFeature", (string key) => ExecuteFeature(key));

        AffectionManager.Instance.SetCurrentNPC(data.id);
        if (data.isBoss) AffectionManager.Instance.SetLinkedUI(((BossDialogueUI)uiManager.GetBossUI()).affectionUI);
        else AffectionManager.Instance.SetLinkedUI(null);

        uiManager.SetupDialogue(data, featureController);

        if (data.isBoss)
        {
            SetState(DialogueState.EventPaused);
            uiManager.StartBossSequence(bossDir, () =>
            {
                SetState(DialogueState.WaitingForInput);
                ContinueStory();
            });
        }
        else
        {
            uiManager.ShowNormalPanel();
            ContinueStory();
        }

        if (SampleTopDownPlayer.Instance != null)
            SampleTopDownPlayer.Instance.SetInteractState(InteractState.Talking);
    }

    public void ContinueStory()
    {
        if (currentStory.canContinue)
        {
            string line = currentStory.Continue().Trim();
            pendingLineContent = line;

            bool isBlocking = tagHandler.ProcessTags(currentStory.currentTags, currentNPC);

            if (isBlocking)
            {
                SetState(DialogueState.EventPaused);
            }
            else
            {
                DisplayPendingLine();
            }
        }
        else if (currentStory.currentChoices.Count == 0)
        {
            ExitDialogueMode();
        }
    }

    private void DisplayPendingLine()
    {
        if (string.IsNullOrEmpty(pendingLineContent))
        {
            ContinueStory();
            return;
        }

        string lineToDisplay = pendingLineContent;
        pendingLineContent = "";

        SetState(DialogueState.Typing);
        uiManager.DisplayLine(lineToDisplay, () =>
        {
            SetState(DialogueState.WaitingForInput);
            if (currentStory.currentChoices.Count > 0)
            {
                uiManager.ShowChoices(currentStory.currentChoices, (index) => MakeChoice(index));
            }
        });
    }

    public void ResumeDialogueAfterUI()
    {
        if (!string.IsNullOrEmpty(pendingLineContent))
        {
            DisplayPendingLine();
        }
        else
        {
            ContinueStory();
        }
    }

    public void MakeChoice(int choiceIndex)
    {
        uiManager.HideChoices();

        Choice selectedChoice = currentStory.currentChoices[choiceIndex];
        bool isBlocking = tagHandler.ProcessTags(selectedChoice.tags, currentNPC);

        currentStory.ChooseChoiceIndex(choiceIndex);

        if (isBlocking)
        {
            SetState(DialogueState.EventPaused);
        }
        else
        {
            ContinueStory();
        }
    }

    private void ExecuteFeature(string key)
    {
        SetState(DialogueState.EventPaused);
        uiManager.ExecuteFeature(key);
    }

    public void SetState(DialogueState newState)
    {
        currentState = newState;
        bool hasChoices = currentStory != null && currentStory.currentChoices.Count > 0;
        uiManager.OnStateChanged(currentState, hasChoices);
    }

    public void ExitDialogueMode()
    {
        if (currentStory != null)
        {
            try { currentStory.UnbindExternalFunction("GetAffection"); } catch { }
            try { currentStory.UnbindExternalFunction("CallFeature"); } catch { }
            dialogueVariables.StopListening(currentStory);
        }

        uiManager.HideUI(() =>
        {
            SetState(DialogueState.Idle);
            if (SampleTopDownPlayer.Instance != null)
                SampleTopDownPlayer.Instance.SetInteractState(InteractState.Idle);
        });
    }
}
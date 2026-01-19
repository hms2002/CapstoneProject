using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Ink.Runtime;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

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

        [Header("대화창 및 이름표 프레임")]
        public RectTransform dialogueTextCon;
        public RectTransform speakerFrame;

        [Header("전용 컨티뉴 아이콘")]
        public GameObject continueIcon;
        [HideInInspector] public Vector3 iconStartPos;
        [HideInInspector] public Tween iconTween;

        [Header("패널 전용 선택지 버튼")]
        public GameObject[] choiceButtons;
        [HideInInspector] public TextMeshProUGUI[] choicesText;
        [HideInInspector] public Button[] buttons;
    }

    [System.Serializable]
    public class BossDialogueUI : DialogueUI
    {
        [Header("보스 전용 연출 오브젝트")]
        public RectTransform portraitFrame;
        public RectTransform dialogueEffectGroup;

        [Header("시네마틱 패널 (검은색 패널)")]
        public RectTransform topCinematicPanel;
        public RectTransform bottomCinematicPanel;

        [Header("보스 파편 (8방향 변수)")]
        public RectTransform shardUp;
        public RectTransform shardUpRight;
        public RectTransform shardRight;
        public RectTransform shardDownRight;
        public RectTransform shardDown;
        public RectTransform shardDownLeft;
        public RectTransform shardLeft;
        public RectTransform shardUpLeft;

        [HideInInspector] public RectTransform[] shardsArray;
        [HideInInspector] public Vector2[] shardTargetAnchoredPositions;
    }

    [Header("UI 구성")]
    [SerializeField] private DialogueUI normalUI;
    [SerializeField] private BossDialogueUI bossUI;
    [SerializeField] private Animator portraitAnimator;

    [Header("연출 설정")]
    [SerializeField] private float cinematicPanelMoveDistance = 500f;
    [SerializeField] private float shardMoveDistance = 1000f;
    [SerializeField] private float bossMoveDistance = 1200f;
    [SerializeField] private float typingSpeed = 0.05f;
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float iconMoveAmount = 10f;
    [SerializeField] private float iconMoveDuration = 0.6f;
    [SerializeField] private TextAsset loadGlobalsJSON;

    private DialogueUI currentUI;
    private Story currentStory;
    private NPCData currentNPC;
    private DialogueVariables dialogueVariables;
    private Direction currentBossDir;

    private Vector2 bossPortraitTargetPos;
    private Vector2 topPanelTargetPos;
    private Vector2 bottomPanelTargetPos;

    public bool dialogueIsPlaying { get; private set; }
    private bool isTyping = false;
    private bool canContinueToNextLine = false;
    private Tween typingTween;

    private static DialogueManager instance;
    public static DialogueManager GetInstance() => instance;

    private void Awake()
    {
        instance = this;
        dialogueVariables = new DialogueVariables(loadGlobalsJSON);
    }

    private void Start()
    {
        InitializeUI(normalUI);
        InitializeUI(bossUI);

        bossUI.shardsArray = new RectTransform[] {
            bossUI.shardUp, bossUI.shardUpRight, bossUI.shardRight, bossUI.shardDownRight,
            bossUI.shardDown, bossUI.shardDownLeft, bossUI.shardLeft, bossUI.shardUpLeft
        };

        bossUI.shardTargetAnchoredPositions = new Vector2[bossUI.shardsArray.Length];
        for (int i = 0; i < bossUI.shardsArray.Length; i++)
        {
            if (bossUI.shardsArray[i] != null)
                bossUI.shardTargetAnchoredPositions[i] = bossUI.shardsArray[i].anchoredPosition;
        }

        if (bossUI.portraitFrame != null) bossPortraitTargetPos = bossUI.portraitFrame.anchoredPosition;
        if (bossUI.topCinematicPanel != null) topPanelTargetPos = bossUI.topCinematicPanel.anchoredPosition;
        if (bossUI.bottomCinematicPanel != null) bottomPanelTargetPos = bossUI.bottomCinematicPanel.anchoredPosition;

        dialogueIsPlaying = false;
        normalUI.panelObj.SetActive(false);
        bossUI.panelObj.SetActive(false);
    }

    private void InitializeUI(DialogueUI ui)
    {
        if (ui.continueIcon != null)
        {
            ui.iconStartPos = ui.continueIcon.transform.localPosition;
            ui.continueIcon.SetActive(false);
        }

        int count = ui.choiceButtons.Length;
        ui.choicesText = new TextMeshProUGUI[count];
        ui.buttons = new Button[count];

        for (int i = 0; i < count; i++)
        {
            if (ui.choiceButtons[i] == null) continue;
            ui.choicesText[i] = ui.choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            ui.buttons[i] = ui.choiceButtons[i].GetComponent<Button>();
            int index = i;
            ui.buttons[i].onClick.RemoveAllListeners();
            ui.buttons[i].onClick.AddListener(() => MakeChoice(index));
            Navigation nav = new Navigation { mode = Navigation.Mode.Automatic };
            ui.buttons[i].navigation = nav;
            ui.choiceButtons[i].SetActive(false);
        }
    }

    private void Update()
    {
        if (!dialogueIsPlaying || currentStory == null) return;
        bool skipInput = Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) ||
                         Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape);

        if (isTyping && skipInput)
        {
            if (typingTween != null && typingTween.IsActive()) typingTween.Complete();
            return;
        }

        if (canContinueToNextLine)
        {
            if (currentStory.currentChoices.Count == 0 && skipInput) ContinueStory();
            else if (currentStory.currentChoices.Count > 0 && Input.GetKeyDown(KeyCode.F))
                ConfirmSelectionWithKeyboard();
        }
    }

    private void ConfirmSelectionWithKeyboard()
    {
        if (EventSystem.current == null) return;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected != null && selected.activeInHierarchy)
        {
            Button btn = selected.GetComponent<Button>();
            if (btn != null) btn.onClick.Invoke();
        }
    }

    public void EnterDialogueMode(TextAsset inkJSON, NPCData data, Direction bossDir = Direction.Left)
    {
        currentNPC = data;
        currentBossDir = bossDir;
        if (TempPlayer.Instance != null) TempPlayer.Instance.SetInteractState(InteractState.Talking);

        currentUI = data.isBoss ? bossUI : normalUI;

        currentStory = new Story(inkJSON.text);
        if (dialogueVariables != null) dialogueVariables.StartListening(currentStory);
        currentStory.BindExternalFunction("GetAffection", () => AffectionManager.Instance.GetLevel(currentNPC.id));

        dialogueIsPlaying = true;

        if (data.isBoss) StartBossSequence();
        else
        {
            normalUI.panelObj.SetActive(true);
            StartNormalSequence();
            ContinueStory();
        }
    }

    private void StartNormalSequence()
    {
        normalUI.canvasGroup.DOKill();
        normalUI.canvasGroup.alpha = 0;
        normalUI.canvasGroup.DOFade(1f, fadeDuration);
    }

    private void StartBossSequence()
    {
        bossUI.topCinematicPanel.gameObject.SetActive(false);
        bossUI.bottomCinematicPanel.gameObject.SetActive(false);
        bossUI.dialogueEffectGroup.gameObject.SetActive(false);
        bossUI.portraitFrame.gameObject.SetActive(false);
        bossUI.dialogueTextCon.gameObject.SetActive(false);
        bossUI.speakerFrame.gameObject.SetActive(false);

        bossUI.panelObj.SetActive(true);

        Sequence bossSeq = DOTween.Sequence();

        // 1. 시네마틱 패널
        bossSeq.AppendCallback(() => {
            bossUI.topCinematicPanel.gameObject.SetActive(true);
            bossUI.bottomCinematicPanel.gameObject.SetActive(true);
            bossUI.topCinematicPanel.anchoredPosition = topPanelTargetPos + Vector2.up * cinematicPanelMoveDistance;
            bossUI.bottomCinematicPanel.anchoredPosition = bottomPanelTargetPos + Vector2.down * cinematicPanelMoveDistance;
        });
        bossSeq.Append(bossUI.topCinematicPanel.DOAnchorPos(topPanelTargetPos, 0.6f).SetEase(Ease.OutCubic));
        bossSeq.Join(bossUI.bottomCinematicPanel.DOAnchorPos(bottomPanelTargetPos, 0.6f).SetEase(Ease.OutCubic));
        bossSeq.AppendInterval(0.1f);

        // 2. Shard 연출
        bossSeq.AppendCallback(() => {
            bossUI.dialogueEffectGroup.gameObject.SetActive(true);
            Vector2[] dirs = {
                Vector2.up, new Vector2(1,1).normalized, Vector2.right, new Vector2(1,-1).normalized,
                Vector2.down, new Vector2(-1,-1).normalized, Vector2.left, new Vector2(-1,1).normalized
            };
            for (int i = 0; i < bossUI.shardsArray.Length; i++)
            {
                if (bossUI.shardsArray[i] == null) continue;
                bossUI.shardsArray[i].anchoredPosition = bossUI.shardTargetAnchoredPositions[i] + (dirs[i] * shardMoveDistance);
            }
        });
        for (int i = 0; i < bossUI.shardsArray.Length; i++)
        {
            if (bossUI.shardsArray[i] == null) continue;
            bossSeq.Join(bossUI.shardsArray[i].DOAnchorPos(bossUI.shardTargetAnchoredPositions[i], 0.7f).SetEase(Ease.OutQuart));
        }
        bossSeq.AppendInterval(0.15f);

        // 3. 보스 일러스트
        bossSeq.AppendCallback(() => {
            bossUI.portraitFrame.gameObject.SetActive(true);
            Vector2 startOffset = Vector2.zero;
            switch (currentBossDir)
            {
                case Direction.Left: startOffset = Vector2.left; break;
                case Direction.Right: startOffset = Vector2.right; break;
                case Direction.Up: startOffset = Vector2.up; break;
                case Direction.Down: startOffset = Vector2.down; break;
            }
            bossUI.portraitFrame.anchoredPosition = bossPortraitTargetPos + (startOffset * bossMoveDistance);
        });
        bossSeq.Append(bossUI.portraitFrame.DOAnchorPos(bossPortraitTargetPos, 0.8f).SetEase(Ease.OutCubic));
        bossSeq.AppendInterval(0.1f);

        // 4. 대화창 & 이름표
        bossSeq.AppendCallback(() => {
            bossUI.dialogueTextCon.gameObject.SetActive(true);
            bossUI.speakerFrame.gameObject.SetActive(true);
            bossUI.dialogueTextCon.localScale = Vector3.zero;
            bossUI.speakerFrame.localScale = Vector3.zero;
        });
        bossSeq.Append(bossUI.dialogueTextCon.DOScale(1f, 0.4f).SetEase(Ease.OutQuart));
        bossSeq.Join(bossUI.speakerFrame.DOScale(1f, 0.4f).SetEase(Ease.OutQuart));

        bossSeq.OnComplete(() => ContinueStory());
    }

    private void ContinueStory()
    {
        if (currentStory != null && currentStory.canContinue)
        {
            string line = currentStory.Continue();
            HandleTags(currentStory.currentTags);
            DisplayLine(line);
        }
        else ExitDialogueMode();
    }

    private void DisplayLine(string line)
    {
        currentUI.dialogueText.enableAutoSizing = true;
        currentUI.dialogueText.text = line;
        currentUI.dialogueText.ForceMeshUpdate();
        float finalSize = currentUI.dialogueText.fontSize;
        currentUI.dialogueText.enableAutoSizing = false;
        currentUI.dialogueText.fontSize = finalSize;
        currentUI.dialogueText.text = "";

        isTyping = true;
        canContinueToNextLine = false;
        ManageContinueIcon(currentUI, false);
        HideAllChoices();

        if (typingTween != null) typingTween.Kill();
        typingTween = currentUI.dialogueText.DOText(line, line.Length * typingSpeed)
            .SetEase(Ease.Linear)
            .OnComplete(() => {
                isTyping = false;
                canContinueToNextLine = true;
                if (currentStory.currentChoices.Count == 0) ManageContinueIcon(currentUI, true);
                DisplayChoices();
            });
    }

    private void DisplayChoices()
    {
        List<Choice> currentChoices = currentStory.currentChoices;
        if (currentChoices.Count == 0) return;
        ManageContinueIcon(currentUI, false);
        for (int i = 0; i < currentChoices.Count; i++)
        {
            if (i >= currentUI.choiceButtons.Length) break;
            currentUI.choiceButtons[i].SetActive(true);
            currentUI.choicesText[i].text = currentChoices[i].text;
            currentUI.choiceButtons[i].transform.localScale = Vector3.zero;
            currentUI.choiceButtons[i].transform.DOScale(1f, 0.3f).SetEase(Ease.OutQuart);
        }
        StartCoroutine(SelectFirstChoice());
    }

    private IEnumerator SelectFirstChoice()
    {
        if (EventSystem.current == null) yield break;
        EventSystem.current.SetSelectedGameObject(null);
        yield return new WaitForEndOfFrame();
        if (currentUI.choiceButtons.Length > 0 && currentUI.choiceButtons[0].activeSelf)
            EventSystem.current.SetSelectedGameObject(currentUI.choiceButtons[0]);
    }

    private void HideAllChoices()
    {
        foreach (var btn in normalUI.choiceButtons) if (btn != null) btn.SetActive(false);
        foreach (var btn in bossUI.choiceButtons) if (btn != null) btn.SetActive(false);
    }

    public void MakeChoice(int choiceIndex)
    {
        if (canContinueToNextLine && !isTyping && currentStory != null)
        {
            currentStory.ChooseChoiceIndex(choiceIndex);
            ContinueStory();
        }
    }

    private void ManageContinueIcon(DialogueUI ui, bool show)
    {
        if (ui.continueIcon == null) return;
        if (show)
        {
            ui.continueIcon.SetActive(true);
            if (ui.iconTween != null) ui.iconTween.Kill();
            ui.continueIcon.transform.localPosition = ui.iconStartPos;
            ui.iconTween = ui.continueIcon.transform
                .DOLocalMoveY(ui.iconStartPos.y - iconMoveAmount, iconMoveDuration)
                .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }
        else { if (ui.iconTween != null) ui.iconTween.Kill(); ui.continueIcon.SetActive(false); }
    }

    private void HandleTags(List<string> tags)
    {
        if (tags == null) return;
        foreach (string t in tags)
        {
            string[] s = t.Split(':');
            if (s.Length != 2) continue;
            string key = s[0].Trim(); string val = s[1].Trim();
            if (key == "speaker" && currentUI.nameText != null) currentUI.nameText.text = val;
            else if (key == "portrait" && portraitAnimator != null) portraitAnimator.Play(val);
            else if (key == "add_aff") AffectionManager.Instance.AddLevel(currentNPC, int.Parse(val));
            else if (key == "scene") SceneManager.LoadScene(val);
        }
    }

    // [최종 수정] ExitDialogueMode에 2배 빠른 역순 보스 연출 추가 [cite: 1, 2, 2026-01-15]
    private void ExitDialogueMode()
    {
        if (currentStory != null)
        {
            currentStory.UnbindExternalFunction("GetAffection");
            if (dialogueVariables != null) dialogueVariables.StopListening(currentStory);
        }

        ManageContinueIcon(normalUI, false);
        ManageContinueIcon(bossUI, false);

        if (currentUI == bossUI)
        {
            StartCoroutine(ExitBossSequence());
        }
        else
        {
            normalUI.canvasGroup.DOFade(0f, fadeDuration).OnComplete(() => {
                FinishDialogueExit();
            });
        }
    }

    // [지시사항] 연출 역순 재생 및 2배속 처리
    private IEnumerator ExitBossSequence()
    {
        Sequence exitSeq = DOTween.Sequence();

        // 1. 대화창 & 이름표 축소 (0.4s -> 0.2s)
        exitSeq.Append(bossUI.dialogueTextCon.DOScale(0f, 0.2f).SetEase(Ease.InQuart));
        exitSeq.Join(bossUI.speakerFrame.DOScale(0f, 0.2f).SetEase(Ease.InQuart));

        // 2. 보스 일러스트 퇴장 (0.8s -> 0.4s)
        Vector2 startOffset = Vector2.zero;
        switch (currentBossDir)
        {
            case Direction.Left: startOffset = Vector2.left; break;
            case Direction.Right: startOffset = Vector2.right; break;
            case Direction.Up: startOffset = Vector2.up; break;
            case Direction.Down: startOffset = Vector2.down; break;
        }
        exitSeq.Append(bossUI.portraitFrame.DOAnchorPos(bossPortraitTargetPos + (startOffset * bossMoveDistance), 0.4f).SetEase(Ease.InCubic));

        // 3. 파편(Shards) 퇴장 (0.7s -> 0.35s)
        Vector2[] dirs = {
            Vector2.up, new Vector2(1,1).normalized, Vector2.right, new Vector2(1,-1).normalized,
            Vector2.down, new Vector2(-1,-1).normalized, Vector2.left, new Vector2(-1,1).normalized
        };
        for (int i = 0; i < bossUI.shardsArray.Length; i++)
        {
            if (bossUI.shardsArray[i] == null) continue;
            exitSeq.Join(bossUI.shardsArray[i].DOAnchorPos(bossUI.shardTargetAnchoredPositions[i] + (dirs[i] * shardMoveDistance), 0.35f).SetEase(Ease.InQuart));
        }

        // 4. 시네마틱 패널 퇴장 (0.6s -> 0.3s)
        exitSeq.Append(bossUI.topCinematicPanel.DOAnchorPos(topPanelTargetPos + Vector2.up * cinematicPanelMoveDistance, 0.3f).SetEase(Ease.InCubic));
        exitSeq.Join(bossUI.bottomCinematicPanel.DOAnchorPos(bottomPanelTargetPos + Vector2.down * cinematicPanelMoveDistance, 0.3f).SetEase(Ease.InCubic));

        yield return exitSeq.WaitForCompletion();
        FinishDialogueExit();
    }

    private void FinishDialogueExit()
    {
        dialogueIsPlaying = false;
        currentStory = null;
        normalUI.panelObj.SetActive(false);
        bossUI.panelObj.SetActive(false);
        if (TempPlayer.Instance != null) TempPlayer.Instance.SetInteractState(InteractState.Idle);
    }

    public void OnApplicationQuit() { if (dialogueVariables != null) dialogueVariables.SaveVariables(); }
}
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
        [Header("보스 전용: 호감도 UI")]
        public AffectionUI affectionUI;

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

    // =================================================================
    // [System] 동적 초상화 시스템
    // =================================================================
    [Header("동적 초상화 시스템")]
    public GameObject portraitPrefab;
    public Transform portraitContainer;

    // [Changed] 리스트 대신 전용 데이터베이스 에셋 연결
    public NPCDatabase npcDatabase;

    private Dictionary<int, PortraitController> portraitMap = new Dictionary<int, PortraitController>();

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
    private NPCFeatureController currentFeatureController;

    private DialogueVariables dialogueVariables;
    private Direction currentBossDir;

    private Vector2 bossPortraitTargetPos;
    private Vector2 topPanelTargetPos;
    private Vector2 bottomPanelTargetPos;

    // =================================================================
    // [State] 상태 변수
    // =================================================================
    public bool dialogueIsPlaying { get; private set; }
    public bool IsEventRunning { get; set; } = false;

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
        canContinueToNextLine = false;
        isTyping = false;

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
        if (IsEventRunning) return;

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

    public void EnterDialogueMode(TextAsset inkJSON, NPCData data, Direction bossDir = Direction.Left, NPCFeatureController featureController = null)
    {
        // 1. 상태 변수 강제 초기화
        dialogueIsPlaying = true;
        canContinueToNextLine = false;
        isTyping = false;

        if (typingTween != null && typingTween.IsActive()) typingTween.Kill();

        currentNPC = data;
        currentBossDir = bossDir;
        currentFeatureController = featureController;

        if (TempPlayer.Instance != null) TempPlayer.Instance.SetInteractState(InteractState.Talking);
        AffectionManager.Instance.SetCurrentNPC(currentNPC.id);

        if (data.isBoss)
        {
            currentUI = bossUI;
            AffectionManager.Instance.SetLinkedUI(bossUI.affectionUI);
        }
        else
        {
            currentUI = normalUI;
            AffectionManager.Instance.SetLinkedUI(null);
        }

        // 텍스트 잔상 제거 및 이름 즉시 설정
        currentUI.dialogueText.text = "";
        if (currentUI.nameText != null)
        {
            currentUI.nameText.text = data.npcName;
        }

        currentStory = new Story(inkJSON.text);
        if (dialogueVariables != null) dialogueVariables.StartListening(currentStory);

        currentStory.BindExternalFunction("GetAffection", () => {
            return AffectionManager.Instance.GetAffection();
        });

        SpawnPortrait(data.id, "center");

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

        if (bossUI.affectionUI != null) bossUI.affectionUI.gameObject.SetActive(false);

        bossUI.panelObj.SetActive(true);

        Sequence bossSeq = DOTween.Sequence();

        bossSeq.AppendCallback(() => {
            bossUI.topCinematicPanel.gameObject.SetActive(true);
            bossUI.bottomCinematicPanel.gameObject.SetActive(true);
            bossUI.topCinematicPanel.anchoredPosition = topPanelTargetPos + Vector2.up * cinematicPanelMoveDistance;
            bossUI.bottomCinematicPanel.anchoredPosition = bottomPanelTargetPos + Vector2.down * cinematicPanelMoveDistance;
        });
        bossSeq.Append(bossUI.topCinematicPanel.DOAnchorPos(topPanelTargetPos, 0.6f).SetEase(Ease.OutCubic));
        bossSeq.Join(bossUI.bottomCinematicPanel.DOAnchorPos(bottomPanelTargetPos, 0.6f).SetEase(Ease.OutCubic));
        bossSeq.AppendInterval(0.1f);

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

        bossSeq.AppendCallback(() => {
            bossUI.dialogueTextCon.gameObject.SetActive(true);
            bossUI.speakerFrame.gameObject.SetActive(true);

            if (bossUI.affectionUI != null)
            {
                bossUI.affectionUI.gameObject.SetActive(true);
                int currentAff = AffectionManager.Instance.GetAffection();
                bossUI.affectionUI.Setup(currentAff);
                bossUI.affectionUI.transform.localScale = Vector3.zero;
            }

            bossUI.dialogueTextCon.localScale = Vector3.zero;
            bossUI.speakerFrame.localScale = Vector3.zero;
        });

        // Affection UI 동시 등장 연출
        bossSeq.Append(bossUI.dialogueTextCon.DOScale(1f, 0.4f).SetEase(Ease.OutQuart));
        bossSeq.Join(bossUI.speakerFrame.DOScale(1f, 0.4f).SetEase(Ease.OutQuart));

        if (bossUI.affectionUI != null)
        {
            bossSeq.Join(bossUI.affectionUI.transform.DOScale(1f, 0.4f).SetEase(Ease.OutQuart));
        }

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
            if (s.Length == 0) continue;

            string first = s[0].Trim().ToLower();

            if (first == "enter" && s.Length >= 2)
            {
                if (int.TryParse(s[1].Trim(), out int spawnId))
                {
                    string posKey = (s.Length > 2) ? s[2].Trim() : "center";
                    SpawnPortrait(spawnId, posKey);
                }
                continue;
            }

            if (first == "exit" && s.Length >= 2)
            {
                if (int.TryParse(s[1].Trim(), out int despawnId))
                {
                    DespawnPortrait(despawnId);
                }
                continue;
            }

            if (int.TryParse(first, out int targetId))
            {
                if (portraitMap.TryGetValue(targetId, out PortraitController portrait))
                {
                    if (s.Length >= 3)
                    {
                        string command = s[1].Trim().ToLower();
                        string val = s[2].Trim();

                        if (command == "face") portrait.SetExpression(val);
                        else if (command == "act") portrait.PlayAction(val);
                        else if (command == "move") portrait.MovePosition(val);
                        else if (command == "emote") portrait.ShowEmote(val);
                    }
                }
                continue;
            }

            if (s.Length >= 2)
            {
                string key = first;
                string val = s[1].Trim();

                if ((key == "face" || key == "act" || key == "emote") && currentNPC != null)
                {
                    if (portraitMap.TryGetValue(currentNPC.id, out PortraitController curPortrait))
                    {
                        if (key == "face") curPortrait.SetExpression(val);
                        else if (key == "act") curPortrait.PlayAction(val);
                        else if (key == "emote") curPortrait.ShowEmote(val);
                    }
                    continue;
                }

                if (key == "speaker" && currentUI.nameText != null) currentUI.nameText.text = val;
                else if (key == "portrait" && portraitAnimator != null) portraitAnimator.Play(val);
                else if (key == "add_aff") AffectionManager.Instance.AddAffection(currentNPC, int.Parse(val));
                else if (key == "scene") SceneManager.LoadScene(val);
                else if (key == "feature")
                {
                    if (currentFeatureController != null)
                    {
                        ExitDialogueMode();
                        currentFeatureController.ExecuteFeature(val);
                    }
                }
            }
        }
    }

    private void SpawnPortrait(int id, string position)
    {
        if (portraitMap.ContainsKey(id)) return;

        // [Updated] 데이터베이스 에셋에서 검색
        if (npcDatabase == null)
        {
            Debug.LogError("NPCDatabase가 연결되지 않았습니다!");
            return;
        }

        NPCData data = npcDatabase.GetNPC(id);
        if (data == null)
        {
            Debug.LogError($"[DialogueManager] NPC Database에서 ID {id}를 찾을 수 없습니다.");
            return;
        }

        if (portraitPrefab == null || portraitContainer == null) return;

        GameObject newObj = Instantiate(portraitPrefab, portraitContainer);
        PortraitController ctrl = newObj.GetComponent<PortraitController>();

        ctrl.ApplyNPCData(data);
        ctrl.SetInitialPosition(position);
        ctrl.EnterAnimation();

        portraitMap.Add(id, ctrl);
    }

    private void DespawnPortrait(int id)
    {
        if (portraitMap.TryGetValue(id, out PortraitController ctrl))
        {
            portraitMap.Remove(id);
            if (ctrl != null)
            {
                ctrl.ExitAnimationAndDestroy();
            }
        }
    }

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

    private IEnumerator ExitBossSequence()
    {
        Sequence exitSeq = DOTween.Sequence();

        exitSeq.Append(bossUI.dialogueTextCon.DOScale(0f, 0.2f).SetEase(Ease.InQuart));
        exitSeq.Join(bossUI.speakerFrame.DOScale(0f, 0.2f).SetEase(Ease.InQuart));

        // Affection UI 퇴장 동시 연출
        if (bossUI.affectionUI != null)
        {
            exitSeq.Join(bossUI.affectionUI.transform.DOScale(0f, 0.2f).SetEase(Ease.InQuart));
        }

        Vector2 startOffset = Vector2.zero;
        switch (currentBossDir)
        {
            case Direction.Left: startOffset = Vector2.left; break;
            case Direction.Right: startOffset = Vector2.right; break;
            case Direction.Up: startOffset = Vector2.up; break;
            case Direction.Down: startOffset = Vector2.down; break;
        }
        exitSeq.Append(bossUI.portraitFrame.DOAnchorPos(bossPortraitTargetPos + (startOffset * bossMoveDistance), 0.4f).SetEase(Ease.InCubic));

        Vector2[] dirs = {
            Vector2.up, new Vector2(1,1).normalized, Vector2.right, new Vector2(1,-1).normalized,
            Vector2.down, new Vector2(-1,-1).normalized, Vector2.left, new Vector2(-1,1).normalized
        };
        for (int i = 0; i < bossUI.shardsArray.Length; i++)
        {
            if (bossUI.shardsArray[i] == null) continue;
            exitSeq.Join(bossUI.shardsArray[i].DOAnchorPos(bossUI.shardTargetAnchoredPositions[i] + (dirs[i] * shardMoveDistance), 0.35f).SetEase(Ease.InQuart));
        }

        exitSeq.Append(bossUI.topCinematicPanel.DOAnchorPos(topPanelTargetPos + Vector2.up * cinematicPanelMoveDistance, 0.3f).SetEase(Ease.InCubic));
        exitSeq.Join(bossUI.bottomCinematicPanel.DOAnchorPos(bottomPanelTargetPos + Vector2.down * cinematicPanelMoveDistance, 0.3f).SetEase(Ease.InCubic));

        yield return exitSeq.WaitForCompletion();
        FinishDialogueExit();
    }

    private void FinishDialogueExit()
    {
        foreach (var ctrl in portraitMap.Values) { if (ctrl != null) ctrl.ExitAnimationAndDestroy(); }
        portraitMap.Clear();

        dialogueIsPlaying = false;
        canContinueToNextLine = false;
        isTyping = false;

        if (typingTween != null) typingTween.Kill();

        currentStory = null;

        normalUI.panelObj.SetActive(false);
        bossUI.panelObj.SetActive(false);

        if (bossUI.affectionUI != null) bossUI.affectionUI.gameObject.SetActive(false);

        if (TempPlayer.Instance != null) TempPlayer.Instance.SetInteractState(InteractState.Idle);
    }

    public void OnApplicationQuit() { if (dialogueVariables != null) dialogueVariables.SaveVariables(); }
}
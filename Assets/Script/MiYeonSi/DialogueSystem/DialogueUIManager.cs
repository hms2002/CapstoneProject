using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Ink.Runtime;

public class DialogueUIManager : MonoBehaviour
{
    [Header("UI 구성")]
    [SerializeField] private DialogueManager.DialogueUI normalUI;
    [SerializeField] private DialogueManager.BossDialogueUI bossUI;

    [Header("일러스트 시스템")]
    public GameObject portraitPrefab;
    public Transform portraitContainer;
    private Dictionary<int, PortraitController> portraitMap = new Dictionary<int, PortraitController>();

    [Header("연출 설정")]
    [SerializeField] private float cinematicPanelMoveDistance = 500f;
    [SerializeField] private float bossMoveDistance = 1200f;
    [SerializeField] private float typingSpeed = 0.02f;
    [SerializeField] private float fadeDuration = 0.3f;

    private DialogueManager.DialogueUI currentUI;
    private NPCFeatureController currentFeature;
    private Tween typingTween;
    private System.Action onTypingComplete;

    private void Awake()
    {
        InitUIComponents(normalUI);
        InitUIComponents(bossUI);

        // 보스 UI 파편 배열 할당
        bossUI.shardsArray = new RectTransform[] {
            bossUI.shardUp, bossUI.shardUpRight, bossUI.shardRight, bossUI.shardDownRight,
            bossUI.shardDown, bossUI.shardDownLeft, bossUI.shardLeft, bossUI.shardUpLeft
        };

        // [중요] 파편들의 "원래 위치(도착점)"를 미리 저장해둡니다.
        bossUI.shardTargetAnchoredPositions = new Vector2[bossUI.shardsArray.Length];
        for (int i = 0; i < bossUI.shardsArray.Length; i++)
        {
            if (bossUI.shardsArray[i] != null)
                bossUI.shardTargetAnchoredPositions[i] = bossUI.shardsArray[i].anchoredPosition;
        }
    }

    private void InitUIComponents(DialogueManager.DialogueUI ui)
    {
        if (ui.continueIcon != null) ui.iconStartPos = ui.continueIcon.transform.localPosition;

        int count = ui.choiceButtons.Length;
        ui.choicesText = new TextMeshProUGUI[count];
        ui.buttons = new Button[count];

        for (int i = 0; i < count; i++)
        {
            if (ui.choiceButtons[i] == null) continue;
            ui.choicesText[i] = ui.choiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            ui.buttons[i] = ui.choiceButtons[i].GetComponent<Button>();
            ui.choiceButtons[i].SetActive(false);
        }
    }

    public DialogueManager.DialogueUI GetBossUI() => bossUI;

    public void SetupDialogue(NPCData data, NPCFeatureController feature)
    {
        currentFeature = feature;
        currentUI = data.isBoss ? bossUI : normalUI;

        normalUI.panelObj.SetActive(false);
        bossUI.panelObj.SetActive(false);
        currentUI.dialogueText.text = "";
        if (currentUI.nameText != null) currentUI.nameText.text = data.npcName;

        ClearPortraits();
    }

    public void ShowNormalPanel()
    {
        normalUI.panelObj.SetActive(true);
        normalUI.canvasGroup.alpha = 1f;
    }

    // [핵심 연출 로직 수정]
    public void StartBossSequence(DialogueManager.Direction dir, System.Action onComplete)
    {
        bossUI.panelObj.SetActive(true);
        bossUI.canvasGroup.alpha = 1f;

        // 1. 모든 요소 숨기기 (초기화)
        bossUI.dialogueEffectGroup.gameObject.SetActive(false); // 파편 그룹 숨김
        bossUI.portraitFrame.gameObject.SetActive(false);       // 포트레이트 숨김
        bossUI.affectionUI.gameObject.SetActive(false);         // 호감도 UI 숨김
        bossUI.dialogueTextCon.gameObject.SetActive(false);     // 텍스트창 숨김
        bossUI.speakerFrame.gameObject.SetActive(false);        // 화자 프레임 숨김

        // 패널 초기 위치 (화면 밖)
        bossUI.topCinematicPanel.anchoredPosition = new Vector2(0, cinematicPanelMoveDistance);
        bossUI.bottomCinematicPanel.anchoredPosition = new Vector2(0, -cinematicPanelMoveDistance);

        Sequence seq = DOTween.Sequence();

        // =================================================================================
        // STEP 1. 시네마틱 패널 등장 (위/아래에서 닫힘)
        // =================================================================================
        seq.Append(bossUI.topCinematicPanel.DOAnchorPosY(0, 0.6f).SetEase(Ease.OutCubic));
        seq.Join(bossUI.bottomCinematicPanel.DOAnchorPosY(0, 0.6f).SetEase(Ease.OutCubic));

        // =================================================================================
        // STEP 2. 파편(Shard) 등장 (패널이 다 닫힌 후 실행됨)
        // =================================================================================

        // 파편 그룹 활성화 (이제 보이기 시작함)
        seq.AppendCallback(() => bossUI.dialogueEffectGroup.gameObject.SetActive(true));

        for (int i = 0; i < bossUI.shardsArray.Length; i++)
        {
            if (bossUI.shardsArray[i] != null)
            {
                // 원래 저장된 위치(Target)를 가져옴
                Vector2 targetPos = bossUI.shardTargetAnchoredPositions[i];

                // 시작 위치 계산: 원래 위치에서 중심으로부터 2배 더 멀어진 곳
                // (이렇게 하면 화면 바깥쪽에서 안쪽으로 들어오는 효과가 납니다)
                Vector2 startPos = targetPos * 2.5f;

                // 첫 번째 파편은 Append로 시간을 소모하게 하고, 나머지는 Join으로 같이 실행
                if (i == 0)
                {
                    // .From(startPos)를 쓰면: startPos에서 시작해서 -> 현재 설정된 위치(targetPos)로 이동함
                    // 즉, 최종적으로는 Awake에서 저장한 원래 위치에 정확히 멈춤
                    seq.Append(bossUI.shardsArray[i].DOAnchorPos(targetPos, 0.6f)
                        .From(startPos)
                        .SetEase(Ease.OutBack));
                }
                else
                {
                    seq.Join(bossUI.shardsArray[i].DOAnchorPos(targetPos, 0.6f)
                        .From(startPos)
                        .SetEase(Ease.OutBack));
                }
            }
        }

        // =================================================================================
        // STEP 3. Portrait 등장 (파편 연출 후 실행)
        // =================================================================================
        float startX = (dir == DialogueManager.Direction.Right) ? bossMoveDistance : -bossMoveDistance;

        seq.AppendCallback(() => {
            bossUI.portraitFrame.anchoredPosition = new Vector2(startX, 0);
            bossUI.portraitFrame.gameObject.SetActive(true);
        });

        // 슬라이드 등장
        seq.Append(bossUI.portraitFrame.DOAnchorPosX(0, 0.8f).SetEase(Ease.OutCubic));

        // =================================================================================
        // STEP 4. UI 요소 등장 (Portrait 등장 후 실행)
        // =================================================================================
        seq.AppendCallback(() => {
            // 호감도 UI
            if (bossUI.affectionUI != null)
            {
                bossUI.affectionUI.gameObject.SetActive(true);
                bossUI.affectionUI.Setup(AffectionManager.Instance.GetAffection());

                // 팝업 효과
                bossUI.affectionUI.transform.localScale = Vector3.zero;
                bossUI.affectionUI.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
            }

            // 대화창 구성요소
            bossUI.dialogueTextCon.gameObject.SetActive(true);
            bossUI.speakerFrame.gameObject.SetActive(true);

            // 페이드 인
            CanvasGroup textGroup = bossUI.dialogueTextCon.GetComponent<CanvasGroup>();
            if (textGroup != null)
            {
                textGroup.alpha = 0f;
                textGroup.DOFade(1f, 0.4f);
            }
        });

        // UI 안정화 대기
        seq.AppendInterval(0.3f);

        // =================================================================================
        // STEP 5. 완료 (대사 시작)
        // =================================================================================
        seq.OnComplete(() => onComplete?.Invoke());
    }

    public void DisplayLine(string line, System.Action onComplete)
    {
        onTypingComplete = onComplete;
        currentUI.dialogueText.text = line;
        currentUI.dialogueText.maxVisibleCharacters = 0;

        typingTween = DOTween.To(() => currentUI.dialogueText.maxVisibleCharacters,
            x => currentUI.dialogueText.maxVisibleCharacters = x, line.Length, line.Length * typingSpeed)
            .SetEase(Ease.Linear)
            .OnComplete(() => onTypingComplete?.Invoke());
    }

    public void CompleteTyping()
    {
        typingTween?.Kill();
        currentUI.dialogueText.maxVisibleCharacters = currentUI.dialogueText.text.Length;
        onTypingComplete?.Invoke();
    }

    public void ShowChoices(List<Choice> choices, System.Action<int> onSelected)
    {
        HideChoices();
        for (int i = 0; i < choices.Count && i < currentUI.choiceButtons.Length; i++)
        {
            currentUI.choiceButtons[i].SetActive(true);
            currentUI.choicesText[i].text = choices[i].text;
            int index = i;
            currentUI.buttons[i].onClick.RemoveAllListeners();
            currentUI.buttons[i].onClick.AddListener(() => {
                HideChoices();
                onSelected.Invoke(index);
            });
        }
        StartCoroutine(SelectFirstChoice());
    }

    private IEnumerator SelectFirstChoice()
    {
        yield return new WaitForEndOfFrame();
        if (EventSystem.current != null && currentUI.choiceButtons[0].activeSelf)
            EventSystem.current.SetSelectedGameObject(currentUI.choiceButtons[0]);
    }

    public void HideChoices()
    {
        foreach (var btn in currentUI.choiceButtons) if (btn != null) btn.SetActive(false);
    }

    public void OnStateChanged(DialogueState state, bool hasChoices)
    {
        bool showIcon = (state == DialogueState.WaitingForInput && !hasChoices);
        if (currentUI.continueIcon != null)
        {
            currentUI.continueIcon.SetActive(showIcon);
            if (showIcon)
            {
                currentUI.continueIcon.transform.DOKill();
                currentUI.continueIcon.transform.localPosition = currentUI.iconStartPos;
                currentUI.continueIcon.transform.DOLocalMoveY(currentUI.iconStartPos.y - 10f, 0.6f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
            }
        }
    }

    public void ExecuteFeature(string key) { if (currentFeature != null) currentFeature.ExecuteFeature(key); }

    public void HideUI(System.Action onComplete)
    {
        currentUI.canvasGroup.DOFade(0f, fadeDuration).OnComplete(() => {
            normalUI.panelObj.SetActive(false);
            bossUI.panelObj.SetActive(false);
            onComplete?.Invoke();
        });
    }

    public void SpawnPortrait(NPCData data, string pos)
    {
        if (data == null || portraitMap.ContainsKey(data.id)) return;
        GameObject go = Instantiate(portraitPrefab, portraitContainer);
        PortraitController ctrl = go.GetComponent<PortraitController>();
        ctrl.ApplyNPCData(data); ctrl.SetInitialPosition(pos); ctrl.EnterAnimation();
        portraitMap.Add(data.id, ctrl);
    }

    public void DespawnPortrait(int id) { if (portraitMap.TryGetValue(id, out PortraitController ctrl)) { ctrl.ExitAnimationAndDestroy(); portraitMap.Remove(id); } }
    public PortraitController GetPortrait(int id) => portraitMap.ContainsKey(id) ? portraitMap[id] : null;
    private void ClearPortraits() { foreach (var ctrl in portraitMap.Values) if (ctrl != null) Destroy(ctrl.gameObject); portraitMap.Clear(); }
}
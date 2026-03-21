using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System;
using System.Collections.Generic;

public class DialogueView : MonoBehaviour
{
    [Header("UI 그룹 (CanvasGroup 필요)")]
    [SerializeField] private CanvasGroup textBoxGroup;
    [SerializeField] private CanvasGroup affectionGroup;

    [Header("텍스트 컴포넌트")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Header("연출 아이콘")]
    [SerializeField] private GameObject continueIcon;

    [Header("선택지 UI")]
    [SerializeField] private Transform choiceContainer;
    [SerializeField] private GameObject choiceButtonPrefab;
    [SerializeField] private Color normalChoiceColor = Color.gray;
    [SerializeField] private Color selectedChoiceColor = Color.white;

    private Tween typingTween;
    private List<GameObject> activeChoiceButtons = new List<GameObject>();

    private int currentChoiceIndex = 0;
    private Action<int> onChoiceSelectedCallback;

    private void Awake()
    {
        if (textBoxGroup != null)
        {
            textBoxGroup.alpha = 0f;
            textBoxGroup.gameObject.SetActive(false);
        }

        if (affectionGroup != null)
        {
            affectionGroup.alpha = 0f;
            affectionGroup.gameObject.SetActive(false);
        }

        if (continueIcon != null)
        {
            continueIcon.SetActive(false);
            RectTransform iconRect = continueIcon.GetComponent<RectTransform>();
            if (iconRect != null)
            {
                iconRect.DOAnchorPosY(iconRect.anchoredPosition.y - 10f, 0.5f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
        }

        ClearChoices();
        ClearText(); // 시작 시에도 깔끔하게 비움
    }

    // [핵심 추가] 텍스트 박스와 이름표를 깔끔하게 비워주는 함수
    public void ClearText()
    {
        if (nameText != null) nameText.text = "";
        if (dialogueText != null) dialogueText.text = "";
    }

    public void ShowUI(bool isBoss, Action onComplete = null)
    {
        if (textBoxGroup != null)
        {
            textBoxGroup.gameObject.SetActive(true);

            Sequence seq = DOTween.Sequence();
            seq.Append(textBoxGroup.DOFade(1f, 0.25f));

            if (isBoss && affectionGroup != null)
            {
                affectionGroup.gameObject.SetActive(true);
                seq.Join(affectionGroup.DOFade(1f, 0.25f));
            }
            else if (!isBoss && affectionGroup != null)
            {
                affectionGroup.gameObject.SetActive(false);
                affectionGroup.alpha = 0f;
            }

            seq.OnComplete(() => onComplete?.Invoke());
        }
        else onComplete?.Invoke();
    }

    public void TypeText(string speakerName, string text, Action onComplete = null)
    {
        if (nameText != null) nameText.text = speakerName;
        if (dialogueText != null) dialogueText.text = "";

        if (continueIcon != null) continueIcon.SetActive(false);

        if (typingTween != null) typingTween.Kill();

        if (dialogueText != null)
        {
            typingTween = dialogueText.DOText(text, text.Length * 0.05f)
                .SetEase(Ease.Linear)
                .OnComplete(() =>
                {
                    if (continueIcon != null) continueIcon.SetActive(true);
                    onComplete?.Invoke();
                });
        }
    }

    public void SkipTyping(string fullText)
    {
        if (typingTween != null) typingTween.Kill();
        if (dialogueText != null) dialogueText.text = fullText;
        if (continueIcon != null) continueIcon.SetActive(true);
    }

    public void ShowChoices(List<Ink.Runtime.Choice> choices, Action<int> onChoiceSelected)
    {
        ClearChoices();
        if (continueIcon != null) continueIcon.SetActive(false);
        if (choiceContainer == null || choiceButtonPrefab == null) return;

        onChoiceSelectedCallback = onChoiceSelected;
        currentChoiceIndex = 0;

        foreach (var choice in choices)
        {
            GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer);
            activeChoiceButtons.Add(btnObj);

            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = choice.text;

            Button btn = btnObj.GetComponent<Button>();
            if (btn != null)
            {
                int index = choice.index;
                btn.onClick.AddListener(() =>
                {
                    ClearChoices();
                    onChoiceSelectedCallback?.Invoke(index);
                });
            }
        }

        HighlightChoice(currentChoiceIndex);
    }

    public void ChangeChoiceSelection(int direction)
    {
        if (activeChoiceButtons.Count == 0) return;

        currentChoiceIndex += direction;

        if (currentChoiceIndex < 0) currentChoiceIndex = activeChoiceButtons.Count - 1;
        else if (currentChoiceIndex >= activeChoiceButtons.Count) currentChoiceIndex = 0;

        HighlightChoice(currentChoiceIndex);
    }

    private void HighlightChoice(int index)
    {
        for (int i = 0; i < activeChoiceButtons.Count; i++)
        {
            TextMeshProUGUI btnText = activeChoiceButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                if (i == index)
                {
                    btnText.color = selectedChoiceColor;
                    activeChoiceButtons[i].transform.DOScale(1.05f, 0.1f);
                }
                else
                {
                    btnText.color = normalChoiceColor;
                    activeChoiceButtons[i].transform.DOScale(1.0f, 0.1f);
                }
            }
        }
    }

    public void ConfirmChoice()
    {
        if (activeChoiceButtons.Count > 0)
        {
            Button selectedBtn = activeChoiceButtons[currentChoiceIndex].GetComponent<Button>();
            selectedBtn?.onClick.Invoke();
        }
    }

    public void ClearChoices()
    {
        foreach (var btn in activeChoiceButtons)
        {
            if (btn != null) Destroy(btn);
        }
        activeChoiceButtons.Clear();
    }

    public void HideUI(Action onComplete = null)
    {
        ClearChoices();
        if (continueIcon != null) continueIcon.SetActive(false);

        Sequence seq = DOTween.Sequence();

        if (textBoxGroup != null) seq.Append(textBoxGroup.DOFade(0f, 0.25f));
        if (affectionGroup != null && affectionGroup.gameObject.activeSelf) seq.Join(affectionGroup.DOFade(0f, 0.25f));

        seq.OnComplete(() =>
        {
            if (textBoxGroup != null) textBoxGroup.gameObject.SetActive(false);
            if (affectionGroup != null) affectionGroup.gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }
}
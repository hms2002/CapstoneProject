using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitleProfileSlotCardUI : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private Button selectButton;
    [SerializeField] private TMP_Text slotLabelText;
    [SerializeField] private TMP_Text stateLabelText;
    [SerializeField] private TMP_Text runLabelText;
    [SerializeField] private TMP_Text metaProgressLabelText;
    [SerializeField] private TMP_Text lastPlayedLabelText;
    [SerializeField] private TMP_Text actionLabelText;
    [SerializeField] private CanvasGroup canvasGroup;

    private Action<int> onSelected;
    private int slotIndex = -1;

    private void Awake()
    {
        ResolveReferences();
        BindListeners();
    }

    public void Bind(TitleProfileSlotSummary summary, TitleProfileSlotPanelMode mode, Action<int> onSelected)
    {
        ResolveReferences();

        this.onSelected = onSelected;
        slotIndex = summary.SlotIndex;

        bool canSelect = CanSelect(summary, mode);

        if (slotLabelText != null)
            slotLabelText.text = summary.SlotLabel;

        if (stateLabelText != null)
            stateLabelText.text = ResolveStateLabel(summary);

        if (runLabelText != null)
            runLabelText.text = summary.RunLabel;

        if (metaProgressLabelText != null)
            metaProgressLabelText.text = summary.MetaProgressLabel;

        if (lastPlayedLabelText != null)
            lastPlayedLabelText.text = summary.LastPlayedLabel;

        if (actionLabelText != null)
            actionLabelText.text = ResolveActionLabel(summary, mode, canSelect);

        if (selectButton != null)
            selectButton.interactable = canSelect;

        if (canvasGroup != null)
            canvasGroup.alpha = canSelect ? 1f : 0.55f;
    }

    private void ResolveReferences()
    {
        if (selectButton == null)
            selectButton = GetComponent<Button>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void BindListeners()
    {
        if (selectButton == null)
            return;

        selectButton.onClick.RemoveListener(HandleClicked);
        selectButton.onClick.AddListener(HandleClicked);
    }

    private void HandleClicked()
    {
        if (slotIndex < 0)
            return;

        onSelected?.Invoke(slotIndex);
    }

    private static bool CanSelect(TitleProfileSlotSummary summary, TitleProfileSlotPanelMode mode)
    {
        return mode switch
        {
            TitleProfileSlotPanelMode.NewGame => true,
            TitleProfileSlotPanelMode.Continue => summary.HasActiveRun,
            _ => false
        };
    }

    private static string ResolveStateLabel(TitleProfileSlotSummary summary)
    {
        if (!summary.HasProfile)
            return "빈 슬롯";

        return summary.HasActiveRun ? "진행 중" : "대기 중";
    }

    private static string ResolveActionLabel(
        TitleProfileSlotSummary summary,
        TitleProfileSlotPanelMode mode,
        bool canSelect)
    {
        if (!canSelect)
            return mode == TitleProfileSlotPanelMode.Continue ? "이어할 런 없음" : "선택 불가";

        if (mode == TitleProfileSlotPanelMode.Continue)
            return "이어하기";

        if (!summary.HasProfile)
            return "새 프로필 생성";

        return summary.HasActiveRun ? "새 런 시작" : "새 런 시작";
    }
}

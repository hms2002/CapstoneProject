using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TitleProfileSlotCardUI : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private UIChainDropPresentation[] closePresentations;
    [SerializeField] private Button selectButton;
    [SerializeField] private TMP_Text selectButtonLabelText;
    [SerializeField] private Button deleteButton;
    [SerializeField] private GameObject deleteButtonChainRoot;
    [SerializeField] private TMP_Text slotLabelText;
    [SerializeField] private TMP_Text stateLabelText;
    [SerializeField] private GameObject playTimeGroup;
    [SerializeField] private TMP_Text playTimeTitleText;
    [SerializeField] private TMP_Text playTimeValueText;
    [SerializeField] private GameObject upgradeProgressGroup;
    [SerializeField] private TMP_Text upgradeProgressTitleText;
    [SerializeField] private TMP_Text upgradeProgressValueText;
    [SerializeField] private GameObject magicStoneGroup;
    [SerializeField] private TMP_Text magicStoneTitleText;
    [SerializeField] private TMP_Text magicStoneValueText;
    [SerializeField] private GameObject clearCountGroup;
    [SerializeField] private TMP_Text clearCountTitleText;
    [SerializeField] private TMP_Text clearCountValueText;
    [SerializeField] private TMP_Text actionLabelText;
    [SerializeField] private CanvasGroup canvasGroup;

    private Action<int> onSelected;
    private Action<int> onDeleteRequested;
    private int slotIndex = -1;

    private void Awake()
    {
        ResolveReferences();
        BindListeners();
    }

    public void Bind(
        TitleProfileSlotSummary summary,
        Action<int> onSelected,
        Action<int> onDeleteRequested)
    {
        ResolveReferences();
        BindListeners();

        this.onSelected = onSelected;
        this.onDeleteRequested = onDeleteRequested;
        slotIndex = summary.SlotIndex;
        bool hasProfile = summary.HasProfile;

        if (slotLabelText != null)
        {
            slotLabelText.gameObject.SetActive(hasProfile);
            slotLabelText.text = summary.SlotLabel;
        }

        if (stateLabelText != null)
        {
            bool showStateLabel = !hasProfile;
            stateLabelText.gameObject.SetActive(showStateLabel);
            if (showStateLabel)
                stateLabelText.text = ResolveStateLabel(summary);
        }

        BindGroupedText(playTimeGroup, playTimeTitleText, playTimeValueText, hasProfile, PlayTimeTitle, summary.PlayTimeLabel);
        BindGroupedText(upgradeProgressGroup, upgradeProgressTitleText, upgradeProgressValueText, hasProfile, UpgradeProgressTitle, summary.UpgradeProgressLabel);
        BindGroupedText(magicStoneGroup, magicStoneTitleText, magicStoneValueText, hasProfile, MagicStoneTitle, summary.MagicStoneLabel);
        BindGroupedText(clearCountGroup, clearCountTitleText, clearCountValueText, hasProfile, ClearCountTitle, summary.ClearCountLabel);

        if (actionLabelText != null)
            actionLabelText.gameObject.SetActive(false);

        if (selectButtonLabelText != null)
            selectButtonLabelText.text = hasProfile
                ? GameText.Get("code.titleprofileslotcardui.9cb367f710", "계속하기")
                : GameText.Get("code.titleprofileslotcardui.389b82de7b", "시작하기");

        if (selectButton != null)
            selectButton.interactable = true;

        if (deleteButton != null)
        {
            bool canDelete = hasProfile;
            deleteButton.gameObject.SetActive(canDelete);
            deleteButton.interactable = canDelete;
        }

        if (deleteButtonChainRoot != null)
            deleteButtonChainRoot.SetActive(hasProfile);

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;
    }

    private void ResolveReferences()
    {
        if (closePresentations == null || closePresentations.Length == 0)
            closePresentations = GetComponentsInChildren<UIChainDropPresentation>(true);

        if (selectButton == null)
            selectButton = GetComponent<Button>();

        if (selectButtonLabelText == null && selectButton != null)
            selectButtonLabelText = selectButton.GetComponentInChildren<TMP_Text>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    private void BindListeners()
    {
        if (selectButton == null)
            return;

        selectButton.onClick.RemoveListener(HandleClicked);
        selectButton.onClick.AddListener(HandleClicked);

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(HandleDeleteClicked);
            deleteButton.onClick.AddListener(HandleDeleteClicked);
        }
    }

    private void HandleClicked()
    {
        if (slotIndex < 0)
            return;

        onSelected?.Invoke(slotIndex);
    }

    private void HandleDeleteClicked()
    {
        if (slotIndex < 0)
            return;

        onDeleteRequested?.Invoke(slotIndex);
    }

    public void SetInteractable(bool enabled)
    {
        if (selectButton != null)
            selectButton.interactable = enabled;

        if (deleteButton != null && deleteButton.gameObject.activeSelf)
            deleteButton.interactable = enabled;
    }

    public void RefreshLocalizedText(TitleProfileSlotSummary summary)
    {
        if (slotLabelText != null) slotLabelText.text = summary.SlotLabel;
        if (stateLabelText != null) stateLabelText.text = ResolveStateLabel(summary);
        if (playTimeTitleText != null) playTimeTitleText.text = PlayTimeTitle;
        if (upgradeProgressTitleText != null) upgradeProgressTitleText.text = UpgradeProgressTitle;
        if (magicStoneTitleText != null) magicStoneTitleText.text = MagicStoneTitle;
        if (clearCountTitleText != null) clearCountTitleText.text = ClearCountTitle;
        if (playTimeValueText != null) playTimeValueText.text = summary.PlayTimeLabel;
        if (upgradeProgressValueText != null) upgradeProgressValueText.text = summary.UpgradeProgressLabel;
        if (magicStoneValueText != null) magicStoneValueText.text = summary.MagicStoneLabel;
        if (clearCountValueText != null) clearCountValueText.text = summary.ClearCountLabel;
        if (selectButtonLabelText != null)
            selectButtonLabelText.text = summary.HasProfile
                ? GameText.Get("code.titleprofileslotcardui.9cb367f710", "계속하기")
                : GameText.Get("code.titleprofileslotcardui.389b82de7b", "시작하기");
    }

    public void PlayClosePresentations(Action onCompleted = null)
    {
        ResolveReferences();

        if (closePresentations == null || closePresentations.Length == 0)
        {
            onCompleted?.Invoke();
            return;
        }

        int activePresentationCount = 0;
        for (int i = 0; i < closePresentations.Length; i++)
        {
            UIChainDropPresentation presentation = closePresentations[i];
            if (presentation == null || !presentation.gameObject.activeInHierarchy)
                continue;

            activePresentationCount++;
        }

        if (activePresentationCount == 0)
        {
            onCompleted?.Invoke();
            return;
        }

        int remainingCallbacks = activePresentationCount;
        for (int i = 0; i < closePresentations.Length; i++)
        {
            UIChainDropPresentation presentation = closePresentations[i];
            if (presentation == null || !presentation.gameObject.activeInHierarchy)
                continue;

            presentation.PlayClose(() =>
            {
                remainingCallbacks--;
                if (remainingCallbacks <= 0)
                    onCompleted?.Invoke();
            });
        }
    }

    private static string ResolveStateLabel(TitleProfileSlotSummary summary)
    {
        if (!summary.HasProfile)
            return GameText.Get("code.titleprofileslotcardui.b5ccf802d5", "빈 슬롯");

        return summary.HasActiveRun ? GameText.Get("code.titleprofileslotcardui.7890cafc8d", "진행 중") : GameText.Get("code.titleprofileslotcardui.ec425b26f2", "대기 중");
    }

    private static void BindGroupedText(
        GameObject groupRoot,
        TMP_Text titleText,
        TMP_Text valueText,
        bool visible,
        string title,
        string value)
    {
        if (groupRoot != null)
            groupRoot.SetActive(visible);

        if (!visible)
            return;

        if (titleText != null)
            titleText.text = title;

        if (valueText != null)
            valueText.text = value;
    }

    private static string PlayTimeTitle => GameText.Get("title.profile.play_time", "플레이 타임");
    private static string UpgradeProgressTitle => GameText.Get("title.profile.upgrade_progress", "업그레이드 진행도");
    private static string MagicStoneTitle => GameText.Get("title.profile.magic_stones", "보유 마정석");
    private static string ClearCountTitle => GameText.Get("title.profile.clear_count", "클리어 횟수");
}

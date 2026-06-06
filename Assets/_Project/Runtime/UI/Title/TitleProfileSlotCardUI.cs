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
                ? "\uACC4\uC18D\uD558\uAE30"
                : "\uC2DC\uC791\uD558\uAE30";

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
            return "\uBE48 \uC2AC\uB86F";

        return summary.HasActiveRun ? "\uC9C4\uD589 \uC911" : "\uB300\uAE30 \uC911";
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

    private const string PlayTimeTitle = "\uD50C\uB808\uC774 \uD0C0\uC784";
    private const string UpgradeProgressTitle = "\uC5C5\uADF8\uB808\uC774\uB4DC \uC9C4\uD589\uB3C4";
    private const string MagicStoneTitle = "\uBCF4\uC720 \uB9C8\uC815\uC11D";
    private const string ClearCountTitle = "\uD074\uB9AC\uC5B4 \uD69F\uC218";
}

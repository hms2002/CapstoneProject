using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임: 제작된 레벨 보상 카드 비주얼에 정의 데이터를 투영하고 선택 ID만 Presenter에 전달한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class LevelRewardCardView : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private Button selectButton;

    [Header("Display")]
    [SerializeField] private TMP_Text displayNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text shortcutText;

    private Action<string> selectionRequested;
    private string boundRewardId;
    private bool listenerRegistered;

    private void Awake()
    {
        RegisterButtonListener();
    }

    private void OnDestroy()
    {
        if (listenerRegistered && selectButton != null)
            selectButton.onClick.RemoveListener(HandleClicked);

        listenerRegistered = false;
    }

    public void Bind(
        LevelRewardDefinitionSO definition,
        int displayIndex,
        Sprite fallbackIcon,
        Action<string> onSelectionRequested)
    {
        RegisterButtonListener();
        selectionRequested = onSelectionRequested;
        boundRewardId = definition != null ? definition.RewardId : null;

        if (displayNameText != null)
            displayNameText.text = definition != null ? definition.DisplayName : string.Empty;

        if (descriptionText != null)
            descriptionText.text = definition != null ? definition.Description : string.Empty;

        if (shortcutText != null)
            shortcutText.text = displayIndex >= 0 ? (displayIndex + 1).ToString() : string.Empty;

        if (iconImage != null)
        {
            Sprite resolvedIcon = definition != null && definition.Icon != null
                ? definition.Icon
                : fallbackIcon;
            iconImage.sprite = resolvedIcon;
            iconImage.enabled = resolvedIcon != null;
        }

        SetInteractable(definition != null && !string.IsNullOrWhiteSpace(boundRewardId));
    }

    public void Clear()
    {
        selectionRequested = null;
        boundRewardId = null;

        if (displayNameText != null)
            displayNameText.text = string.Empty;
        if (descriptionText != null)
            descriptionText.text = string.Empty;
        if (shortcutText != null)
            shortcutText.text = string.Empty;
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        SetInteractable(false);
    }

    public void SetInteractable(bool interactable)
    {
        if (selectButton != null)
            selectButton.interactable = interactable;
    }

    private void RegisterButtonListener()
    {
        if (listenerRegistered || selectButton == null)
            return;

        selectButton.onClick.AddListener(HandleClicked);
        listenerRegistered = true;
    }

    private void HandleClicked()
    {
        if (string.IsNullOrWhiteSpace(boundRewardId))
            return;

        selectionRequested?.Invoke(boundRewardId);
    }
}

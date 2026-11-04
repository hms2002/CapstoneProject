using System;
using TMPro;
using UnityEngine;

/// <summary>Projects one authored creature card; the tab owns selection and progression data.</summary>
[DisallowMultipleComponent]
public sealed class EncyclopediaCharacterEntryView : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button button;
    [SerializeField] private UnityEngine.UI.Image portraitImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private GameObject selectedMarker;
    [SerializeField] private GameObject affectionRoot;
    [SerializeField] private UnityEngine.UI.Image[] affectionFillImages = Array.Empty<UnityEngine.UI.Image>();

    private int entryIndex = -1;
    private bool interactionEnabled = true;
    public event Action<int> Selected;

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(HandleClick);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(int index, string title, Sprite portrait, bool character, int affection, bool selected)
    {
        entryIndex = index;
        gameObject.SetActive(true);
        if (titleText != null)
            titleText.text = title ?? string.Empty;
        EncyclopediaPortraitUtility.SetSprite(portraitImage, portrait);
        SetSelected(selected);
        if (affectionRoot != null)
            affectionRoot.SetActive(character);
        for (int i = 0; i < affectionFillImages.Length; i++)
            if (affectionFillImages[i] != null)
                affectionFillImages[i].fillAmount = i < Mathf.Max(0, affection) ? 1f : 0f;
        SetInteractionEnabled(interactionEnabled);
    }

    public void SetPortrait(Sprite portrait) => EncyclopediaPortraitUtility.SetSprite(portraitImage, portrait);

    public void SetSelected(bool selected)
    {
        if (selectedMarker != null)
            selectedMarker.SetActive(selected);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        if (button != null)
            button.interactable = enabled && entryIndex >= 0;
    }

    public void Clear()
    {
        entryIndex = -1;
        EncyclopediaPortraitUtility.SetSprite(portraitImage, null);
        SetSelected(false);
        gameObject.SetActive(false);
    }

    private void HandleClick()
    {
        if (interactionEnabled && entryIndex >= 0)
            Selected?.Invoke(entryIndex);
    }
}

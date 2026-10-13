using System;
using TMPro;
using UnityEngine;

/// <summary>An authored catalog card; it projects selection without owning gameplay state.</summary>
[DisallowMultipleComponent]
public sealed class TestSceneCatalogCell : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button button;
    [SerializeField] private UnityEngine.UI.Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private UnityEngine.UI.Image selectionBorder;

    private Action<int, int> selected;
    private int category;
    private int index;

    public string DisplayName => label != null ? label.text : gameObject.name;
    public Sprite Icon => icon != null ? icon.sprite : null;

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(HandleClick);
    }

    public void Bind(int categoryIndex, int entryIndex, Action<int, int> onSelected)
    {
        category = categoryIndex;
        index = entryIndex;
        selected = onSelected;
    }

    public void Unbind() => selected = null;

    public void SetSelected(bool value)
    {
        if (selectionBorder != null) selectionBorder.enabled = value;
    }

    private void HandleClick() => selected?.Invoke(category, index);
}

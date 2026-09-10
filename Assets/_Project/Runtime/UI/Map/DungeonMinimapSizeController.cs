using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Responsibility: own only the minimap's local display size and authored button bindings,
/// independently of dungeon discovery and the presenter's scene visibility.
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonMinimapSizeController : MonoBehaviour
{
    [SerializeField] private RectTransform mapBody;
    [SerializeField] private Button expandButton;
    [SerializeField] private Button shrinkButton;
    [SerializeField, Min(1f)] private float expandedAreaMultiplier = 6f;

    private const int Collapsed = 0;
    private const int Normal = 1;
    private const int Expanded = 2;
    private int sizeLevel = Normal;
    private Vector3 normalScale;

    private void Awake()
    {
        normalScale = mapBody != null ? mapBody.localScale : Vector3.one;
    }

    private void OnEnable()
    {
        if (expandButton != null)
            expandButton.onClick.AddListener(Expand);
        if (shrinkButton != null)
            shrinkButton.onClick.AddListener(Shrink);
        ApplySize();
    }

    private void OnDisable()
    {
        if (expandButton != null)
            expandButton.onClick.RemoveListener(Expand);
        if (shrinkButton != null)
            shrinkButton.onClick.RemoveListener(Shrink);
    }

    public void Expand()
    {
        sizeLevel = Mathf.Min(Expanded, sizeLevel + 1);
        ApplySize();
    }

    public void Shrink()
    {
        sizeLevel = Mathf.Max(Collapsed, sizeLevel - 1);
        ApplySize();
    }

    private void ApplySize()
    {
        if (mapBody != null)
        {
            float scale = sizeLevel == Expanded
                ? Mathf.Sqrt(Mathf.Max(1f, expandedAreaMultiplier))
                : 1f;
            mapBody.localScale = new Vector3(normalScale.x * scale, normalScale.y * scale, normalScale.z);
            mapBody.gameObject.SetActive(sizeLevel != Collapsed);
        }

        if (expandButton != null)
            expandButton.interactable = sizeLevel < Expanded;
        if (shrinkButton != null)
            shrinkButton.interactable = sizeLevel > Collapsed;
    }
}

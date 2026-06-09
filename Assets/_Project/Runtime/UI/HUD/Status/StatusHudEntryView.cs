using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 책임 :
/// - 상태 HUD 엔트리 하나의 아이콘, 스택, 지속시간, 강조 표시를 실제 UI로 그린다.
/// - 마우스 hover 시 공통 hover 시스템을 통해 상태 툴팁을 띄워 상태 UI와 툴팁 계층을 분리한다.
/// </summary>
public sealed class StatusHudEntryView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Refs")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image durationFillImage;
    [SerializeField] private TMP_Text stackText;
    [SerializeField] private TMP_Text durationText;

    [Header("Style")]
    [SerializeField] private Vector2 entrySize = new(48f, 48f);
    [SerializeField] private Color backgroundColor = new(0.1f, 0.12f, 0.18f, 0.9f);
    [SerializeField] private Color highlightedBackgroundColor = new(0.33f, 0.21f, 0.08f, 0.92f);
    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] private Color highlightedIconColor = new(1f, 0.95f, 0.72f, 1f);
    [SerializeField] private Color durationFillColor = new(0.95f, 0.77f, 0.26f, 0.95f);
    [SerializeField] private Color outlineColor = new(0.02f, 0.04f, 0.08f, 0.6f);
    [SerializeField] private Vector2 outlineDistance = new(1f, -1f);

    [Header("Fallback Layout")]
    [SerializeField] private Vector2 iconOffsetMin = new(8f, 8f);
    [SerializeField] private Vector2 iconOffsetMax = new(-8f, -8f);
    [SerializeField] private Vector2 durationFillOffsetMin = new(4f, 40f);
    [SerializeField] private Vector2 durationFillOffsetMax = new(-4f, -4f);
    [SerializeField] private int stackFontSize = 13;
    [SerializeField] private int durationFontSize = 11;
    [SerializeField] private Vector2 stackOffsetMin = new(-4f, -3f);
    [SerializeField] private Vector2 stackOffsetMax = new(-4f, -3f);
    [SerializeField] private Vector2 durationOffsetMin = new(4f, 2f);
    [SerializeField] private Vector2 durationOffsetMax = new(-4f, 14f);

    private RectTransform rectTransform;
    private StatusHudEntry currentEntry;
    private bool isPointerHovering;

    private void Awake()
    {
        EnsureVisualTree();
    }

    private void OnDisable()
    {
        HideHoverIfNeeded();
    }

    public void Bind(in StatusHudEntry entry)
    {
        EnsureVisualTree();
        currentEntry = entry;

        gameObject.SetActive(entry.IsVisible);
        if (!entry.IsVisible)
        {
            HideHoverIfNeeded();
            return;
        }

        backgroundImage.color = entry.IsHighlighted
            ? highlightedBackgroundColor
            : backgroundColor;

        iconImage.sprite = entry.Icon;
        iconImage.enabled = entry.Icon != null;
        iconImage.color = entry.IsHighlighted
            ? highlightedIconColor
            : iconColor;

        stackText.text = entry.ShowStacks && entry.StackCount > 0 ? entry.StackCount.ToString() : string.Empty;
        durationText.text = entry.ShowDuration && entry.RemainingTime > 0f
            ? entry.RemainingTime.ToString("0.0")
            : string.Empty;

        if (entry.ShowDuration && entry.MaxTime > 0f && entry.RemainingTime > 0f)
        {
            durationFillImage.enabled = true;
            durationFillImage.fillAmount = Mathf.Clamp01(entry.RemainingTime / entry.MaxTime);
        }
        else
        {
            durationFillImage.enabled = false;
            durationFillImage.fillAmount = 0f;
        }

        if (isPointerHovering && UIManager.Instance != null)
            UIManager.Instance.ShowHover(StatusHudTooltipView.Instance, rectTransform, currentEntry);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!currentEntry.IsVisible || UIManager.Instance == null)
            return;

        isPointerHovering = true;
        UIManager.Instance.ShowHover(StatusHudTooltipView.Instance, rectTransform, currentEntry);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideHoverIfNeeded();
    }

    private void HideHoverIfNeeded()
    {
        if (!isPointerHovering)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.HideHover(StatusHudTooltipView.Instance, rectTransform);

        isPointerHovering = false;
    }

    /// <summary>
    /// 책임 :
    /// - 프리팹 없이도 상태 HUD 엔트리가 동작하도록 배경, 아이콘, 스택, 지속시간 UI를 코드로 생성한다.
    /// - presenter가 런타임 풀링만으로 엔트리 뷰를 만들고 재사용할 수 있게 최소 시각 구조를 보장한다.
    /// </summary>
    private void EnsureVisualTree()
    {
        if (rectTransform == null ||
            backgroundImage == null ||
            transform.Find("Icon") == null ||
            transform.Find("DurationFill") == null ||
            transform.Find("StackText") == null ||
            transform.Find("DurationText") == null)
        {
            RuntimePresentationFallbackAudit.Record(
                this,
                "Status HUD entry visual fallback",
                "an authored StatusHudEntryView prefab with icon, duration, stack, and text references");
        }

        rectTransform ??= gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        if (rectTransform.sizeDelta == Vector2.zero)
            rectTransform.sizeDelta = entrySize;

        backgroundImage ??= gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        backgroundImage.color = backgroundColor;
        Outline outline = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = outlineDistance;

        iconImage ??= CreateImage("Icon", iconOffsetMin, iconOffsetMax, 0);
        iconImage.preserveAspect = true;

        durationFillImage ??= CreateImage("DurationFill", durationFillOffsetMin, durationFillOffsetMax, 1);
        durationFillImage.type = Image.Type.Filled;
        durationFillImage.fillMethod = Image.FillMethod.Horizontal;
        durationFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        durationFillImage.color = durationFillColor;

        stackText ??= CreateText("StackText", stackFontSize, TextAlignmentOptions.TopRight, new Vector2(0f, 0f), new Vector2(0f, 0f), stackOffsetMin, stackOffsetMax);
        durationText ??= CreateText("DurationText", durationFontSize, TextAlignmentOptions.Bottom, new Vector2(0f, 0f), new Vector2(1f, 0f), durationOffsetMin, durationOffsetMax);
    }

    private Image CreateImage(string name, Vector2 offsetMin, Vector2 offsetMax, int siblingIndex)
    {
        Transform existing = transform.Find(name);
        GameObject child = existing != null ? existing.gameObject : new GameObject(name);
        child.transform.SetParent(transform, false);
        child.transform.SetSiblingIndex(siblingIndex);

        RectTransform childRect = child.GetComponent<RectTransform>() ?? child.AddComponent<RectTransform>();
        childRect.anchorMin = Vector2.zero;
        childRect.anchorMax = Vector2.one;
        childRect.offsetMin = offsetMin;
        childRect.offsetMax = offsetMax;

        Image image = child.GetComponent<Image>() ?? child.AddComponent<Image>();
        return image;
    }

    private TMP_Text CreateText(
        string name,
        int fontSize,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        Transform existing = transform.Find(name);
        GameObject child = existing != null ? existing.gameObject : new GameObject(name);
        child.transform.SetParent(transform, false);

        RectTransform childRect = child.GetComponent<RectTransform>() ?? child.AddComponent<RectTransform>();
        childRect.anchorMin = anchorMin;
        childRect.anchorMax = anchorMax;
        childRect.offsetMin = offsetMin;
        childRect.offsetMax = offsetMax;

        TMP_Text text = child.GetComponent<TMP_Text>() ?? child.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.richText = false;
        text.enableWordWrapping = false;
        return text;
    }
}

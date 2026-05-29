using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 책임 :
/// - 상태 HUD 엔트리에 마우스를 올렸을 때 아이콘, 이름, 서사 설명, 효과 설명을 보여주는 호버 툴팁 뷰를 제공한다.
/// - 상태 소유 계층이 직접 UI를 그리지 않고, HoverUIController를 통해 공통 hover 흐름을 재사용하게 만든다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class StatusHudTooltipView : MonoBehaviour, IHoverView
{
    private static StatusHudTooltipView instance;

    [Header("Refs")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text storyText;
    [SerializeField] private TMP_Text effectText;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    public static StatusHudTooltipView Instance => EnsureInstance();
    public RectTransform Rect => rectTransform;
    public bool IsActive => gameObject.activeSelf && canvasGroup != null && canvasGroup.alpha > 0.01f;

    public static StatusHudTooltipView EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<StatusHudTooltipView>(FindObjectsInactive.Include);
        if (instance != null)
            return instance;

        StatusHudTooltipView prefab = GlobalUIRoot.GetStatusTooltipPrefab();
        if (prefab != null)
        {
            instance = Instantiate(prefab);
            return instance;
        }

        GameObject root = new("StatusHudTooltipView");
        instance = root.AddComponent<StatusHudTooltipView>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureVisualTree();
        GlobalUIRoot.AdoptToCanvas(GlobalCanvasLayer.Hover, transform, false);
        OpenUI();
        HideHover();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void OpenUI()
    {
        gameObject.SetActive(true);
    }

    public void CloseUI()
    {
        HideHover();
    }

    public void ShowHover(object data, object context = null)
    {
        if (data is not StatusHudEntry entry)
        {
            HideHover();
            return;
        }

        EnsureVisualTree();

        if (iconImage != null)
        {
            iconImage.sprite = entry.Icon;
            iconImage.enabled = entry.Icon != null;
        }

        nameText.text = string.IsNullOrWhiteSpace(entry.NameText) ? entry.StatusId : entry.NameText;
        storyText.text = entry.StoryText ?? string.Empty;

        string stackLine = entry.ShowStacks ? $"스택: {entry.StackCount}" : string.Empty;
        string durationLine = entry.ShowDuration
            ? $"지속시간: {Mathf.Max(0f, entry.RemainingTime):0.0}s"
            : string.Empty;
        string runtimeStats = string.IsNullOrEmpty(stackLine)
            ? durationLine
            : string.IsNullOrEmpty(durationLine)
                ? stackLine
                : $"{stackLine}\n{durationLine}";

        effectText.text = string.IsNullOrWhiteSpace(entry.EffectText)
            ? runtimeStats
            : string.IsNullOrWhiteSpace(runtimeStats)
                ? entry.EffectText
                : $"{entry.EffectText}\n\n{runtimeStats}";

        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    public void HideHover()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// 책임 :
    /// - 프리팹 없이도 상태 HUD 툴팁이 동작하도록 최소 UI 트리를 코드로 생성한다.
    /// - 배경, 아이콘, 이름, 서사 설명, 효과 설명 영역을 한 곳에서 보장해 hover 시스템과의 연결만으로 툴팁이 보이게 만든다.
    /// </summary>
    private void EnsureVisualTree()
    {
        if (gameObject.GetComponent<RectTransform>() == null ||
            backgroundImage == null ||
            transform.Find("Icon") == null ||
            transform.Find("Name") == null ||
            transform.Find("Story") == null ||
            transform.Find("Effect") == null)
        {
            RuntimePresentationFallbackAudit.Record(
                this,
                "Status HUD tooltip visual fallback",
                "an authored StatusHudTooltipView prefab with background, icon, name, story, and effect references");
        }

        bool createdRectTransform = false;
        rectTransform = gameObject.GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = gameObject.AddComponent<RectTransform>();
            createdRectTransform = true;
        }

        if (createdRectTransform)
        {
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(240f, 120f);
        }

        canvasGroup ??= GetComponent<CanvasGroup>();

        backgroundImage ??= gameObject.GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.08f, 0.1f, 0.16f, 0.94f);
        }

        Outline outline = gameObject.GetComponent<Outline>();
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.78f, 0.84f, 0.97f, 0.4f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        VerticalLayoutGroup layout = gameObject.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
        }

        ContentSizeFitter fitter = gameObject.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        iconImage ??= CreateIcon("Icon");
        nameText ??= CreateText("Name", 18, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Color(0.98f, 0.95f, 0.82f, 1f));
        storyText ??= CreateText("Story", 14, FontStyles.Normal, TextAlignmentOptions.TopLeft, Color.white);
        effectText ??= CreateText("Effect", 13, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Color(0.79f, 0.87f, 0.98f, 1f));
    }

    /// <summary>
    /// 책임 :
    /// - 상태 툴팁이 사용하는 아이콘 이미지를 코드 fallback으로 생성한다.
    /// - 수동 authoring 프리팹이 없더라도 아이콘/이름/설명 레이아웃이 유지되게 만든다.
    /// </summary>
    private Image CreateIcon(string name)
    {
        RectTransform childRect = GetOrCreateUiChild(name);
        GameObject child = childRect.gameObject;
        childRect.anchorMin = new Vector2(0f, 1f);
        childRect.anchorMax = new Vector2(0f, 1f);
        childRect.sizeDelta = new Vector2(32f, 32f);

        LayoutElement layoutElement = child.GetComponent<LayoutElement>() ?? child.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 32f;
        layoutElement.preferredHeight = 32f;

        Image image = child.GetComponent<Image>() ?? child.AddComponent<Image>();
        image.preserveAspect = true;
        return image;
    }

    private TMP_Text CreateText(string name, int fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
    {
        RectTransform childRect = GetOrCreateUiChild(name);
        GameObject child = childRect.gameObject;
        childRect.anchorMin = new Vector2(0f, 1f);
        childRect.anchorMax = new Vector2(1f, 1f);

        TMP_Text text = child.GetComponent<TMP_Text>() ?? child.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.richText = true;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    /// <summary>
    /// 책임 :
    /// - fallback UI 생성 시 같은 이름의 일반 Transform 자식이 있어도 RectTransform 기반 UI 자식을 안전하게 보장한다.
    /// - 잘못 authoring된 레거시 자식은 비활성화하고 새 UI 자식을 만들어 MissingComponentException을 막는다.
    /// </summary>
    private RectTransform GetOrCreateUiChild(string name)
    {
        Transform existing = transform.Find(name);
        if (existing != null && existing.TryGetComponent(out RectTransform existingRect))
            return existingRect;

        if (existing != null)
        {
            existing.gameObject.SetActive(false);
            existing.name = $"{name}_NonUiLegacy";
        }

        GameObject child = new(name, typeof(RectTransform));
        child.transform.SetParent(transform, false);
        return child.GetComponent<RectTransform>();
    }
}

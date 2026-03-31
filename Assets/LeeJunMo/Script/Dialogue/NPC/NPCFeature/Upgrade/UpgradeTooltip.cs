using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class UpgradeTooltip : MonoBehaviour, IHoverView
{
    public static UpgradeTooltip Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;
    [SerializeField] private RectTransform backgroundRect;

    [Header("Layout")]
    [SerializeField] private float minTooltipWidth = 180f;
    [SerializeField] private float maxTooltipWidth = 360f;
    [SerializeField] private float horizontalPadding = 24f;

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    public RectTransform Rect => rectTransform;
    public bool IsActive => gameObject.activeSelf && canvasGroup != null && canvasGroup.alpha > 0.01f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
        OpenUI();
        HideHover();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
        if (!(data is UpgradeNodeSO node))
        {
            HideHover();
            return;
        }

        if (titleText != null)
            titleText.text = node.upgradeName;

        if (contentText != null)
        {
            contentText.text = node.description;
            contentText.enableWordWrapping = true;
        }

        ApplyTooltipLayout();

        gameObject.SetActive(true);
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

    private void ApplyTooltipLayout()
    {
        if (backgroundRect == null)
            return;

        Canvas.ForceUpdateCanvases();

        float titleWidth = titleText != null ? titleText.preferredWidth + horizontalPadding : minTooltipWidth;
        float tooltipWidth = Mathf.Clamp(titleWidth, minTooltipWidth, maxTooltipWidth);

        backgroundRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, tooltipWidth);

        if (titleText != null)
        {
            RectTransform titleRect = titleText.rectTransform;
            titleRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, tooltipWidth - horizontalPadding);
        }

        if (contentText != null)
        {
            RectTransform contentRect = contentText.rectTransform;
            contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, tooltipWidth - horizontalPadding);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRect);
    }
}

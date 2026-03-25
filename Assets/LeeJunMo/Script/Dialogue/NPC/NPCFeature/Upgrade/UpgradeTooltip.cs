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
            contentText.text = node.description;

        if (backgroundRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRect);

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
}

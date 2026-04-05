using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ItemDetailPanel : MonoBehaviour, IHoverView
{
    public static ItemDetailPanel Instance { get; private set; }

    [Header("Header")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;

    [Header("Views")]
    [SerializeField] private WeaponDetailView weaponView;
    [SerializeField] private WeaponDetailViewV2 weaponViewV2;
    [SerializeField] private RelicDetailView relicView;
    [SerializeField] private ConsumableDetailView consumableView;

    [Header("Glossary (optional)")]
    [SerializeField] private GlossaryDatabase glossary;
    [SerializeField] private GlossaryPopup glossaryPopup;

    [Header("Services")]
    [SerializeField] private TooltipColorPalette tooltipColorPalette;
    [SerializeField] private string glossaryLinkColorHex = "5EC8FF";

    private ItemDetailPanelServices _services;

    public RectTransform Rect => transform as RectTransform;
    public bool IsActive => gameObject.activeSelf;

    public void OpenUI()
    {
        gameObject.SetActive(true);
    }

    public void CloseUI()
    {
        HideHover();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        GlobalUIRoot.AdoptToCanvas(GlobalCanvasLayer.Hover, transform);
        _services = new ItemDetailPanelServices
        {
            formatText = raw => DetailTextFormatter.Format(raw, tooltipColorPalette, glossaryLinkColorHex),
            showGlossary = ShowGlossaryPopup
        };

        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowHover(object definition, object context = null)
    {
        if (definition == null)
        {
            HideHover();
            return;
        }

        var ctx = context as ItemDetailContext;

        if (glossaryPopup != null)
            glossaryPopup.Hide();

        gameObject.SetActive(true);

        if (definition is IInventoryItemDefinition common)
        {
            if (iconImage != null)
            {
                iconImage.sprite = common.Icon;
                iconImage.enabled = common.Icon != null;
            }

            if (titleText != null)
                titleText.text = common.DisplayName;

            if (subtitleText != null)
                subtitleText.text = common.Kind.ToString();
        }
        else
        {
            if (titleText != null)
                titleText.text = definition.ToString();

            if (subtitleText != null)
                subtitleText.text = string.Empty;

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        if (weaponViewV2 != null && weaponViewV2.CanShow(definition))
        {
            weaponViewV2.Show(definition, ctx, _services);
            if (weaponView != null)
                weaponView.Hide();
            if (relicView != null)
                relicView.Hide();
            if (consumableView != null)
                consumableView.Hide();
        }
        else if (weaponView != null && weaponView.CanShow(definition))
        {
            weaponView.Show(definition, ctx, _services);
            if (weaponViewV2 != null)
                weaponViewV2.Hide();
            if (relicView != null)
                relicView.Hide();
            if (consumableView != null)
                consumableView.Hide();
        }
        else if (relicView != null && relicView.CanShow(definition))
        {
            relicView.Show(definition, ctx, _services);
            if (weaponView != null)
                weaponView.Hide();
            if (weaponViewV2 != null)
                weaponViewV2.Hide();
            if (consumableView != null)
                consumableView.Hide();
        }
        else if (consumableView != null && consumableView.CanShow(definition))
        {
            consumableView.Show(definition, ctx, _services);
            if (weaponView != null)
                weaponView.Hide();
            if (weaponViewV2 != null)
                weaponViewV2.Hide();
            if (relicView != null)
                relicView.Hide();
        }
        else
        {
            if (weaponView != null)
                weaponView.Hide();
            if (weaponViewV2 != null)
                weaponViewV2.Hide();

            if (relicView != null)
                relicView.Hide();

            if (consumableView != null)
                consumableView.Hide();
        }

        Canvas.ForceUpdateCanvases();
    }

    public void HideHover()
    {
        if (weaponView != null)
            weaponView.Hide();

        if (weaponViewV2 != null)
            weaponViewV2.Hide();

        if (relicView != null)
            relicView.Hide();

        if (consumableView != null)
            consumableView.Hide();

        if (glossaryPopup != null)
            glossaryPopup.Hide();

        gameObject.SetActive(false);
    }

    private void ShowGlossaryPopup(string key)
    {
        if (glossaryPopup == null)
            return;

        if (glossary != null && glossary.TryGet(key, out var desc))
        {
            glossaryPopup.Show(key, desc);
        }
        else
        {
            glossaryPopup.Show(key, "설명이 등록되지 않았습니다.");
        }
    }
}

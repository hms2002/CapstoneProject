using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailPanel : MonoBehaviour, IHoverView
{
    public static ItemDetailPanel Instance { get; private set; }

    [Header("Header")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;

    [Header("Views")]
    [SerializeField] private WeaponDetailView weaponView;
    [SerializeField] private RelicDetailView relicView;

    [Header("Glossary (optional)")]
    [SerializeField] private GlossaryDatabase glossary;
    [SerializeField] private GlossaryPopup glossaryPopup;

    [Header("Services")]
    [SerializeField] private string glossaryLinkColorHex = "5EC8FF";

    private ItemDetailPanelServices _services;

    // =========================================================
    // IHoverView 규약 (위치 계산을 위해 자기 자신의 Rect 제공)
    // =========================================================
    public RectTransform Rect => transform as RectTransform;
    public bool IsActive => gameObject.activeSelf;

    public void OpenUI() { gameObject.SetActive(true); }
    public void CloseUI() { HideHover(); }

    private void Awake()
    {
        Instance = this;
        _services = new ItemDetailPanelServices
        {
            formatText = (raw) => DetailTextFormatter.ApplyGlossaryLinks(raw, glossaryLinkColorHex),
            showGlossary = ShowGlossaryPopup
        };

        gameObject.SetActive(false);
    }

    public void ShowHover(object definition, object context = null)
    {
        if (definition == null)
        {
            HideHover();
            return;
        }

        ItemDetailContext ctx = context as ItemDetailContext;

        if (glossaryPopup != null) glossaryPopup.Hide();

        if (definition is IInventoryItemDefinition common)
        {
            if (iconImage != null)
            {
                iconImage.sprite = common.Icon;
                iconImage.enabled = common.Icon != null;
            }
            if (titleText != null) titleText.text = common.DisplayName;
            if (subtitleText != null) subtitleText.text = common.Kind.ToString();
        }
        else
        {
            if (titleText != null) titleText.text = definition.ToString();
            if (subtitleText != null) subtitleText.text = "";
            if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
        }

        bool shown = false;
        if (weaponView != null && weaponView.CanShow(definition))
        {
            weaponView.Show(definition, ctx, _services);
            if (relicView != null) relicView.Hide();
            shown = true;
        }
        else if (relicView != null && relicView.CanShow(definition))
        {
            relicView.Show(definition, ctx, _services);
            if (weaponView != null) weaponView.Hide();
            shown = true;
        }
        else
        {
            if (weaponView != null) weaponView.Hide();
            if (relicView != null) relicView.Hide();
        }

        gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
    }

    public void HideHover()
    {
        if (weaponView != null) weaponView.Hide();
        if (relicView != null) relicView.Hide();
        if (glossaryPopup != null) glossaryPopup.Hide();

        gameObject.SetActive(false);
    }

    private void ShowGlossaryPopup(string key)
    {
        if (glossaryPopup == null) return;

        if (glossary != null && glossary.TryGet(key, out var desc))
            glossaryPopup.Show(key, desc);
        else
            glossaryPopup.Show(key, "설명이 등록되지 않았습니다.");
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemDetailPanel : MonoBehaviour
{
    public static ItemDetailPanel Instance { get; private set; }

    [Header("Header")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText; // 없으면 비워도 됨

    [Header("Views")]
    [SerializeField] private WeaponDetailView weaponView;
    [SerializeField] private RelicDetailView relicView;

    [Header("Glossary (optional)")]
    [SerializeField] private GlossaryDatabase glossary;   // 없으면 null 가능
    [SerializeField] private GlossaryPopup glossaryPopup; // 없으면 null 가능

    [Header("Services")]
    [SerializeField] private string glossaryLinkColorHex = "5EC8FF";

    private ItemDetailPanelServices _services;

    private void Awake()
    {
        Instance = this;

        // services 준비
        _services = new ItemDetailPanelServices
        {
            formatText = (raw) => DetailTextFormatter.ApplyGlossaryLinks(raw, glossaryLinkColorHex),
            showGlossary = ShowGlossaryPopup
        };

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 외부(UIHoverManager 등)에서 호출: definition이 WeaponDefinition/RelicDefinition 등일 수 있음
    /// </summary>
    public void Show(object definition, ItemDetailContext ctx)
    {
        if (definition == null)
        {
            Hide();
            return;
        }

        // 팝업 초기화
        if (glossaryPopup != null) glossaryPopup.Hide();

        // 공통 헤더 표시: IInventoryItemDefinition이면 아이콘/이름/종류를 자동 표시
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
            // fallback
            if (titleText != null) titleText.text = definition.ToString();
            if (subtitleText != null) subtitleText.text = "";
            if (iconImage != null) { iconImage.sprite = null; iconImage.enabled = false; }
        }

        // View 선택
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
            // 둘 다 아니면 아무것도 못 보여줌
            if (weaponView != null) weaponView.Hide();
            if (relicView != null) relicView.Hide();
        }

        gameObject.SetActive(true);

        // 혹시 레이아웃 갱신이 필요한 경우(섹션이 많을 때)
        Canvas.ForceUpdateCanvases();

        // shown이 false면 그냥 기본 헤더만 보이게 둘 수도 있고, Hide할 수도 있음.
        // 여기선 헤더만이라도 보이게 유지.
    }

    public void Hide()
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

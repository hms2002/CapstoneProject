using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[AddComponentMenu("")]
[DisallowMultipleComponent]
public sealed class EncyclopediaLeftPageView : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] private TMP_Text pageTitleText;
    [SerializeField] private Image pageTitleIcon;

    [Header("Item Sub Tabs")]
    [FormerlySerializedAs("weaponTabButton")]
    [SerializeField] private Button weaponSubTabButton;
    [FormerlySerializedAs("monsterTabButton")]
    [SerializeField] private Button relicSubTabButton;
    [FormerlySerializedAs("bossTabButton")]
    [SerializeField] private Button consumableSubTabButton;
    [FormerlySerializedAs("weaponSelectedMarker")]
    [SerializeField] private GameObject weaponSelectedMarker;
    [FormerlySerializedAs("monsterSelectedMarker")]
    [SerializeField] private GameObject relicSelectedMarker;
    [FormerlySerializedAs("bossSelectedMarker")]
    [SerializeField] private GameObject consumableSelectedMarker;

    [Header("Grid")]
    [SerializeField] private EncyclopediaEntryGridView entryGridView;

    [Header("Pagination")]
    [SerializeField] private Button previousStepButton;
    [SerializeField] private Button nextStepButton;
    [SerializeField] private TMP_Text pageText;
    [SerializeField] private TMP_Text entryCountText;
    [SerializeField] private TMP_Text listNoticeText;

    private bool listenersBound;

    public event Action<EncyclopediaItemSubTab> ItemSubTabRequested;
    public event Action PreviousPageRequested;
    public event Action NextPageRequested;

    public EncyclopediaEntryGridView EntryGridView
    {
        get
        {
            ResolveReferences();
            return entryGridView;
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveReferences();
    }

    [ContextMenu("Auto Wire References")]
    private void AutoWireReferences()
    {
        ResolveReferences();
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    private void OnDestroy()
    {
        UnbindListeners();
    }

    public void ResolveReferences()
    {
        if (pageTitleText == null)
            pageTitleText = EncyclopediaReferenceResolver.FindComponentUnderParent<TMP_Text>(transform, "TitleGroup", "TitleText", "Title", "Text", "Text (TMP)", "Text(TMP)");

        if (pageTitleIcon == null)
            pageTitleIcon = EncyclopediaReferenceResolver.FindComponentUnderParent<Image>(transform, "TitleGroup", "TitleIcon", "Icon", "Decoration");

        if (weaponSubTabButton == null)
            weaponSubTabButton = EncyclopediaReferenceResolver.FindComponent<Button>(transform, "WeaponTab", "WeaponTabButton", "Weapon", "WeaponButton");

        if (relicSubTabButton == null)
            relicSubTabButton = EncyclopediaReferenceResolver.FindComponent<Button>(transform, "RelicTab", "RelicTabButton", "Relic", "RelicButton", "MonsterTab", "MonsterTabButton");

        if (consumableSubTabButton == null)
            consumableSubTabButton = EncyclopediaReferenceResolver.FindComponent<Button>(transform, "ConsumableTab", "ConsumableTabButton", "Consumable", "ConsumableButton", "BossTab", "BossTabButton");

        if (weaponSelectedMarker == null)
            weaponSelectedMarker = EncyclopediaReferenceResolver.FindMarker(transform, "WeaponTab") ??
                EncyclopediaReferenceResolver.FindMarker(transform, "Weapon");

        if (relicSelectedMarker == null)
            relicSelectedMarker = EncyclopediaReferenceResolver.FindMarker(transform, "RelicTab") ??
                EncyclopediaReferenceResolver.FindMarker(transform, "Relic") ??
                EncyclopediaReferenceResolver.FindMarker(transform, "MonsterTab");

        if (consumableSelectedMarker == null)
            consumableSelectedMarker = EncyclopediaReferenceResolver.FindMarker(transform, "ConsumableTab") ??
                EncyclopediaReferenceResolver.FindMarker(transform, "Consumable") ??
                EncyclopediaReferenceResolver.FindMarker(transform, "BossTab");

        if (entryGridView == null)
            entryGridView = GetComponentInChildren<EncyclopediaEntryGridView>(true);

        if (previousStepButton == null)
            previousStepButton = EncyclopediaReferenceResolver.FindComponentUnderParent<Button>(transform, "PageButtonGroup", "PreviousStepButton", "PreviousButton", "PrevButton", "Previous", "Prev");

        if (nextStepButton == null)
            nextStepButton = EncyclopediaReferenceResolver.FindComponentUnderParent<Button>(transform, "PageButtonGroup", "NextStepButton", "NextButton", "Next");

        if (pageText == null)
            pageText = EncyclopediaReferenceResolver.FindComponentUnderParent<TMP_Text>(transform, "PageButtonGroup", "PageText", "Page", "Text", "Text (TMP)", "Text(TMP)");

        if (entryCountText == null)
            entryCountText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "EntryCountText", "CountText");

        if (listNoticeText == null)
            listNoticeText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "ListNoticeText", "NoticeText", "EmptyText");
    }

    public void BindListeners()
    {
        if (listenersBound)
            return;

        if (weaponSubTabButton != null)
            weaponSubTabButton.onClick.AddListener(RequestWeapons);
        if (relicSubTabButton != null)
            relicSubTabButton.onClick.AddListener(RequestRelics);
        if (consumableSubTabButton != null)
            consumableSubTabButton.onClick.AddListener(RequestConsumables);
        if (previousStepButton != null)
            previousStepButton.onClick.AddListener(RequestPreviousPage);
        if (nextStepButton != null)
            nextStepButton.onClick.AddListener(RequestNextPage);

        listenersBound = true;
    }

    public void UnbindListeners()
    {
        if (!listenersBound)
            return;

        if (weaponSubTabButton != null)
            weaponSubTabButton.onClick.RemoveListener(RequestWeapons);
        if (relicSubTabButton != null)
            relicSubTabButton.onClick.RemoveListener(RequestRelics);
        if (consumableSubTabButton != null)
            consumableSubTabButton.onClick.RemoveListener(RequestConsumables);
        if (previousStepButton != null)
            previousStepButton.onClick.RemoveListener(RequestPreviousPage);
        if (nextStepButton != null)
            nextStepButton.onClick.RemoveListener(RequestNextPage);

        listenersBound = false;
    }

    public void SetPageTitle(string title, Sprite icon)
    {
        SetText(pageTitleText, title);
        SetImage(pageTitleIcon, icon);

        if (pageTitleIcon != null)
            pageTitleIcon.gameObject.SetActive(icon != null);
    }

    public void SetItemSubTabState(EncyclopediaItemSubTab selectedSubTab)
    {
        SetSubTabState(weaponSubTabButton, weaponSelectedMarker, selectedSubTab == EncyclopediaItemSubTab.Weapon);
        SetSubTabState(relicSubTabButton, relicSelectedMarker, selectedSubTab == EncyclopediaItemSubTab.Relic);
        SetSubTabState(consumableSubTabButton, consumableSelectedMarker, selectedSubTab == EncyclopediaItemSubTab.Consumable);
    }

    public void SetItemSubTabsInteractable(bool interactable)
    {
        if (weaponSubTabButton != null)
            weaponSubTabButton.interactable = interactable;
        if (relicSubTabButton != null)
            relicSubTabButton.interactable = interactable;
        if (consumableSubTabButton != null)
            consumableSubTabButton.interactable = interactable;
    }

    public void SetPagination(int currentPage, int pageCount)
    {
        bool hasMultiplePages = pageCount > 1;
        if (previousStepButton != null)
            previousStepButton.interactable = hasMultiplePages && currentPage > 0;

        if (nextStepButton != null)
            nextStepButton.interactable = hasMultiplePages && currentPage < pageCount - 1;

        SetText(pageText, pageCount > 0 ? $"{currentPage + 1}/{pageCount}" : "0/0");
    }

    public void SetEntryCount(int shownThrough, int totalCount)
    {
        SetText(entryCountText, $"{shownThrough}/{totalCount}");
    }

    public void SetListNotice(string message)
    {
        if (listNoticeText == null)
            return;

        bool hasMessage = !string.IsNullOrWhiteSpace(message);
        listNoticeText.gameObject.SetActive(hasMessage);
        listNoticeText.text = hasMessage ? message : string.Empty;
    }

    private void RequestWeapons()
    {
        ItemSubTabRequested?.Invoke(EncyclopediaItemSubTab.Weapon);
    }

    private void RequestRelics()
    {
        ItemSubTabRequested?.Invoke(EncyclopediaItemSubTab.Relic);
    }

    private void RequestConsumables()
    {
        ItemSubTabRequested?.Invoke(EncyclopediaItemSubTab.Consumable);
    }

    private void RequestPreviousPage()
    {
        PreviousPageRequested?.Invoke();
    }

    private void RequestNextPage()
    {
        NextPageRequested?.Invoke();
    }

    private static void SetSubTabState(Button button, GameObject selectedMarker, bool selected)
    {
        if (button != null)
            button.interactable = true;

        SetActive(selectedMarker, selected);
    }

    private static void SetImage(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}

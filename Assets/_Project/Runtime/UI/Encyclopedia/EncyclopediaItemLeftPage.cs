using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 책임 : 아이템 도감 왼쪽 페이지의 탭, 페이지네이션, 목록 선택 상태를 표시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class EncyclopediaItemLeftPage : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject contentRoot;

    [Header("Title")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image titleIcon;

    [Header("Sub Tabs")]
    [FormerlySerializedAs("weaponSubTabButton")]
    [SerializeField] private Button weaponButton;
    [FormerlySerializedAs("relicSubTabButton")]
    [SerializeField] private Button relicButton;
    [FormerlySerializedAs("consumableSubTabButton")]
    [SerializeField] private Button consumableButton;
    [SerializeField] private Image weaponTabIcon;
    [SerializeField] private Image relicTabIcon;
    [SerializeField] private Image consumableTabIcon;
    [SerializeField] private GameObject weaponSelectedMarker;
    [SerializeField] private GameObject relicSelectedMarker;
    [SerializeField] private GameObject consumableSelectedMarker;

    [Header("Grid")]
    [SerializeField] private EncyclopediaEntryGridView entryGridView;

    [Header("Pagination")]
    [SerializeField] private Button previousPageButton;
    [SerializeField] private Button nextPageButton;
    [SerializeField] private TMP_Text pageText;
    [SerializeField] private TMP_Text entryCountText;
    [SerializeField] private TMP_Text noticeText;

    private bool listenersBound;
    private bool interactionEnabled = true;
    private bool warnedMissingEntryGridView;

    public event Action<EncyclopediaItemSubTab> SubTabRequested;
    public event Action PreviousPageRequested;
    public event Action NextPageRequested;

    public EncyclopediaEntryGridView EntryGridView
    {
        get { return entryGridView; }
    }

    private void Awake()
    {
        ValidateRequiredReferences();
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
        EditorAuthoringPlayback.MarkDirty(this);
    }
#endif

    private void OnDestroy()
    {
        UnbindListeners();
    }

    public void ResolveReferences()
    {
        if (contentRoot == null)
            contentRoot = EncyclopediaReferenceResolver.FindGameObject(transform, "ItemLeftContent", "ContentRoot", "Content");

        if (titleText == null)
            titleText = EncyclopediaReferenceResolver.FindComponentUnderParent<TMP_Text>(transform, "TitleGroup", "TitleText", "Title", "Text", "Text (TMP)", "Text(TMP)");

        if (titleIcon == null)
            titleIcon = EncyclopediaReferenceResolver.FindComponentUnderParent<Image>(transform, "TitleGroup", "TitleIcon", "Icon", "Decoration");

        if (weaponButton == null)
            weaponButton = EncyclopediaReferenceResolver.FindComponent<Button>(transform, "WeaponTab", "WeaponTabButton", "Weapon", "WeaponButton");

        if (relicButton == null)
            relicButton = EncyclopediaReferenceResolver.FindComponent<Button>(transform, "RelicTab", "RelicTabButton", "Relic", "RelicButton");

        if (consumableButton == null)
            consumableButton = EncyclopediaReferenceResolver.FindComponent<Button>(transform, "ConsumableTab", "ConsumableTabButton", "Consumable", "ConsumableButton");

        if (weaponTabIcon == null && weaponButton != null)
            weaponTabIcon = EncyclopediaReferenceResolver.FindTabIcon(weaponButton.transform);

        if (relicTabIcon == null && relicButton != null)
            relicTabIcon = EncyclopediaReferenceResolver.FindTabIcon(relicButton.transform);

        if (consumableTabIcon == null && consumableButton != null)
            consumableTabIcon = EncyclopediaReferenceResolver.FindTabIcon(consumableButton.transform);

        if (weaponSelectedMarker == null)
            weaponSelectedMarker = EncyclopediaReferenceResolver.FindMarker(transform, "WeaponTab") ??
                EncyclopediaReferenceResolver.FindMarker(transform, "Weapon");

        if (relicSelectedMarker == null)
            relicSelectedMarker = EncyclopediaReferenceResolver.FindMarker(transform, "RelicTab") ??
                EncyclopediaReferenceResolver.FindMarker(transform, "Relic");

        if (consumableSelectedMarker == null)
            consumableSelectedMarker = EncyclopediaReferenceResolver.FindMarker(transform, "ConsumableTab") ??
                EncyclopediaReferenceResolver.FindMarker(transform, "Consumable");

        if (entryGridView == null)
            entryGridView = GetComponentInChildren<EncyclopediaEntryGridView>(true);

        if (previousPageButton == null)
            previousPageButton = EncyclopediaReferenceResolver.FindComponentUnderParent<Button>(transform, "PageButtonGroup", "PreviousStepButton", "PreviousButton", "PrevButton", "Previous", "Prev");

        if (nextPageButton == null)
            nextPageButton = EncyclopediaReferenceResolver.FindComponentUnderParent<Button>(transform, "PageButtonGroup", "NextStepButton", "NextButton", "Next");

        if (pageText == null)
            pageText = EncyclopediaReferenceResolver.FindComponentUnderParent<TMP_Text>(transform, "PageButtonGroup", "PageText", "Page", "Text", "Text (TMP)", "Text(TMP)");

        if (entryCountText == null)
            entryCountText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "EntryCountText", "CountText");

        if (noticeText == null)
            noticeText = EncyclopediaReferenceResolver.FindComponent<TMP_Text>(transform, "ListNoticeText", "NoticeText", "EmptyText");

        if (entryGridView != null)
            entryGridView.ResolveReferences();
    }

    private void ValidateRequiredReferences()
    {
        if (entryGridView == null && !warnedMissingEntryGridView)
        {
            warnedMissingEntryGridView = true;
            Debug.LogWarning("[EncyclopediaItemLeftPage] EntryGridView is not assigned. The item grid cannot be populated.", this);
        }
    }

    public void BindListeners()
    {
        if (listenersBound)
            return;

        if (weaponButton != null)
            weaponButton.onClick.AddListener(RequestWeapons);
        if (relicButton != null)
            relicButton.onClick.AddListener(RequestRelics);
        if (consumableButton != null)
            consumableButton.onClick.AddListener(RequestConsumables);
        if (previousPageButton != null)
            previousPageButton.onClick.AddListener(RequestPreviousPage);
        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(RequestNextPage);

        listenersBound = true;
    }

    public void UnbindListeners()
    {
        if (!listenersBound)
            return;

        if (weaponButton != null)
            weaponButton.onClick.RemoveListener(RequestWeapons);
        if (relicButton != null)
            relicButton.onClick.RemoveListener(RequestRelics);
        if (consumableButton != null)
            consumableButton.onClick.RemoveListener(RequestConsumables);
        if (previousPageButton != null)
            previousPageButton.onClick.RemoveListener(RequestPreviousPage);
        if (nextPageButton != null)
            nextPageButton.onClick.RemoveListener(RequestNextPage);

        listenersBound = false;
    }

    public void SetContentVisible(bool visible)
    {
        if (contentRoot != null)
            contentRoot.SetActive(visible);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        SetButtonInteractable(weaponButton, enabled);
        SetButtonInteractable(relicButton, enabled);
        SetButtonInteractable(consumableButton, enabled);
        SetButtonInteractable(previousPageButton, enabled);
        SetButtonInteractable(nextPageButton, enabled);
    }

    public void SetTitle(string text, Sprite icon)
    {
        SetText(titleText, text);
        SetImage(titleIcon, icon);
    }

    public void SetSubTabState(EncyclopediaItemSubTab selectedSubTab)
    {
        SetActive(weaponSelectedMarker, selectedSubTab == EncyclopediaItemSubTab.Weapon);
        SetActive(relicSelectedMarker, selectedSubTab == EncyclopediaItemSubTab.Relic);
        SetActive(consumableSelectedMarker, selectedSubTab == EncyclopediaItemSubTab.Consumable);
    }

    public void SetPagination(int currentPage, int pageCount)
    {
        bool hasMultiplePages = pageCount > 1;
        if (previousPageButton != null)
            previousPageButton.interactable = interactionEnabled && hasMultiplePages && currentPage > 0;
        if (nextPageButton != null)
            nextPageButton.interactable = interactionEnabled && hasMultiplePages && currentPage < pageCount - 1;

        SetText(pageText, pageCount > 0 ? $"{currentPage + 1}/{pageCount}" : "0/0");
    }

    public void SetEntryCount(int shownThrough, int totalCount)
    {
        SetText(entryCountText, $"{shownThrough}/{totalCount}");
    }

    public void SetNotice(string message)
    {
        if (noticeText == null)
            return;

        bool hasMessage = !string.IsNullOrWhiteSpace(message);
        noticeText.gameObject.SetActive(hasMessage);
        noticeText.text = hasMessage ? message : string.Empty;
    }

    public void RefreshSelection(int selectedIndex)
    {
        EntryGridView?.RefreshSelection(selectedIndex);
    }

    public void SettleLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (titleText != null)
        {
            titleText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(titleText.rectTransform);
        }

        if (entryGridView != null && entryGridView.transform is RectTransform entryGridRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(entryGridRect);

        if (contentRoot != null && contentRoot.transform is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        Canvas.ForceUpdateCanvases();
    }

    public void ClearSlots()
    {
        EntryGridView?.Clear();
    }

    private void RequestWeapons()
    {
        SubTabRequested?.Invoke(EncyclopediaItemSubTab.Weapon);
    }

    private void RequestRelics()
    {
        SubTabRequested?.Invoke(EncyclopediaItemSubTab.Relic);
    }

    private void RequestConsumables()
    {
        SubTabRequested?.Invoke(EncyclopediaItemSubTab.Consumable);
    }

    private void RequestPreviousPage()
    {
        PreviousPageRequested?.Invoke();
    }

    private void RequestNextPage()
    {
        NextPageRequested?.Invoke();
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private static void SetImage(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.enabled = sprite != null;
        image.gameObject.SetActive(sprite != null);
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

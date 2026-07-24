using System;
using System.Collections.Generic;
using CapstoneAudio;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EncyclopediaItemTab : MonoBehaviour
{
    private static readonly SoundRef CategoryChangeStartSound = SoundRef.FromKey("sound_ui_ChangeDictionaryCategoryStart");
    private static readonly SoundRef CategoryChangeFlipPageSound = SoundRef.FromKey("sound_ui_ChangeDictionaryCategoryFlipPage");
    private static readonly SoundRef CategoryChangeEndSound = SoundRef.FromKey("sound_ui_ChangeDictionaryCategoryEnd");

    [Serializable]
    private struct TitlePreset
    {
        public string text;
        public Sprite icon;

        public TitlePreset(string text)
        {
            this.text = text;
            icon = null;
        }
    }

    [Header("Data")]
    [SerializeField] private ItemDatabase itemDatabase;

    [Header("Pages")]
    [SerializeField] private EncyclopediaItemLeftPage leftPage;
    [SerializeField] private EncyclopediaItemRightPage rightPage;

    [Header("Presentation")]
    [SerializeField] private EncyclopediaBookPresentation bookPresentation;
    [SerializeField] private bool playLeftPageTurnOnSubTabChange = true;

    [Header("Titles")]
    [SerializeField] private TitlePreset weaponTitle = new("무기");
    [SerializeField] private TitlePreset relicTitle = new("유물");
    [SerializeField] private TitlePreset consumableTitle = new("소모품");

    private EncyclopediaItemSubTab currentSubTab = EncyclopediaItemSubTab.Weapon;
    private int currentPage;
    private int selectedIndex = -1;
    private EncyclopediaCategory selectedCategory = EncyclopediaCategory.Weapon;
    private ScriptableObject selectedItem;
    private bool listenersBound;
    private bool interactionEnabled = true;
    private bool contentVisible;
    private bool warnedMissingItemDatabase;
    private bool warnedMissingLeftPage;
    private bool warnedMissingRightPage;

    public bool HasDataSource => itemDatabase != null;

    private EncyclopediaCategory CurrentCategory => GetCategoryForSubTab(currentSubTab);

    private void Awake()
    {
        ValidateRequiredReferences();
        BindListeners();
    }

    private void Update()
    {
        HandleGridNavigationInput();
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
        if (leftPage == null)
            leftPage = GetComponentInChildren<EncyclopediaItemLeftPage>(true);

        if (rightPage == null)
            rightPage = GetComponentInChildren<EncyclopediaItemRightPage>(true);

        if (bookPresentation == null)
            bookPresentation = GetComponentInChildren<EncyclopediaBookPresentation>(true);

        leftPage?.ResolveReferences();
        rightPage?.ResolveReferences();
    }

    public void BindListeners()
    {
        if (listenersBound)
            return;

        if (leftPage != null)
        {
            leftPage.BindListeners();
            leftPage.SubTabRequested += RequestSubTab;
            leftPage.PreviousPageRequested += RequestPreviousPage;
            leftPage.NextPageRequested += RequestNextPage;
        }

        listenersBound = true;
    }

    public void UnbindListeners()
    {
        if (!listenersBound)
            return;

        if (leftPage != null)
        {
            leftPage.SubTabRequested -= RequestSubTab;
            leftPage.PreviousPageRequested -= RequestPreviousPage;
            leftPage.NextPageRequested -= RequestNextPage;
            leftPage.UnbindListeners();
        }

        listenersBound = false;
    }

    public void SetItemDatabase(ItemDatabase database)
    {
        itemDatabase = database;
    }

    public void SetBookPresentation(EncyclopediaBookPresentation presentation)
    {
        bookPresentation = presentation;
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        leftPage?.SetInteractionEnabled(enabled);

        if (enabled)
            RefreshPaginationState(GetPageCount(GetEntryCount(CurrentCategory), GetCurrentPageCapacity()));
    }

    public void SetContentVisible(bool visible)
    {
        contentVisible = visible;
        leftPage?.SetContentVisible(visible);
        rightPage?.SetContentVisible(visible);
    }

    public void ShowDefault()
    {
        currentSubTab = EncyclopediaItemSubTab.Weapon;
        currentPage = 0;
        ClearSelection();
        SetContentVisible(true);
        ApplyCurrentState(selectFirst: true);
    }

    public void ShowCurrent()
    {
        SetContentVisible(true);
        ApplyCurrentState(selectFirst: selectedIndex < 0 || selectedItem == null);
    }

    public void OpenWeaponSubTab()
    {
        RequestSubTab(EncyclopediaItemSubTab.Weapon);
    }

    public void OpenRelicSubTab()
    {
        RequestSubTab(EncyclopediaItemSubTab.Relic);
    }

    public void OpenConsumableSubTab()
    {
        RequestSubTab(EncyclopediaItemSubTab.Consumable);
    }

    public void RequestPreviousPage()
    {
        if (currentPage <= 0)
            return;

        currentPage--;
        ClearSelection();
        Rebuild(selectFirst: true);
    }

    public void RequestNextPage()
    {
        int entryCount = GetEntryCount(CurrentCategory);
        int pageCount = GetPageCount(entryCount, GetCurrentPageCapacity());
        if (currentPage >= pageCount - 1)
            return;

        currentPage++;
        ClearSelection();
        Rebuild(selectFirst: true);
    }

    public void Clear()
    {
        contentVisible = false;
        ClearSelection();
        leftPage?.ClearSlots();
        rightPage?.Clear();
        SettleCurrentLayout();
    }

    private void HandleGridNavigationInput()
    {
        if (!CanHandleGridNavigation())
            return;

        InputBindingService input = InputBindingService.EnsureInstance();
        if (input.WasPressedThisFrame(InputActionId.MoveRight))
            MoveSelectionLinear(1);
        else if (input.WasPressedThisFrame(InputActionId.MoveLeft))
            MoveSelectionLinear(-1);
        else if (input.WasPressedThisFrame(InputActionId.MoveDown))
            MoveSelectionVertical(1);
        else if (input.WasPressedThisFrame(InputActionId.MoveUp))
            MoveSelectionVertical(-1);
    }

    private bool CanHandleGridNavigation()
    {
        return contentVisible &&
               interactionEnabled &&
               isActiveAndEnabled &&
               gameObject.activeInHierarchy &&
               GetEntryCount(CurrentCategory) > 0;
    }

    private void MoveSelectionLinear(int direction)
    {
        int entryCount = GetEntryCount(CurrentCategory);
        if (entryCount <= 0)
            return;

        int targetIndex = selectedIndex >= 0
            ? selectedIndex + direction
            : GetCurrentPageStartIndex();
        if (targetIndex < 0 || targetIndex >= entryCount)
            return;

        SelectIndexFromNavigation(targetIndex);
    }

    private void MoveSelectionVertical(int rowDirection)
    {
        int entryCount = GetEntryCount(CurrentCategory);
        int pageCapacity = GetCurrentPageCapacity();
        int pageStartIndex = currentPage * pageCapacity;
        int visibleCount = Mathf.Clamp(entryCount - pageStartIndex, 0, pageCapacity);
        if (visibleCount <= 0)
            return;

        if (selectedIndex < pageStartIndex || selectedIndex >= pageStartIndex + visibleCount)
        {
            SelectIndexFromNavigation(pageStartIndex);
            return;
        }

        int columns = GetGridNavigationColumnCount();
        int selectedOffset = selectedIndex - pageStartIndex;
        int selectedColumn = selectedOffset % columns;
        int targetOffset = selectedOffset + rowDirection * columns;

        if (targetOffset < 0)
            targetOffset = GetBottomVisibleOffsetInColumn(selectedColumn, columns, visibleCount);
        else if (targetOffset >= visibleCount)
            targetOffset = selectedColumn;

        if (targetOffset == selectedOffset || targetOffset < 0 || targetOffset >= visibleCount)
            return;

        SelectIndexFromNavigation(pageStartIndex + targetOffset);
    }

    private void SelectIndexFromNavigation(int targetIndex)
    {
        int entryCount = GetEntryCount(CurrentCategory);
        if (targetIndex < 0 || targetIndex >= entryCount)
            return;

        int pageCapacity = GetCurrentPageCapacity();
        int targetPage = targetIndex / pageCapacity;
        if (targetPage != currentPage)
        {
            currentPage = targetPage;
            selectedIndex = targetIndex;
            Rebuild(selectFirst: false);
            return;
        }

        SelectEntry(CurrentCategory, targetIndex);
    }

    private int GetCurrentPageStartIndex()
    {
        return currentPage * GetCurrentPageCapacity();
    }

    private int GetGridNavigationColumnCount()
    {
        EncyclopediaEntryGridView gridView = leftPage != null ? leftPage.EntryGridView : null;
        if (gridView != null)
            return Mathf.Max(1, gridView.NavigationColumnCount);

        return Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(GetCurrentPageCapacity())));
    }

    private static int GetBottomVisibleOffsetInColumn(int column, int columns, int visibleCount)
    {
        int bottomOffset = column;
        while (bottomOffset + columns < visibleCount)
            bottomOffset += columns;

        return bottomOffset;
    }

    private void RequestSubTab(EncyclopediaItemSubTab subTab)
    {
        if (subTab == currentSubTab)
            return;

        PlayCategoryChangeStartSound();
        SetInteractionEnabled(false);

        void Swap()
        {
            ApplySubTab(subTab, selectFirst: true);
            PlayCategoryChangeEndSound();
        }

        void Complete()
        {
            SetContentVisible(true);
            SetInteractionEnabled(true);
        }

        if (bookPresentation != null && playLeftPageTurnOnSubTabChange)
            bookPresentation.PlayLeftPageTurn(Swap, Complete, PlayCategoryChangeFlipPageSound);
        else
        {
            SetContentVisible(true);
            PlayCategoryChangeFlipPageSound();
            Swap();
            Complete();
        }
    }

    private void PlayCategoryChangeStartSound()
    {
        SoundPlaybackUtility.Play(CategoryChangeStartSound, sourceObject: this);
    }

    private void PlayCategoryChangeFlipPageSound()
    {
        SoundPlaybackUtility.Play(CategoryChangeFlipPageSound, sourceObject: this);
    }

    private void PlayCategoryChangeEndSound()
    {
        SoundPlaybackUtility.Play(CategoryChangeEndSound, sourceObject: this);
    }

    private void ApplySubTab(EncyclopediaItemSubTab subTab, bool selectFirst)
    {
        currentSubTab = subTab;
        currentPage = 0;
        ClearSelection();
        ApplyCurrentState(selectFirst);
    }

    private void ApplyCurrentState(bool selectFirst)
    {
        ValidateRequiredReferences();
        RefreshTitle();
        leftPage?.SetSubTabState(currentSubTab);
        Rebuild(selectFirst);
        SettleCurrentLayout();
    }

    private void ValidateRequiredReferences()
    {
        if (itemDatabase == null && !warnedMissingItemDatabase)
        {
            warnedMissingItemDatabase = true;
            Debug.LogWarning("[EncyclopediaItemTab] ItemDatabase is not assigned. Item encyclopedia entries cannot be populated.", this);
        }

        if (leftPage == null && !warnedMissingLeftPage)
        {
            warnedMissingLeftPage = true;
            Debug.LogWarning("[EncyclopediaItemTab] EncyclopediaItemLeftPage is not assigned. Item tab buttons and grid cannot be populated.", this);
        }

        if (rightPage == null && !warnedMissingRightPage)
        {
            warnedMissingRightPage = true;
            Debug.LogWarning("[EncyclopediaItemTab] EncyclopediaItemRightPage is not assigned. Item detail cannot be displayed.", this);
        }
    }

    private void Rebuild(bool selectFirst)
    {
        EncyclopediaCategory category = CurrentCategory;
        EncyclopediaEntryGridView gridView = leftPage != null ? leftPage.EntryGridView : null;
        int entryCount = GetEntryCount(category);
        int pageCapacity = GetCurrentPageCapacity();
        int pageCount = GetPageCount(entryCount, pageCapacity);
        currentPage = pageCount > 0 ? Mathf.Clamp(currentPage, 0, pageCount - 1) : 0;
        int pageStartIndex = currentPage * pageCapacity;

        int availableSlotCount = 0;
        int visibleCount = 0;
        bool hasAnySlotAuthoring = gridView != null && gridView.HasAnySlotAuthoring;
        bool hasRuntimeSlotAuthoring = gridView != null && gridView.HasRuntimeSlotAuthoring;

        if (gridView != null)
        {
            availableSlotCount = gridView.Populate(
                category,
                pageStartIndex,
                entryCount,
                selectedIndex,
                ResolveDisplayName,
                ResolveIconItem,
                ResolveFallbackIcon,
                SelectEntry,
                out visibleCount);
        }

        int shownThrough = visibleCount > 0 ? pageStartIndex + visibleCount : 0;
        leftPage?.SetEntryCount(shownThrough, entryCount);
        RefreshPaginationState(pageCount);
        leftPage?.SetNotice(BuildNotice(entryCount, availableSlotCount, hasRuntimeSlotAuthoring, hasAnySlotAuthoring));

        if (entryCount == 0 || visibleCount == 0)
        {
            selectedCategory = category;
            selectedIndex = -1;
            selectedItem = null;
            leftPage?.RefreshSelection(selectedIndex);
            rightPage?.Clear();
            SettleCurrentLayout();
            return;
        }

        bool selectedOnPage = selectedIndex >= pageStartIndex && selectedIndex < pageStartIndex + visibleCount;
        if (selectFirst || !selectedOnPage)
            SelectEntry(category, pageStartIndex);
        else
            SelectEntry(category, selectedIndex);

        SettleCurrentLayout();
    }

    private int GetCurrentPageCapacity()
    {
        EncyclopediaEntryGridView gridView = leftPage != null ? leftPage.EntryGridView : null;
        return gridView != null ? Mathf.Max(1, gridView.SlotsPerPage) : 16;
    }

    private static int GetPageCount(int entryCount, int pageCapacity)
    {
        if (entryCount <= 0)
            return 0;

        return Mathf.CeilToInt(entryCount / (float)Mathf.Max(1, pageCapacity));
    }

    private void RefreshPaginationState(int pageCount)
    {
        leftPage?.SetPagination(currentPage, pageCount);
    }

    private void RefreshTitle()
    {
        TitlePreset preset = currentSubTab switch
        {
            EncyclopediaItemSubTab.Relic => relicTitle,
            EncyclopediaItemSubTab.Consumable => consumableTitle,
            _ => weaponTitle
        };

        string title = !string.IsNullOrWhiteSpace(preset.text)
            ? preset.text
            : currentSubTab switch
            {
                EncyclopediaItemSubTab.Relic => "유물",
                EncyclopediaItemSubTab.Consumable => "소모품",
                _ => "무기"
            };

        leftPage?.SetTitle(title, preset.icon);
    }

    private string BuildNotice(int entryCount, int availableSlotCount, bool hasRuntimeSlotAuthoring, bool hasAnySlotAuthoring)
    {
        if (itemDatabase == null)
            return "ItemDatabase가 연결되지 않았습니다.";

        if (!hasRuntimeSlotAuthoring && !hasAnySlotAuthoring)
            return "EntryGridRoot 또는 슬롯 프리팹이 연결되지 않았습니다.";

        if (entryCount == 0)
            return "등록된 항목이 없습니다.";

        if (availableSlotCount <= 0)
            return "표시할 슬롯이 없습니다.";

        return null;
    }

    private int GetEntryCount(EncyclopediaCategory category)
    {
        return category switch
        {
            EncyclopediaCategory.Weapon => GetNonNullCount(itemDatabase != null ? itemDatabase.allWeapons : null),
            EncyclopediaCategory.Relic => GetNonNullCount(itemDatabase != null ? itemDatabase.allRelics : null),
            EncyclopediaCategory.Consumable => GetNonNullCount(itemDatabase != null ? itemDatabase.allConsumables : null),
            _ => 0
        };
    }

    private string ResolveDisplayName(EncyclopediaCategory category, int index)
    {
        ScriptableObject definition = ResolveItem(category, index);
        return definition is IInventoryItemDefinition item && !string.IsNullOrWhiteSpace(item.DisplayName)
            ? item.DisplayName
            : definition != null ? definition.name : string.Empty;
    }

    private ScriptableObject ResolveIconItem(EncyclopediaCategory category, int index)
    {
        return ResolveItem(category, index);
    }

    private Sprite ResolveFallbackIcon(EncyclopediaCategory category, int index)
    {
        return ResolveItem(category, index) is IInventoryItemDefinition item ? item.Icon : null;
    }

    private ScriptableObject ResolveItem(EncyclopediaCategory category, int index)
    {
        return category switch
        {
            EncyclopediaCategory.Weapon => TryGetNonNullItem(itemDatabase != null ? itemDatabase.allWeapons : null, index, out WeaponDefinition weapon) ? weapon : null,
            EncyclopediaCategory.Relic => TryGetNonNullItem(itemDatabase != null ? itemDatabase.allRelics : null, index, out RelicDefinition relic) ? relic : null,
            EncyclopediaCategory.Consumable => TryGetNonNullItem(itemDatabase != null ? itemDatabase.allConsumables : null, index, out ConsumableDefinition consumable) ? consumable : null,
            _ => null
        };
    }

    private void SelectEntry(EncyclopediaCategory category, int index)
    {
        if (category != CurrentCategory)
            return;

        ScriptableObject item = ResolveItem(category, index);
        if (item == null)
        {
            selectedCategory = category;
            selectedIndex = -1;
            selectedItem = null;
            leftPage?.RefreshSelection(selectedIndex);
            rightPage?.Clear();
            SettleCurrentLayout();
            return;
        }

        bool isSameSelection = selectedCategory == category && selectedIndex == index && selectedItem == item;
        selectedIndex = index;
        selectedCategory = category;
        selectedItem = item;
        leftPage?.RefreshSelection(selectedIndex);

        if (isSameSelection)
        {
            SettleCurrentLayout();
            return;
        }

        switch (item)
        {
            case WeaponDefinition weapon:
                rightPage?.ShowWeapon(weapon);
                break;
            case RelicDefinition relic:
                rightPage?.ShowRelic(relic);
                break;
            case ConsumableDefinition consumable:
                rightPage?.ShowConsumable(consumable);
                break;
            default:
                rightPage?.Clear();
                break;
        }

        SettleCurrentLayout();
    }

    private void ClearSelection()
    {
        selectedCategory = CurrentCategory;
        selectedIndex = -1;
        selectedItem = null;
    }

    private void SettleCurrentLayout()
    {
        leftPage?.SettleLayout();
        rightPage?.SettleLayout();
        Canvas.ForceUpdateCanvases();
    }

    private static EncyclopediaCategory GetCategoryForSubTab(EncyclopediaItemSubTab subTab)
    {
        return subTab switch
        {
            EncyclopediaItemSubTab.Relic => EncyclopediaCategory.Relic,
            EncyclopediaItemSubTab.Consumable => EncyclopediaCategory.Consumable,
            _ => EncyclopediaCategory.Weapon
        };
    }

    private static int GetNonNullCount<T>(IReadOnlyList<T> entries) where T : UnityEngine.Object
    {
        if (entries == null)
            return 0;

        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
                count++;
        }

        return count;
    }

    private static bool TryGetNonNullItem<T>(IReadOnlyList<T> entries, int index, out T item) where T : UnityEngine.Object
    {
        item = null;
        if (entries == null || index < 0)
            return false;

        int currentIndex = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            T candidate = entries[i];
            if (candidate == null)
                continue;

            if (currentIndex == index)
            {
                item = candidate;
                return true;
            }

            currentIndex++;
        }

        return false;
    }
}

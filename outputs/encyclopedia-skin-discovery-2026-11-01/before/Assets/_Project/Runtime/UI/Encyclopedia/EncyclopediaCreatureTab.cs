using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>Owns only monster/boss/NPC encyclopedia paging and selection; reads existing profile state.</summary>
[DisallowMultipleComponent]
public sealed class EncyclopediaCreatureTab : MonoBehaviour
{
    [SerializeField] private EncyclopediaCatalogSO catalog;

    [Header("Authored Pages")]
    [SerializeField] private GameObject leftContentRoot;
    [SerializeField] private GameObject rightContentRoot;
    [SerializeField] private CanvasGroup interactionCanvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text pageText;
    [SerializeField] private TMP_Text entryCountText;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private UnityEngine.UI.Button previousPageButton;
    [SerializeField] private UnityEngine.UI.Button nextPageButton;

    [Header("Authored Slot Sets")]
    [SerializeField] private GameObject monsterGridRoot;
    [SerializeField] private GameObject characterGridRoot;
    [SerializeField] private EncyclopediaCharacterEntryView[] monsterSlots = Array.Empty<EncyclopediaCharacterEntryView>();
    [SerializeField] private EncyclopediaCharacterEntryView[] characterSlots = Array.Empty<EncyclopediaCharacterEntryView>();
    [SerializeField] private EncyclopediaCreatureDetailView detailView;

    private readonly int[] pageIndices = new int[4];
    private readonly int[] selectedIndices = { -1, -1, -1, -1 };
    private EncyclopediaMainTab activeTab = EncyclopediaMainTab.Monster;
    private bool showing;
    private bool interactionEnabled = true;
    private AffectionManager subscribedAffectionManager;

    private int TabIndex => (int)activeTab;
    public bool HasDataSource => catalog != null;
    private EncyclopediaCharacterEntryView[] ActiveSlots => activeTab == EncyclopediaMainTab.Monster
        ? monsterSlots : characterSlots;
    private int EntryCount => catalog == null ? 0 : activeTab switch
    {
        EncyclopediaMainTab.Monster => catalog.MonsterEntries != null ? catalog.MonsterEntries.Count : 0,
        EncyclopediaMainTab.Boss => catalog.BossEntries != null ? catalog.BossEntries.Count : 0,
        EncyclopediaMainTab.NPC => catalog.NpcEntries != null ? catalog.NpcEntries.Count : 0,
        _ => 0
    };
    private int PageSize => ActiveSlots.Length;
    private int PageCount => PageSize > 0 ? Mathf.CeilToInt((float)EntryCount / PageSize) : 0;

    private void Awake()
    {
        if (previousPageButton != null)
            previousPageButton.onClick.AddListener(ShowPreviousPage);
        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(ShowNextPage);
        SubscribeSlots(monsterSlots, true);
        SubscribeSlots(characterSlots, true);
        if (detailView != null)
            detailView.PortraitChanged += HandlePortraitChanged;
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        GameDataStore.OnDataLoaded += HandleProfileLoaded;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        GameDataStore.OnDataLoaded -= HandleProfileLoaded;
        UnsubscribeAffection();
        Hide();
    }

    private void OnDestroy()
    {
        if (previousPageButton != null)
            previousPageButton.onClick.RemoveListener(ShowPreviousPage);
        if (nextPageButton != null)
            nextPageButton.onClick.RemoveListener(ShowNextPage);
        SubscribeSlots(monsterSlots, false);
        SubscribeSlots(characterSlots, false);
        if (detailView != null)
            detailView.PortraitChanged -= HandlePortraitChanged;
    }

    private void Update()
    {
        if (!showing || !interactionEnabled || PageSize == 0 || EntryCount == 0 ||
            (detailView != null && detailView.IsFullViewOpen))
            return;
        GameObject selectedObject = UnityEngine.EventSystems.EventSystem.current != null
            ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        if (selectedObject != null && (selectedObject.GetComponent<TMP_InputField>() != null ||
            selectedObject.GetComponent<UnityEngine.UI.InputField>() != null))
            return;
        InputBindingService input = InputBindingService.Instance;
        if (input == null)
            return;
        if (input.WasPressedThisFrame(InputActionId.MoveRight))
            SelectEntry(selectedIndices[TabIndex] + 1);
        else if (input.WasPressedThisFrame(InputActionId.MoveLeft))
            SelectEntry(selectedIndices[TabIndex] - 1);
        else if (input.WasPressedThisFrame(InputActionId.MoveDown))
            MoveSelectionVertical(1);
        else if (input.WasPressedThisFrame(InputActionId.MoveUp))
            MoveSelectionVertical(-1);
        else if (activeTab != EncyclopediaMainTab.Monster && detailView != null)
        {
            if (input.WasPressedThisFrame(InputContextShortcutId.RelicPreviewPrevious))
                detailView.StepPortrait(-1);
            else if (input.WasPressedThisFrame(InputContextShortcutId.RelicPreviewNext))
                detailView.StepPortrait(1);
        }
    }

    public void SetCatalog(EncyclopediaCatalogSO value)
    {
        catalog = value;
        if (showing)
            Refresh(true);
    }

    public void Show(EncyclopediaMainTab tab)
    {
        if (tab != EncyclopediaMainTab.Monster && tab != EncyclopediaMainTab.Boss && tab != EncyclopediaMainTab.NPC)
        {
            Hide();
            return;
        }
        bool changedTab = !showing || activeTab != tab;
        activeTab = tab;
        showing = true;
        SetActive(leftContentRoot, true);
        SetActive(rightContentRoot, true);
        SubscribeAffection();
        Refresh(changedTab);
    }

    public void Hide()
    {
        showing = false;
        UnsubscribeAffection();
        if (detailView != null)
            detailView.Clear();
        SetActive(leftContentRoot, false);
        SetActive(rightContentRoot, false);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        if (interactionCanvasGroup != null)
        {
            interactionCanvasGroup.interactable = enabled;
            interactionCanvasGroup.blocksRaycasts = enabled;
        }
        SetSlotsInteraction(monsterSlots, enabled);
        SetSlotsInteraction(characterSlots, enabled);
        if (detailView != null)
            detailView.SetInteractionEnabled(enabled);
        RefreshPageButtons();
    }

    public bool TryCloseFullView() => detailView != null && detailView.TryCloseFullView();

    private void Refresh(bool resetDetail)
    {
        if (!showing)
            return;

        int count = EntryCount;
        int pages = PageCount;
        int page = Mathf.Clamp(pageIndices[TabIndex], 0, Mathf.Max(0, pages - 1));
        pageIndices[TabIndex] = page;
        int start = page * PageSize;
        if (selectedIndices[TabIndex] < start || selectedIndices[TabIndex] >= Math.Min(start + PageSize, count))
        {
            selectedIndices[TabIndex] = count > 0 && PageSize > 0 ? start : -1;
            resetDetail = true;
        }
        SetActive(monsterGridRoot, activeTab == EncyclopediaMainTab.Monster);
        SetActive(characterGridRoot, activeTab != EncyclopediaMainTab.Monster);
        SetText(titleText, activeTab switch
        {
            EncyclopediaMainTab.Monster => GameText.Get("code.encyclopediadetailpanel.fa2390684b", "몬스터"),
            EncyclopediaMainTab.Boss => GameText.Get("code.encyclopediadetailpanel.792e7e43e5", "보스"),
            _ => GameText.Get("encyclopedia.tab_npc", "NPC")
        });
        SetText(pageText, GameText.Format("encyclopedia.page_indicator", "{0} / {1}", pages > 0 ? page + 1 : 0, pages));
        SetText(entryCountText, GameText.Format("encyclopedia.entry_count", "{0}종", count));
        SetText(emptyText, GameText.Get("encyclopedia.entries_pending", "등록된 항목 없음"));
        SetActive(emptyText != null ? emptyText.gameObject : null, count == 0);

        EncyclopediaCharacterEntryView[] slots = ActiveSlots;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;
            int index = start + i;
            if (index < count)
                BindSlot(slots[i], index);
            else
                slots[i].Clear();
        }
        RefreshDetail(resetDetail);
        RefreshPageButtons();
    }

    private void BindSlot(EncyclopediaCharacterEntryView slot, int index)
    {
        bool selected = index == selectedIndices[TabIndex];
        if (activeTab == EncyclopediaMainTab.Monster && catalog.TryGetMonster(index, out EncyclopediaMonsterEntry monster))
        {
            Sprite preview = monster.previewFrames != null && monster.previewFrames.Length > 0
                ? monster.previewFrames[0] : monster.image;
            slot.Bind(index, monster.DisplayName, preview, false, 0, selected);
        }
        else if (activeTab == EncyclopediaMainTab.Boss && catalog.TryGetBoss(index, out EncyclopediaBossEntry boss))
            BindCharacterSlot(slot, index, boss.npcData != null ? boss.npcData.DisplayName : boss.DisplayName,
                boss.npcData, boss.portraits, selected);
        else if (activeTab == EncyclopediaMainTab.NPC && catalog.TryGetNpc(index, out EncyclopediaNpcEntry npc))
            BindCharacterSlot(slot, index, npc.DisplayName, npc.npcData, npc.portraits, selected);
        else
            slot.Clear();
    }

    private static void BindCharacterSlot(EncyclopediaCharacterEntryView slot, int index, string title,
        NPCData npc, IReadOnlyList<EncyclopediaPortrait> portraits, bool selected)
    {
        int portrait = EncyclopediaPortraitUtility.GetDefaultIndex(portraits);
        slot.Bind(index, title, portrait >= 0 ? portraits[portrait].sprite : null, true, GetAffection(npc), selected);
    }

    private void RefreshDetail(bool resetDetail)
    {
        if (detailView == null)
            return;
        int selected = selectedIndices[TabIndex];
        if (catalog == null || selected < 0)
        {
            detailView.Clear();
            return;
        }
        if (activeTab == EncyclopediaMainTab.Monster && catalog.TryGetMonster(selected, out EncyclopediaMonsterEntry monster))
            detailView.ShowMonster(monster, MonsterEncyclopediaProgress.GetKillCount(monster.id), resetDetail);
        else if (activeTab == EncyclopediaMainTab.Boss && catalog.TryGetBoss(selected, out EncyclopediaBossEntry boss))
            detailView.ShowCharacter(boss.npcData != null ? boss.npcData.DisplayName : boss.DisplayName,
                boss.StoryText, boss.npcData, boss.portraits, GetAffection(boss.npcData), resetDetail);
        else if (activeTab == EncyclopediaMainTab.NPC && catalog.TryGetNpc(selected, out EncyclopediaNpcEntry npc))
            detailView.ShowCharacter(npc.DisplayName, npc.StoryText, npc.npcData, npc.portraits,
                GetAffection(npc.npcData), resetDetail);
        else
            detailView.Clear();
    }

    private void SelectEntry(int index)
    {
        if (!showing || !interactionEnabled || index < 0 || index >= EntryCount || index == selectedIndices[TabIndex])
            return;
        selectedIndices[TabIndex] = index;
        pageIndices[TabIndex] = index / PageSize;
        Refresh(true);
    }

    private void MoveSelectionVertical(int direction)
    {
        int columns = activeTab == EncyclopediaMainTab.Monster ? 4 : 2;
        int start = pageIndices[TabIndex] * PageSize;
        int visible = Math.Min(PageSize, EntryCount - start);
        int offset = selectedIndices[TabIndex] - start;
        int column = offset % columns;
        int target = offset + direction * columns;
        if (target < 0)
            target = column + ((visible - 1 - column) / columns) * columns;
        else if (target >= visible)
            target = column;
        if (target >= 0 && target < visible)
            SelectEntry(start + target);
    }

    private void HandlePortraitChanged(Sprite portrait)
    {
        if (!showing || activeTab == EncyclopediaMainTab.Monster || PageSize == 0)
            return;
        int slotIndex = selectedIndices[TabIndex] - pageIndices[TabIndex] * PageSize;
        if (slotIndex >= 0 && slotIndex < characterSlots.Length && characterSlots[slotIndex] != null)
            characterSlots[slotIndex].SetPortrait(portrait);
    }

    private void ShowPreviousPage() => ChangePage(-1);
    private void ShowNextPage() => ChangePage(1);

    private void ChangePage(int delta)
    {
        if (!showing || !interactionEnabled)
            return;
        int page = pageIndices[TabIndex] + delta;
        if (page < 0 || page >= PageCount)
            return;
        pageIndices[TabIndex] = page;
        selectedIndices[TabIndex] = page * PageSize;
        Refresh(true);
    }

    private void RefreshPageButtons()
    {
        if (previousPageButton != null)
            previousPageButton.interactable = interactionEnabled && showing && pageIndices[TabIndex] > 0;
        if (nextPageButton != null)
            nextPageButton.interactable = interactionEnabled && showing && pageIndices[TabIndex] + 1 < PageCount;
    }

    private void SubscribeSlots(EncyclopediaCharacterEntryView[] slots, bool subscribe)
    {
        foreach (EncyclopediaCharacterEntryView slot in slots)
        {
            if (slot == null)
                continue;
            if (subscribe)
                slot.Selected += SelectEntry;
            else
                slot.Selected -= SelectEntry;
        }
    }

    private static void SetSlotsInteraction(EncyclopediaCharacterEntryView[] slots, bool enabled)
    {
        foreach (EncyclopediaCharacterEntryView slot in slots)
            if (slot != null)
                slot.SetInteractionEnabled(enabled);
    }

    private void SubscribeAffection()
    {
        AffectionManager manager = AffectionManager.Instance;
        if (manager == subscribedAffectionManager)
            return;
        UnsubscribeAffection();
        subscribedAffectionManager = manager;
        if (subscribedAffectionManager != null)
            subscribedAffectionManager.OnAffectionChanged += HandleAffectionChanged;
    }

    private void UnsubscribeAffection()
    {
        if (subscribedAffectionManager != null)
            subscribedAffectionManager.OnAffectionChanged -= HandleAffectionChanged;
        subscribedAffectionManager = null;
    }

    private static int GetAffection(NPCData npc)
    {
        if (npc == null)
            return 0;
        if (AffectionManager.Instance != null)
            return AffectionManager.Instance.GetAffection(npc.id);
        IReadOnlyList<AffectionRecord> records = GameDataStore.Data?.affectionData?.affectionRecords;
        if (records != null)
            for (int i = 0; i < records.Count; i++)
                if (records[i] != null && records[i].npcId == npc.id)
                    return records[i].amount;
        return 0;
    }

    private void HandleLocaleChanged(Locale locale) => Refresh(false);
    private void HandleProfileLoaded(GameData data, int slotIndex) => Refresh(false);
    private void HandleAffectionChanged(int npcId, int value) => Refresh(false);

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value ?? string.Empty;
    }

    private static void SetActive(GameObject root, bool active)
    {
        if (root != null)
            root.SetActive(active);
    }
}

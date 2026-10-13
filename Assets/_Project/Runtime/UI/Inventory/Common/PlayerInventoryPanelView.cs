using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventoryPanelView : MonoBehaviour
{
    private const int RelicVisibleSlotCount = 24;

    [Header("Slot Roots")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Transform consumableGridRoot;
    [SerializeField] private Transform weaponGridRoot;
    [SerializeField] private Transform relicGridRoot;

    [Header("Presentation Bounds")]
    [SerializeField] private RectTransform collisionBounds;

    [Header("Support Views")]
    [SerializeField] private PlayerStatPanelView playerStatPanel;
    [SerializeField] private DropZoneUI dropZone;

    [Header("Slot Prefabs")]
    [SerializeField] private ItemSlotUI consumableSlotPrefab;
    [SerializeField] private ItemSlotUI weaponSlotPrefab;
    [SerializeField] private ItemSlotUI relicSlotPrefab;

    private readonly List<ItemSlotUI> spawnedSlots = new();
    private readonly SlotGroup consumableSlots = new();
    private readonly SlotGroup weaponSlots = new();
    private readonly SlotGroup relicSlots = new();

    private sealed class SlotGroup
    {
        public Transform Root;
        public ItemSlotUI Prefab;
        public readonly List<ItemSlotUI> Slots = new();
    }

    private IDisposable consumableDisposer;
    private IDisposable weaponDisposer;
    private IDisposable relicDisposer;

    public IItemContainer ConsumableContainer { get; private set; }
    public IItemContainer WeaponContainer { get; private set; }
    public IItemContainer RelicContainer { get; private set; }
    public RectTransform RectTransform => panelRoot != null ? panelRoot : transform as RectTransform;
    public RectTransform CollisionBoundsRect => ResolveCollisionBoundsRect();
    public PlayerStatPanelView PlayerStatPanel => playerStatPanel;
    public DropZoneUI DropZone => dropZone;
    public RectTransform PlayerStatPanelRect => playerStatPanel != null
        ? playerStatPanel.transform as RectTransform
        : null;

    private void OnDisable()
    {
        ClearBinding();
    }

    private void OnDestroy()
    {
        ClearBinding();
        DiscardSlots(consumableSlots);
        DiscardSlots(weaponSlots);
        DiscardSlots(relicSlots);
        spawnedSlots.Clear();
    }

    public void Configure(
        Transform consumableRoot,
        Transform weaponRoot,
        Transform relicRoot,
        PlayerStatPanelView statPanel,
        ItemSlotUI consumablePrefab,
        ItemSlotUI weaponPrefab,
        ItemSlotUI relicPrefab,
        DropZoneUI dropZoneView,
        RectTransform panelRootOverride = null)
    {
        panelRoot ??= panelRootOverride;
        consumableGridRoot ??= consumableRoot;
        weaponGridRoot ??= weaponRoot;
        relicGridRoot ??= relicRoot;
        playerStatPanel ??= statPanel;
        consumableSlotPrefab ??= consumablePrefab;
        weaponSlotPrefab ??= weaponPrefab;
        relicSlotPrefab ??= relicPrefab;
        dropZone ??= dropZoneView;
    }

    public void SetPlayerStatPanel(PlayerStatPanelView statPanel)
    {
        playerStatPanel = statPanel;
    }

    private RectTransform ResolveCollisionBoundsRect()
    {
        if (collisionBounds != null)
            return collisionBounds;

        return RectTransform;
    }

    public void Bind(
        PlayerConsumableInventory consumableInventory,
        WeaponInventory2D weaponInventory,
        RelicInventory relicInventory,
        Transform dropOrigin,
        Transform playerRoot)
    {
        ClearBinding();

        ConsumableContainer = new PlayerConsumableContainerAdapter(consumableInventory);
        WeaponContainer = new PlayerWeaponContainerAdapter(weaponInventory);
        RelicContainer = new PlayerRelicContainerAdapter(relicInventory);

        consumableDisposer = ConsumableContainer as IDisposable;
        weaponDisposer = WeaponContainer as IDisposable;
        relicDisposer = RelicContainer as IDisposable;

        if (playerStatPanel != null)
            playerStatPanel.Bind(playerRoot);

        if (dropZone != null)
        {
            dropZone.SetDropOrigin(dropOrigin);
            dropZone.Hide();
        }

        BuildSlots(ConsumableContainer, consumableGridRoot, consumableSlotPrefab, consumableSlots);
        BuildSlots(WeaponContainer, weaponGridRoot, weaponSlotPrefab, weaponSlots);
        BuildSlots(RelicContainer, relicGridRoot, relicSlotPrefab, relicSlots, RelicVisibleSlotCount);
        spawnedSlots.Clear();
        spawnedSlots.AddRange(consumableSlots.Slots);
        spawnedSlots.AddRange(weaponSlots.Slots);
        spawnedSlots.AddRange(relicSlots.Slots);
    }

    public void ClearBinding()
    {
        ClearSlots();
        dropZone?.Hide();

        consumableDisposer?.Dispose();
        weaponDisposer?.Dispose();
        relicDisposer?.Dispose();

        consumableDisposer = null;
        weaponDisposer = null;
        relicDisposer = null;

        ConsumableContainer = null;
        WeaponContainer = null;
        RelicContainer = null;
    }

    private void BuildSlots(IItemContainer container, Transform gridRoot, ItemSlotUI slotPrefab, SlotGroup group, int visibleSlotCount = 0)
    {
        if (container == null || gridRoot == null || slotPrefab == null)
        {
            DiscardSlots(group);
            return;
        }

        int slotCount = visibleSlotCount > 0
            ? Mathf.Max(container.SlotCount, visibleSlotCount)
            : container.SlotCount;

        bool reusable = group.Root == gridRoot && group.Prefab == slotPrefab && group.Slots.Count == slotCount;
        for (int i = 0; reusable && i < group.Slots.Count; i++)
            reusable = group.Slots[i] != null && group.Slots[i].transform.parent == gridRoot;
        if (!reusable)
        {
            DiscardSlots(group);
            group.Root = gridRoot;
            group.Prefab = slotPrefab;
            for (int i = 0; i < slotCount; i++)
                group.Slots.Add(Instantiate(slotPrefab, gridRoot));
        }

        for (int i = 0; i < group.Slots.Count; i++)
        {
            ItemSlotUI slot = group.Slots[i];
            slot.transform.SetAsLastSibling();
            slot.gameObject.SetActive(true);
            slot.Bind(container, i);
        }
    }

    private void ClearSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] == null)
                continue;

            spawnedSlots[i].ClearBinding();
            spawnedSlots[i].gameObject.SetActive(false);
        }
    }

    private void DiscardSlots(SlotGroup group)
    {
        for (int i = 0; i < group.Slots.Count; i++)
        {
            ItemSlotUI slot = group.Slots[i];
            if (slot == null)
                continue;
            slot.ClearBinding();
            slot.gameObject.SetActive(false);
            // IncludeInactive queries must not observe deferred-destruction views after a rebind.
            slot.transform.SetParent(null, false);
            if (Application.isPlaying)
                Destroy(slot.gameObject);
            else
                DestroyImmediate(slot.gameObject);
        }
        group.Slots.Clear();
        group.Root = null;
        group.Prefab = null;
    }

}

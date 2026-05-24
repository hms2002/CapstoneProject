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

    private IDisposable consumableDisposer;
    private IDisposable weaponDisposer;
    private IDisposable relicDisposer;

    public IItemContainer ConsumableContainer { get; private set; }
    public IItemContainer WeaponContainer { get; private set; }
    public IItemContainer RelicContainer { get; private set; }
    public RectTransform RectTransform => panelRoot != null ? panelRoot : transform as RectTransform;
    public RectTransform CollisionBoundsRect => ResolveCollisionBoundsRect();
    public PlayerStatPanelView PlayerStatPanel => playerStatPanel;
    public RectTransform PlayerStatPanelRect => playerStatPanel != null
        ? playerStatPanel.transform as RectTransform
        : null;

    private void OnDisable()
    {
        ClearBinding();
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

        BuildSlots(ConsumableContainer, consumableGridRoot, consumableSlotPrefab);
        BuildSlots(WeaponContainer, weaponGridRoot, weaponSlotPrefab);
        BuildSlots(RelicContainer, relicGridRoot, relicSlotPrefab, RelicVisibleSlotCount);
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

    private void BuildSlots(IItemContainer container, Transform gridRoot, ItemSlotUI slotPrefab, int visibleSlotCount = 0)
    {
        if (container == null || gridRoot == null || slotPrefab == null)
            return;

        int slotCount = visibleSlotCount > 0
            ? Mathf.Max(container.SlotCount, visibleSlotCount)
            : container.SlotCount;

        for (int i = 0; i < slotCount; i++)
        {
            ItemSlotUI slot = Instantiate(slotPrefab, gridRoot);
            slot.Bind(container, i);
            spawnedSlots.Add(slot);
        }
    }

    private void ClearSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] == null)
                continue;

            if (Application.isPlaying)
                Destroy(spawnedSlots[i].gameObject);
            else
                DestroyImmediate(spawnedSlots[i].gameObject);
        }

        spawnedSlots.Clear();
    }

}

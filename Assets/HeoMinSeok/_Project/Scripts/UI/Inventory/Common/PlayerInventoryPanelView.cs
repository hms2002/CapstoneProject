using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInventoryPanelView : MonoBehaviour
{
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
        BuildSlots(RelicContainer, relicGridRoot, relicSlotPrefab);
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

    private void BuildSlots(IItemContainer container, Transform gridRoot, ItemSlotUI slotPrefab)
    {
        if (container == null || gridRoot == null || slotPrefab == null)
            return;

        for (int i = 0; i < container.SlotCount; i++)
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

    private sealed class PlayerConsumableContainerAdapter : IItemContainer, IDisposable
    {
        private readonly PlayerConsumableInventory inventory;
        public event Action OnChanged;

        public PlayerConsumableContainerAdapter(PlayerConsumableInventory inventory)
        {
            this.inventory = inventory;
            if (this.inventory != null)
                this.inventory.OnChanged += HandleChanged;
        }

        public int SlotCount => inventory != null ? inventory.SlotCount : 0;

        public ScriptableObject Get(int index)
        {
            return inventory != null ? inventory.GetConsumableInSlot(index) : null;
        }

        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
        {
            if (inventory == null)
                return false;
            if (item == null)
                return true;

            return item is ConsumableDefinition consumable
                && inventory.CanPlaceConsumableInSlot(index, consumable);
        }

        public bool TrySet(int index, ScriptableObject item)
        {
            if (inventory == null)
                return false;
            if (item == null)
                return inventory.TrySetConsumableSlot(index, null);

            return item is ConsumableDefinition consumable
                && inventory.TrySetConsumableSlot(index, consumable);
        }

        public bool TrySwap(int a, int b)
        {
            return inventory != null && inventory.TrySwapConsumableSlots(a, b);
        }

        public void Dispose()
        {
            if (inventory != null)
                inventory.OnChanged -= HandleChanged;
        }

        private void HandleChanged()
        {
            OnChanged?.Invoke();
        }
    }

    private sealed class PlayerWeaponContainerAdapter : IItemContainer, IDisposable
    {
        private readonly WeaponInventory2D inventory;
        public event Action OnChanged;

        public PlayerWeaponContainerAdapter(WeaponInventory2D inventory)
        {
            this.inventory = inventory;
            if (this.inventory != null)
                this.inventory.OnInventoryChanged += HandleChanged;
        }

        public int SlotCount => inventory != null ? inventory.SlotCount : 0;

        public ScriptableObject Get(int index)
        {
            return inventory != null ? inventory.GetWeaponInSlot(index) : null;
        }

        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
        {
            if (inventory == null)
                return false;
            if (item == null)
                return true;

            return item is WeaponDefinition weapon
                && inventory.CanPlaceWeaponInSlot(index, weapon);
        }

        public bool TrySet(int index, ScriptableObject item)
        {
            if (inventory == null)
                return false;
            if (item == null)
                return inventory.TrySetWeaponSlot(index, null);

            return item is WeaponDefinition weapon
                && inventory.TrySetWeaponSlot(index, weapon);
        }

        public bool TrySwap(int a, int b)
        {
            return inventory != null && inventory.TrySwapWeaponSlots(a, b);
        }

        public void Dispose()
        {
            if (inventory != null)
                inventory.OnInventoryChanged -= HandleChanged;
        }

        private void HandleChanged()
        {
            OnChanged?.Invoke();
        }
    }

    private sealed class PlayerRelicContainerAdapter : IItemContainer, IDisposable, IRelicLevelProvider, IRelicSlotReceiver
    {
        private readonly RelicInventory inventory;
        public event Action OnChanged;

        public PlayerRelicContainerAdapter(RelicInventory inventory)
        {
            this.inventory = inventory;
            if (this.inventory != null)
                this.inventory.OnChanged += HandleChanged;
        }

        public int SlotCount => inventory != null ? inventory.Capacity : 0;

        public ScriptableObject Get(int index)
        {
            return inventory != null ? inventory.GetRelicInSlot(index) : null;
        }

        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
        {
            if (inventory == null)
                return false;
            if (item == null)
                return true;

            return item is RelicDefinition relic
                && inventory.CanPlaceRelicInSlot(index, relic, ignoreIndex);
        }

        public bool TrySet(int index, ScriptableObject item)
        {
            if (inventory == null)
                return false;
            if (item == null)
                return inventory.TrySetRelicSlot(index, null);
            if (!(item is RelicDefinition relic))
                return false;

            bool ok = inventory.TrySetRelicSlot(index, relic);
            if (!ok)
                ShowRelicWarning(ResolveRelicFailure(relic, relic.dropLevel > 0 ? relic.dropLevel : 1));
            return ok;
        }

        public bool TrySwap(int a, int b)
        {
            return inventory != null && inventory.TrySwapRelicSlots(a, b);
        }

        public bool TryGetRelicLevel(int index, out int level)
        {
            level = inventory != null ? inventory.GetRelicLevelInSlot(index) : 0;
            return level > 0;
        }

        public bool TrySetRelicWithLevel(int index, RelicDefinition relic, int level)
        {
            if (inventory == null)
                return false;
            if (relic == null)
                return inventory.TrySetRelicSlot(index, null);

            bool ok = inventory.TrySetRelicSlotWithLevel(index, relic, level);
            if (!ok)
                ShowRelicWarning(ResolveRelicFailure(relic, level));
            return ok;
        }

        public void Dispose()
        {
            if (inventory != null)
                inventory.OnChanged -= HandleChanged;
        }

        private void HandleChanged()
        {
            OnChanged?.Invoke();
        }

        private static void ShowRelicWarning(RelicInventory.AcquireResult result)
        {
            if (UIManager.Instance == null)
                return;

            WarningPopupCode code = result switch
            {
                RelicInventory.AcquireResult.InventoryFull => WarningPopupCode.RelicInventoryFull,
                RelicInventory.AcquireResult.AlreadyMaxLevel => WarningPopupCode.RelicAlreadyMaxLevel,
                _ => WarningPopupCode.None
            };

            if (code != WarningPopupCode.None)
                UIManager.Instance.ShowWarning(code);
        }

        private RelicInventory.AcquireResult ResolveRelicFailure(RelicDefinition relic, int incomingLevel)
        {
            if (inventory == null || relic == null)
                return RelicInventory.AcquireResult.InvalidDefinition;

            if (!inventory.TryGetRelicLevelById(relic.relicId, out int currentLevel))
                return RelicInventory.AcquireResult.InvalidDefinition;

            int gain = Mathf.Max(1, incomingLevel);
            int nextLevel = relic.ClampLevel(currentLevel + gain);
            return nextLevel == currentLevel
                ? RelicInventory.AcquireResult.AlreadyMaxLevel
                : RelicInventory.AcquireResult.InvalidDefinition;
        }
    }
}

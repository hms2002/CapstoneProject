using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Standalone inventory screen:
/// - Backpack (generic storage)
/// - Weapon equip (2)
/// - Relic equip (18)
/// - Nearby loot (fixed slots)
/// - Drop zone to discard items to the world
/// </summary>
// [핵심] IStackableUI를 상속받아 UIManager의 통제를 받습니다!
public class InventoryScreen : MonoBehaviour, IStackableUI
{
    [Header("UI Refs")]
    [SerializeField] private Transform backpackGridRoot;
    [SerializeField] private Transform weaponGridRoot;
    [SerializeField] private Transform relicGridRoot;
    [SerializeField] private Transform lootGridRoot;
    [SerializeField] private ItemSlotUI slotPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private DropZoneUI dropZone;

    [Header("Loot")]
    [SerializeField] private float lootRadius = 2.2f;
    [SerializeField] private int lootSlotCount = 8;
    [SerializeField] private float lootRefreshInterval = 0.25f;

    private IItemContainer backpackContainer;
    private IItemContainer weaponContainer;
    private IItemContainer relicContainer;
    private WorldLootContainerAdapter lootContainer;

    private readonly List<ItemSlotUI> spawned = new();

    private IDisposable backpackDisposer;
    private IDisposable weaponDisposer;
    private IDisposable relicDisposer;

    private Transform lootOrigin;
    private float lootRefreshTimer;

    // =========================================================
    // IStackableUI 규약
    // =========================================================
    public bool IsActive => gameObject.activeSelf;
    public bool CanCloseOnEscape => true;

    public void OpenUI()
    {
        gameObject.SetActive(true);
    }

    public void CloseUI()
    {
        gameObject.SetActive(false);
        // 창이 닫힐 때 허공에 남은 툴팁 즉시 해제
        if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();
    }
    // =========================================================

    private void Awake()
    {
        if (closeButton != null)
        {
            // [수정] 직접 끄지 않고 UIManager에게 닫아달라고(Pop) 요청
            closeButton.onClick.AddListener(() => {
                if (UIManager.Instance != null) UIManager.Instance.PopUI(this);
                else CloseUI();
            });
        }
    }

    private void OnDisable()
    {
        ClearUI();
        ItemContainerGroupRegistry.Clear();

        backpackDisposer?.Dispose();
        weaponDisposer?.Dispose();
        relicDisposer?.Dispose();

        backpackDisposer = null;
        weaponDisposer = null;
        relicDisposer = null;

        lootContainer = null;
        lootOrigin = null;
        lootRefreshTimer = 0f;

        if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();
    }

    public void Bind(PlayerBackpackInventory backpack, WeaponInventory2D weaponInv, RelicInventory relicInv, Transform lootOrigin)
    {
        this.lootOrigin = lootOrigin;

        backpackContainer = new BackpackContainerAdapter(backpack);
        weaponContainer = new PlayerWeaponContainerAdapter(weaponInv);
        relicContainer = new PlayerRelicContainerAdapter(relicInv);

        ItemContainerGroupRegistry.SetGroup(backpackContainer, weaponContainer, relicContainer);

        lootContainer = new WorldLootContainerAdapter(this.lootOrigin, lootRadius, lootSlotCount);

        if (dropZone != null)
            dropZone.SetDropOrigin(this.lootOrigin);

        BuildUI();
        RefreshLootNow();
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;
        if (lootContainer == null) return;

        lootRefreshTimer += Time.deltaTime;
        if (lootRefreshTimer >= lootRefreshInterval)
        {
            lootRefreshTimer = 0f;
            RefreshLootNow();
        }
    }

    private void RefreshLootNow()
    {
        lootContainer?.RefreshFromWorld();
    }

    private void BuildUI()
    {
        ClearUI();
        if (slotPrefab == null) return;

        if (backpackContainer != null && backpackGridRoot != null)
        {
            for (int i = 0; i < backpackContainer.SlotCount; i++)
            {
                var ui = Instantiate(slotPrefab, backpackGridRoot);
                ui.Bind(backpackContainer, i);
                spawned.Add(ui);
            }
        }

        if (weaponContainer != null && weaponGridRoot != null)
        {
            for (int i = 0; i < weaponContainer.SlotCount; i++)
            {
                var ui = Instantiate(slotPrefab, weaponGridRoot);
                ui.Bind(weaponContainer, i);
                spawned.Add(ui);
            }
        }

        if (relicContainer != null && relicGridRoot != null)
        {
            for (int i = 0; i < relicContainer.SlotCount; i++)
            {
                var ui = Instantiate(slotPrefab, relicGridRoot);
                ui.Bind(relicContainer, i);
                spawned.Add(ui);
            }
        }

        if (lootContainer != null && lootGridRoot != null)
        {
            for (int i = 0; i < lootContainer.SlotCount; i++)
            {
                var ui = Instantiate(slotPrefab, lootGridRoot);
                ui.Bind(lootContainer, i);
                spawned.Add(ui);
            }
        }
    }

    private void ClearUI()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        spawned.Clear();
    }

    // -----------------------
    // Adapters (public logic copied from ChestScreen)
    // -----------------------
    private class BackpackContainerAdapter : IItemContainer, IDisposable, IRelicLevelProvider, IRelicSlotReceiver
    {
        private readonly PlayerBackpackInventory inv;
        public event Action OnChanged;

        public BackpackContainerAdapter(PlayerBackpackInventory inv)
        {
            this.inv = inv;
            if (this.inv != null) this.inv.OnChanged += HandleChanged;
        }

        public int SlotCount => inv != null && inv.Inventory != null ? inv.Inventory.Capacity : 0;

        public ScriptableObject Get(int index) => inv != null ? inv.Get(index) : null;

        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
        {
            // Backpack accepts any inventory item.
            return true;
        }

        public bool TrySet(int index, ScriptableObject item) => inv != null && inv.Set(index, item);

        public bool TrySwap(int a, int b) => inv != null && inv.Swap(a, b);

        private void HandleChanged() => OnChanged?.Invoke();

        public void Dispose()
        {
            if (inv != null) inv.OnChanged -= HandleChanged;
        }
        public bool TryGetRelicLevel(int index, out int level)
        {
            level = inv != null ? inv.GetRelicLevelInSlot(index) : 0;
            return level > 0;
        }

        public bool TrySetRelicWithLevel(int index, RelicDefinition relic, int level)
        {
            return inv != null && inv.SetRelicWithLevel(index, relic, level);
        }

    }

    private class PlayerWeaponContainerAdapter : IItemContainer, IDisposable
    {
        private readonly WeaponInventory2D inv;
        public event Action OnChanged;

        public PlayerWeaponContainerAdapter(WeaponInventory2D inv)
        {
            this.inv = inv;
            if (this.inv != null) this.inv.OnInventoryChanged += HandleChanged;
        }

        public int SlotCount => inv != null ? inv.SlotCount : 0;

        public ScriptableObject Get(int index) => inv != null ? inv.GetWeaponInSlot(index) : null;

        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
        {
            if (inv == null) return false;
            if (item == null) return true;

            var w = item as WeaponDefinition;
            if (w == null) return false;

            return inv.CanPlaceWeaponInSlot(index, w);
        }

        public bool TrySet(int index, ScriptableObject item)
        {
            if (inv == null) return false;
            if (item == null) return inv.TrySetWeaponSlot(index, null);

            var w = item as WeaponDefinition;
            if (w == null) return false;

            return inv.TrySetWeaponSlot(index, w);
        }

        public bool TrySwap(int a, int b) => inv != null && inv.TrySwapWeaponSlots(a, b);

        private void HandleChanged() => OnChanged?.Invoke();

        public void Dispose()
        {
            if (inv != null) inv.OnInventoryChanged -= HandleChanged;
        }
    }

    private class PlayerRelicContainerAdapter : IItemContainer, IDisposable, IRelicLevelProvider, IRelicSlotReceiver
    {
        private readonly RelicInventory inv;
        public event Action OnChanged;

        public PlayerRelicContainerAdapter(RelicInventory inv)
        {
            this.inv = inv;
            if (this.inv != null) this.inv.OnChanged += HandleChanged;
        }

        public int SlotCount => inv != null ? inv.Capacity : 0;

        public ScriptableObject Get(int index) => inv != null ? inv.GetRelicInSlot(index) : null;

        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
        {
            if (inv == null) return false;
            if (item == null) return true;

            var r = item as RelicDefinition;
            if (r == null) return false;

            return inv.CanPlaceRelicInSlot(index, r, ignoreIndex);
        }

        public bool TrySet(int index, ScriptableObject item)
        {
            if (inv == null) return false;
            if (item == null) return inv.TrySetRelicSlot(index, null);

            var r = item as RelicDefinition;
            if (r == null) return false;

            return inv.TrySetRelicSlot(index, r);
        }

        public bool TrySwap(int a, int b) => inv != null && inv.TrySwapRelicSlots(a, b);

        private void HandleChanged() => OnChanged?.Invoke();

        public void Dispose()
        {
            if (inv != null) inv.OnChanged -= HandleChanged;
        }
        public bool TryGetRelicLevel(int index, out int level)
        {
            level = inv != null ? inv.GetRelicLevelInSlot(index) : 0;
            return level > 0;
        }

        public bool TrySetRelicWithLevel(int index, RelicDefinition relic, int level)
        {
            if (inv == null) return false;
            if (relic == null) return inv.TrySetRelicSlot(index, null);
            return inv.TrySetRelicSlotWithLevel(index, relic, level);
        }
    }
}

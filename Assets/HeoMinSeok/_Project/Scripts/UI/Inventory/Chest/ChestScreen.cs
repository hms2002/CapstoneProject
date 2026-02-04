using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChestScreen : MonoBehaviour
{
    [Header("UI Refs")]
    [Tooltip("플레이어 인벤토리 영역(무기/유물)이 포함된 패널 RectTransform")]
    [SerializeField] private RectTransform inventoryPanelRect;
    [Tooltip("상자 영역(상자 슬롯)이 포함된 패널 RectTransform")]
    [SerializeField] private RectTransform chestPanelRect;
    [SerializeField] private Transform chestGridRoot;
    [SerializeField] private Transform weaponGridRoot;
    [SerializeField] private Transform relicGridRoot;
    [SerializeField] private ItemSlotUI slotPrefab;
    [SerializeField] private Button closeButton;

    [Header("Runtime Refs")]
    [SerializeField] private WeaponInventory2D playerWeaponInventory;
    [SerializeField] private RelicInventory playerRelicInventory;

    private ChestInventory chestInventory;

    private IItemContainer chestContainer;
    private IItemContainer weaponContainer;
    private IItemContainer relicContainer;

    private readonly List<ItemSlotUI> spawned = new();

    private IDisposable chestAdapterDisposer;
    private IDisposable weaponAdapterDisposer;
    private IDisposable relicAdapterDisposer;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(() => ChestUIManager.Instance.CloseChest());

        if (playerWeaponInventory == null)
            playerWeaponInventory = FindFirstObjectByType<WeaponInventory2D>();

        if (playerRelicInventory == null)
            playerRelicInventory = FindFirstObjectByType<RelicInventory>();
    }

    private void OnDisable()
    {
        // Chest UI가 닫히면 Hover/Detail 상태도 정리
        UIHoverManager.Instance?.HideImmediate();
        ItemDetailPanel.Instance?.Hide();

        ClearUI();
        ItemContainerGroupRegistry.Clear();

        chestAdapterDisposer?.Dispose();
        weaponAdapterDisposer?.Dispose();
        relicAdapterDisposer?.Dispose();

        chestAdapterDisposer = null;
        weaponAdapterDisposer = null;
        relicAdapterDisposer = null;
    }

    public void Bind(ChestInventory inv)
    {
        chestInventory = inv;

        chestContainer = new ChestContainerAdapter(chestInventory);
        weaponContainer = new PlayerWeaponContainerAdapter(playerWeaponInventory);
        relicContainer = new PlayerRelicContainerAdapter(playerRelicInventory);
        // Right-click quick move를 위해 그룹 등록
        ItemContainerGroupRegistry.SetGroup(chestContainer, weaponContainer, relicContainer);

        BuildUI();

        // ✅ HoverManager에 "현재 활성 UI 패널" 등록
        // - inventoryPanelRect / chestPanelRect를 인스펙터에서 지정하는 것을 권장
        // - 미지정 시에는 슬롯이 생성되는 gridRoot를 fallback으로 사용
        var invRect = inventoryPanelRect != null
            ? inventoryPanelRect
            : (weaponGridRoot != null ? weaponGridRoot as RectTransform : null);

        var chestRect = chestPanelRect != null
            ? chestPanelRect
            : (chestGridRoot != null ? chestGridRoot as RectTransform : null);

        UIHoverManager.Instance?.HideImmediate();
        ItemDetailPanel.Instance?.Hide();
    }

    private void BuildUI()
    {
        ClearUI();

        // Chest slots (16)
        for (int i = 0; i < chestContainer.SlotCount; i++)
        {
            var ui = Instantiate(slotPrefab, chestGridRoot);
            ui.Bind(chestContainer, i);
            spawned.Add(ui);
        }

        // Player weapon slots (2)
        for (int i = 0; i < weaponContainer.SlotCount; i++)
        {
            var ui = Instantiate(slotPrefab, weaponGridRoot);
            ui.Bind(weaponContainer, i);
            spawned.Add(ui);
        }

        // Player relic slots (18)
        for (int i = 0; i < relicContainer.SlotCount; i++)
        {
            var ui = Instantiate(slotPrefab, relicGridRoot);
            ui.Bind(relicContainer, i);
            spawned.Add(ui);
        }
    }

    private void ClearUI()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null) Destroy(spawned[i].gameObject);
        }
        spawned.Clear();
    }

    // -----------------------
    // Adapters
    // -----------------------
    private class ChestContainerAdapter : IItemContainer, IDisposable, IRelicLevelProvider, IRelicSlotReceiver
    {
        private readonly ChestInventory inv;
        public event Action OnChanged;

        public ChestContainerAdapter(ChestInventory inv)
        {
            this.inv = inv;
            if (this.inv != null) this.inv.OnChanged += HandleChanged;
        }

        public int SlotCount => inv != null ? inv.Capacity : 0;

        public ScriptableObject Get(int index) => inv != null ? inv.Get(index) : null;

        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1) => true; // 상자는 어떤 아이템이든 OK

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

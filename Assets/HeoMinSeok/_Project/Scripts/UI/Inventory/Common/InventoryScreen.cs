using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Standalone inventory screen:
/// - Weapon equip (2)
/// - Relic equip (18)
/// - Drop zone to discard items to the world
/// </summary>
// [핵심] IStackableUI를 상속받아 UIManager의 통제를 받습니다!
public class InventoryScreen : MonoBehaviour, IStackableUI
{
    [Header("UI Refs")]
    [SerializeField] private Transform weaponGridRoot;
    [SerializeField] private Transform relicGridRoot;
    [SerializeField] private ItemSlotUI slotPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private DropZoneUI dropZone;

    private IItemContainer weaponContainer;
    private IItemContainer relicContainer;

    private readonly List<ItemSlotUI> spawned = new();

    private IDisposable weaponDisposer;
    private IDisposable relicDisposer;

    private Transform lootOrigin;

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

        weaponDisposer?.Dispose();
        relicDisposer?.Dispose();

        weaponDisposer = null;
        relicDisposer = null;

        lootOrigin = null;

        if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();
    }

    public void Bind(WeaponInventory2D weaponInv, RelicInventory relicInv, Transform lootOrigin)
    {
        this.lootOrigin = lootOrigin;

        weaponContainer = new PlayerWeaponContainerAdapter(weaponInv);
        relicContainer = new PlayerRelicContainerAdapter(relicInv);

        ItemContainerGroupRegistry.SetGroup(null, weaponContainer, relicContainer);

        if (dropZone != null)
            dropZone.SetDropOrigin(this.lootOrigin);

        BuildUI();
    }

    private void BuildUI()
    {
        ClearUI();
        if (slotPrefab == null) return;

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

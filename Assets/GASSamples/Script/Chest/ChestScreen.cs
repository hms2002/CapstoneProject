using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChestScreen : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private Transform chestGridRoot;
    [SerializeField] private Transform playerGridRoot;
    [SerializeField] private ItemSlotUI slotPrefab;
    [SerializeField] private Button closeButton;

    [Header("Runtime Refs")]
    [SerializeField] private WeaponInventory2D playerWeaponInventory;

    private ChestInventory chestInventory;

    private IWeaponContainer chestContainer;
    private IWeaponContainer playerContainer;

    private readonly List<ItemSlotUI> spawned = new();

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(() => ChestUIManager.Instance.CloseChest());

        if (playerWeaponInventory == null)
            playerWeaponInventory = FindFirstObjectByType<WeaponInventory2D>();
    }

    public void Bind(ChestInventory inv)
    {
        chestInventory = inv;

        chestContainer = new ChestContainerAdapter(chestInventory);
        playerContainer = new PlayerWeaponContainerAdapter(playerWeaponInventory);

        BuildUI();
    }

    private void OnDisable()
    {
        ClearUI();
        (chestContainer as IDisposable)?.Dispose();
        (playerContainer as IDisposable)?.Dispose();

        chestContainer = null;
        playerContainer = null;
        chestInventory = null;
    }

    private void BuildUI()
    {
        ClearUI();

        if (slotPrefab == null || chestGridRoot == null || playerGridRoot == null)
        {
            Debug.LogError("[ChestScreen] UI references are missing.");
            return;
        }

        // Chest slots
        for (int i = 0; i < chestContainer.SlotCount; i++)
        {
            var ui = Instantiate(slotPrefab, chestGridRoot);
            ui.Bind(chestContainer, i);
            spawned.Add(ui);
        }

        // Player weapon slots (2)
        for (int i = 0; i < playerContainer.SlotCount; i++)
        {
            var ui = Instantiate(slotPrefab, playerGridRoot);
            ui.Bind(playerContainer, i);
            spawned.Add(ui);
        }
    }

    private void ClearUI()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i].gameObject);
        }
        spawned.Clear();
    }

    // -----------------------
    // Container adapters
    // -----------------------
    private class ChestContainerAdapter : IWeaponContainer, IDisposable
    {
        private readonly ChestInventory inv;
        public event Action OnChanged;

        public ChestContainerAdapter(ChestInventory inv)
        {
            this.inv = inv;
            if (this.inv != null) this.inv.OnChanged += HandleChanged;
        }

        public int SlotCount => inv != null ? inv.Capacity : 0;

        public WeaponDefinition Get(int index) => inv != null ? inv.Get(index) : null;

        public bool CanPlace(WeaponDefinition weapon, int index, int ignoreIndex = -1) => true;

        public bool TrySet(int index, WeaponDefinition weapon) => inv != null && inv.Set(index, weapon);

        public bool TrySwap(int a, int b) => inv != null && inv.Swap(a, b);

        private void HandleChanged() => OnChanged?.Invoke();

        public void Dispose()
        {
            if (inv != null) inv.OnChanged -= HandleChanged;
        }
    }

    private class PlayerWeaponContainerAdapter : IWeaponContainer, IDisposable
    {
        private readonly WeaponInventory2D inv;
        public event Action OnChanged;

        public PlayerWeaponContainerAdapter(WeaponInventory2D inv)
        {
            this.inv = inv;
            if (this.inv != null) this.inv.OnInventoryChanged += HandleChanged;
        }

        public int SlotCount => inv != null ? inv.SlotCount : 0;

        public WeaponDefinition Get(int index) => inv != null ? inv.GetWeaponInSlot(index) : null;

        public bool CanPlace(WeaponDefinition weapon, int index, int ignoreIndex = -1)
        {
            if (inv == null) return false;
            return inv.CanPlaceWeaponInSlot(index, weapon);
        }

        public bool TrySet(int index, WeaponDefinition weapon)
        {
            if (inv == null) return false;
            return inv.TrySetWeaponSlot(index, weapon);
        }

        public bool TrySwap(int a, int b)
        {
            if (inv == null) return false;
            return inv.TrySwapWeaponSlots(a, b);
        }

        private void HandleChanged() => OnChanged?.Invoke();

        public void Dispose()
        {
            if (inv != null) inv.OnInventoryChanged -= HandleChanged;
        }
    }
}

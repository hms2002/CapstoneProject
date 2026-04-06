using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// [핵심] IStackableUI를 상속받아 UIManager의 통제를 받습니다!
public class ChestScreen : MonoBehaviour, IStackableUI
{
    [Header("UI Refs")]
    [Tooltip("플레이어 인벤토리 영역(무기/유물)이 포함된 패널 RectTransform")]
    [SerializeField] private RectTransform inventoryPanelRect;
    [Tooltip("상자 영역(상자 슬롯)이 포함된 패널 RectTransform")]
    [SerializeField] private RectTransform chestPanelRect;
    [SerializeField] private Transform chestGridRoot;
    [SerializeField] private Transform consumableGridRoot;
    [SerializeField] private Transform weaponGridRoot;
    [SerializeField] private Transform relicGridRoot;
    [SerializeField] private PlayerStatPanelView playerStatPanel;
    [SerializeField] private ItemSlotUI chestSlotPrefab;
    [SerializeField] private ItemSlotUI consumableSlotPrefab;
    [SerializeField] private ItemSlotUI weaponSlotPrefab;
    [SerializeField] private ItemSlotUI relicSlotPrefab;
    [SerializeField] private Button closeButton;
    [SerializeField] private DropZoneUI dropZone;

    [Header("Runtime Refs")]
    [SerializeField] private PlayerConsumableInventory playerConsumableInventory;
    [SerializeField] private WeaponInventory2D playerWeaponInventory;
    [SerializeField] private RelicInventory playerRelicInventory;

    private ChestInventory chestInventory;
    private Transform dropOrigin;

    private IItemContainer chestContainer;
    private IItemContainer consumableContainer;
    private IItemContainer weaponContainer;
    private IItemContainer relicContainer;

    private readonly List<ItemSlotUI> spawned = new();

    private IDisposable chestAdapterDisposer;
    private IDisposable consumableAdapterDisposer;
    private IDisposable weaponAdapterDisposer;
    private IDisposable relicAdapterDisposer;

    // =========================================================
    // IStackableUI 규약
    // =========================================================
    public bool IsActive => gameObject.activeSelf;
    public bool CanCloseOnEscape => true;
    public UIOpenGroup OpenGroup => UIOpenGroup.ExclusiveModal;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.ExclusiveModal;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;

    public void OpenUI()
    {
        gameObject.SetActive(true);
    }

    public void CloseUI()
    {
        gameObject.SetActive(false);

        // 창이 닫힐 때 허공에 뜬 Hover UI(툴팁) 강제 제거
        if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();

        // [핵심] UIManager에 의해 창이 닫히면 매니저에게 알려서 시간과 플레이어 상태를 복구합니다.
        if (ChestUIManager.Instance != null)
        {
            ChestUIManager.Instance.HandleChestClosed();
        }
    }
    // =========================================================

    private void Awake()
    {
        if (closeButton != null)
        {
            // 직접 끄지 않고 사령탑(UIManager)에게 닫아달라고(Pop) 요청
            closeButton.onClick.AddListener(() => {
                if (UIManager.Instance != null) UIManager.Instance.PopUI(this);
                else CloseUI();
            });
        }

    }

    private void OnDisable()
    {
        if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();

        ClearUI();
        ItemContainerGroupRegistry.Clear();
        dropZone?.Hide();

        chestAdapterDisposer?.Dispose();
        consumableAdapterDisposer?.Dispose();
        weaponAdapterDisposer?.Dispose();
        relicAdapterDisposer?.Dispose();

        chestAdapterDisposer = null;
        consumableAdapterDisposer = null;
        weaponAdapterDisposer = null;
        relicAdapterDisposer = null;
        dropOrigin = null;
    }

    public void Bind(ChestInventory inv)
    {
        chestInventory = inv;

        ResolvePlayerInventories();

        chestContainer = new ChestContainerAdapter(chestInventory);
        consumableContainer = new PlayerConsumableContainerAdapter(playerConsumableInventory);
        weaponContainer = new PlayerWeaponContainerAdapter(playerWeaponInventory);
        relicContainer = new PlayerRelicContainerAdapter(playerRelicInventory);

        if (playerStatPanel != null)
        {
            var currentPlayer = PlayerRuntimeRegistry.CurrentPlayer != null
                ? PlayerRuntimeRegistry.CurrentPlayer
                : PlayerInteractor2D.Instance;
            playerStatPanel.Bind(currentPlayer != null ? currentPlayer.transform : null);
        }

        var currentTransform = PlayerRuntimeRegistry.GetPlayerTransform();
        dropOrigin = currentTransform != null
            ? currentTransform
            : (PlayerInteractor2D.Instance != null ? PlayerInteractor2D.Instance.transform : null);

        ItemContainerGroupRegistry.SetGroup(chestContainer, consumableContainer, weaponContainer, relicContainer);

        if (dropZone != null)
        {
            dropZone.SetDropOrigin(dropOrigin);
            dropZone.Hide();
        }

        BuildUI();

        if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();
    }


    private void ResolvePlayerInventories()
    {
        var currentPlayer = PlayerRuntimeRegistry.CurrentPlayer != null
            ? PlayerRuntimeRegistry.CurrentPlayer
            : PlayerInteractor2D.Instance;

        if (currentPlayer != null)
        {
            playerWeaponInventory = currentPlayer.GetComponent<WeaponInventory2D>();
            playerRelicInventory = currentPlayer.GetComponent<RelicInventory>();
            playerConsumableInventory = currentPlayer.GetComponent<PlayerConsumableInventory>();
        }

        if (playerConsumableInventory == null)
            playerConsumableInventory = FindFirstObjectByType<PlayerConsumableInventory>();

        if (playerWeaponInventory == null)
            playerWeaponInventory = FindFirstObjectByType<WeaponInventory2D>();

        if (playerRelicInventory == null)
            playerRelicInventory = FindFirstObjectByType<RelicInventory>();
    }

    private void BuildUI()
    {
        ClearUI();
        BuildSlots(chestContainer, chestGridRoot, chestSlotPrefab);
        BuildSlots(consumableContainer, consumableGridRoot, consumableSlotPrefab);
        BuildSlots(weaponContainer, weaponGridRoot, weaponSlotPrefab);
        BuildSlots(relicContainer, relicGridRoot, relicSlotPrefab);
    }

    /// <summary>
    /// 책임 : 지정된 컨테이너를 대응하는 슬롯 프리팹으로 상자 UI에 렌더링한다.
    /// </summary>
    private void BuildSlots(IItemContainer container, Transform gridRoot, ItemSlotUI slotPrefab)
    {
        if (container == null || gridRoot == null || slotPrefab == null)
            return;

        for (int i = 0; i < container.SlotCount; i++)
        {
            var ui = Instantiate(slotPrefab, gridRoot);
            ui.Bind(container, i);
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

    /// <summary>
    /// 책임 :
    /// - 상자 UI에서 플레이어의 1회용 아이템 슬롯을 IItemContainer 규약으로 노출한다.
    /// - 상자 슬롯과의 drag, drop, quick move가 consumable 인벤토리와 연결되도록 한다.
    /// </summary>
    private class PlayerConsumableContainerAdapter : IItemContainer, IDisposable
    {
        private readonly PlayerConsumableInventory inv;
        public event Action OnChanged;

        public PlayerConsumableContainerAdapter(PlayerConsumableInventory inv)
        {
            this.inv = inv;
            if (this.inv != null) this.inv.OnChanged += HandleChanged;
        }

        public int SlotCount => inv != null ? inv.SlotCount : 0;

        public ScriptableObject Get(int index) => inv != null ? inv.GetConsumableInSlot(index) : null;

        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
        {
            if (inv == null) return false;
            if (item == null) return true;

            var consumable = item as ConsumableDefinition;
            if (consumable == null) return false;

            return inv.CanPlaceConsumableInSlot(index, consumable);
        }

        public bool TrySet(int index, ScriptableObject item)
        {
            if (inv == null) return false;
            if (item == null) return inv.TrySetConsumableSlot(index, null);

            var consumable = item as ConsumableDefinition;
            if (consumable == null) return false;

            return inv.TrySetConsumableSlot(index, consumable);
        }

        public bool TrySwap(int a, int b) => inv != null && inv.TrySwapConsumableSlots(a, b);

        private void HandleChanged() => OnChanged?.Invoke();

        public void Dispose()
        {
            if (inv != null) inv.OnChanged -= HandleChanged;
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

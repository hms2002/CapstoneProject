using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ChestScreen : MonoBehaviour, IStackableUI, IMouseCursorDomainSource
{
    [Header("Layout Refs")]
    [SerializeField] private RectTransform inventoryPanelRect;
    [SerializeField] private RectTransform chestPanelRect;

    [Header("Chest Inventory")]
    [SerializeField] private Transform chestGridRoot;
    [SerializeField] private ItemSlotUI chestSlotPrefab;

    [Header("Player Inventory")]
    [SerializeField] private PlayerInventoryPanelView playerInventoryPanel;

    [Header("UI Refs")]
    [SerializeField] private Button closeButton;

    [Header("Presentation")]
    [SerializeField] private UISlideFadePresentation slideFadePresentation;
    [SerializeField] private ChestFirstOpenRevealPresentation firstOpenRevealPresentation;

    [Header("Runtime Refs")]
    [SerializeField] private PlayerConsumableInventory playerConsumableInventory;
    [SerializeField] private WeaponInventory2D playerWeaponInventory;
    [SerializeField] private RelicInventory playerRelicInventory;

    [SerializeField, HideInInspector] private Transform consumableGridRoot;
    [SerializeField, HideInInspector] private Transform weaponGridRoot;
    [SerializeField, HideInInspector] private Transform relicGridRoot;
    [FormerlySerializedAs("playerStatPanel")]
    [SerializeField, HideInInspector] private PlayerStatPanelView legacyPlayerStatPanel;
    [SerializeField, HideInInspector] private ItemSlotUI consumableSlotPrefab;
    [SerializeField, HideInInspector] private ItemSlotUI weaponSlotPrefab;
    [SerializeField, HideInInspector] private ItemSlotUI relicSlotPrefab;
    [SerializeField, HideInInspector] private DropZoneUI dropZone;

    private readonly List<ItemSlotUI> spawnedChestSlots = new();

    private ChestInventory chestInventory;
    private IItemContainer chestContainer;
    private IDisposable chestAdapterDisposer;
    private bool playSlideFadePresentationOnNextOpen = true;
    private IStackableUI rootOwner;

    public bool IsActive => gameObject.activeSelf;
    public bool CanCloseOnEscape => true;
    public UIOpenGroup OpenGroup => UIOpenGroup.ExclusiveModal;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.ExclusiveModal;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;
    public MouseCursorDomain CursorDomain => MouseCursorDomain.Inventory;

    public void SetSlideFadePresentationForNextOpen(bool playPresentation)
    {
        playSlideFadePresentationOnNextOpen = playPresentation;
    }

    public void SetPresentationForNextOpen(bool playPresentation)
    {
        SetSlideFadePresentationForNextOpen(playPresentation);
    }

    public void SetRootOwner(IStackableUI owner)
    {
        rootOwner = owner;
    }

    public void OpenUI()
    {
        ResolvePresentation();
        ResolvePlayerInventoryPanel();

        bool shouldPlaySlideFade = playSlideFadePresentationOnNextOpen;
        playSlideFadePresentationOnNextOpen = true;

        if (slideFadePresentation == null)
        {
            gameObject.SetActive(true);
            firstOpenRevealPresentation?.SnapOpen();
            return;
        }

        if (shouldPlaySlideFade)
        {
            firstOpenRevealPresentation?.SnapOpen();
            slideFadePresentation.PlayOpen();
        }
        else if (firstOpenRevealPresentation != null)
        {
            slideFadePresentation.SnapOpen();
            firstOpenRevealPresentation.PlayOpen();
        }
        else
        {
            slideFadePresentation.SnapOpen();
        }
    }

    public void CloseUI()
    {
        ItemDragContext.CancelActiveDragSession();
        UIManager.Instance?.HideHoverImmediate();

        ResolvePresentation();

        if (slideFadePresentation != null)
        {
            slideFadePresentation.PlayClose(NotifyChestClosed);
            return;
        }

        gameObject.SetActive(false);
        NotifyChestClosed();
    }

    private void Awake()
    {
        ResolvePresentation();
        ResolvePlayerInventoryPanel();

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() =>
            {
                IStackableUI closeTarget = rootOwner ?? this;
                if (UIManager.Instance != null)
                    UIManager.Instance.PopUI(closeTarget);
                else
                    closeTarget.CloseUI();
            });
        }
    }

    private void OnEnable()
    {
        MouseCursorService.EnsureInstance().SetDomain(this, MouseCursorDomain.Inventory, priority: 100);
    }

    private void OnDisable()
    {
        ItemDragContext.CancelActiveDragSession();
        MouseCursorService.Instance?.ClearDomain(this);
        UIManager.Instance?.HideHoverImmediate();

        ClearChestSlots();
        DisposeChestAdapter();
        playerInventoryPanel?.ClearBinding();
        ItemContainerGroupRegistry.Clear();
    }

    public void Bind(ChestInventory inventory)
    {
        chestInventory = inventory;

        ResolvePresentation();
        ResolvePlayerInventoryPanel();
        SetInternalPlayerContentVisible(true);
        ResolvePlayerInventories();

        ClearChestSlots();
        DisposeChestAdapter();
        playerInventoryPanel?.ClearBinding();

        chestContainer = new ChestContainerAdapter(chestInventory);
        chestAdapterDisposer = chestContainer as IDisposable;

        Transform playerRoot = ResolveCurrentPlayerRoot();
        Transform dropOrigin = ResolveDropOrigin(playerRoot);

        if (playerInventoryPanel != null)
        {
            playerInventoryPanel.Bind(
                playerConsumableInventory,
                playerWeaponInventory,
                playerRelicInventory,
                dropOrigin,
                playerRoot);
        }

        ItemContainerGroupRegistry.SetGroup(
            chestContainer,
            playerInventoryPanel != null ? playerInventoryPanel.ConsumableContainer : null,
            playerInventoryPanel != null ? playerInventoryPanel.WeaponContainer : null,
            playerInventoryPanel != null ? playerInventoryPanel.RelicContainer : null);

        BuildChestSlots();
        UIManager.Instance?.HideHoverImmediate();
    }

    public void BindChestOnly(ChestInventory inventory, PlayerInventoryPanelView sharedPlayerInventoryPanel)
    {
        chestInventory = inventory;

        ResolvePresentation();
        SetInternalPlayerContentVisible(false);

        ClearChestSlots();
        DisposeChestAdapter();

        chestContainer = new ChestContainerAdapter(chestInventory);
        chestAdapterDisposer = chestContainer as IDisposable;

        ItemContainerGroupRegistry.SetGroup(
            chestContainer,
            sharedPlayerInventoryPanel != null ? sharedPlayerInventoryPanel.ConsumableContainer : null,
            sharedPlayerInventoryPanel != null ? sharedPlayerInventoryPanel.WeaponContainer : null,
            sharedPlayerInventoryPanel != null ? sharedPlayerInventoryPanel.RelicContainer : null);

        BuildChestSlots();
        UIManager.Instance?.HideHoverImmediate();
    }

    public void ClearChestBinding()
    {
        ClearChestSlots();
        DisposeChestAdapter();
        SetInternalPlayerContentVisible(rootOwner == null);
    }

    public void PrepareForInventoryRoot(PlayerInventoryPanelView sharedPlayerInventoryPanel)
    {
        ResolvePresentation();
        SetInternalPlayerContentVisible(false);

        RectTransform playerPanelRect = sharedPlayerInventoryPanel != null
            ? sharedPlayerInventoryPanel.RectTransform
            : inventoryPanelRect;

        RectTransform playerStatRect = sharedPlayerInventoryPanel != null
            ? sharedPlayerInventoryPanel.PlayerStatPanelRect
            : null;

        firstOpenRevealPresentation?.ConfigurePanels(chestPanelRect, playerPanelRect, playerStatRect);
    }

    public void SnapOpenForInventoryRoot(PlayerInventoryPanelView sharedPlayerInventoryPanel)
    {
        PrepareForInventoryRoot(sharedPlayerInventoryPanel);
        firstOpenRevealPresentation?.SnapOpen();
    }

    public void PlayRevealForInventoryRoot(PlayerInventoryPanelView sharedPlayerInventoryPanel)
    {
        PrepareForInventoryRoot(sharedPlayerInventoryPanel);
        firstOpenRevealPresentation?.PlayOpen();
    }

    private void NotifyChestClosed()
    {
        if (ChestUIManager.Instance != null)
            ChestUIManager.Instance.HandleChestClosed();
    }

    private void ResolvePresentation()
    {
        if (slideFadePresentation == null)
        {
            slideFadePresentation = GetComponent<UISlideFadePresentation>();
            if (slideFadePresentation == null)
                slideFadePresentation = gameObject.AddComponent<UISlideFadePresentation>();
        }

        if (firstOpenRevealPresentation == null)
        {
            firstOpenRevealPresentation = GetComponent<ChestFirstOpenRevealPresentation>();
            if (firstOpenRevealPresentation == null)
                firstOpenRevealPresentation = gameObject.AddComponent<ChestFirstOpenRevealPresentation>();
        }
    }

    private void ResolvePlayerInventoryPanel()
    {
        if (playerInventoryPanel == null)
        {
            if (inventoryPanelRect != null)
                playerInventoryPanel = inventoryPanelRect.GetComponent<PlayerInventoryPanelView>();
            if (playerInventoryPanel == null)
                playerInventoryPanel = GetComponentInChildren<PlayerInventoryPanelView>(true);
            if (playerInventoryPanel == null)
            {
                GameObject target = inventoryPanelRect != null ? inventoryPanelRect.gameObject : gameObject;
                playerInventoryPanel = target.AddComponent<PlayerInventoryPanelView>();
            }
        }

        if (playerInventoryPanel == null)
            return;

        playerInventoryPanel.Configure(
            consumableGridRoot,
            weaponGridRoot,
            relicGridRoot,
            legacyPlayerStatPanel,
            consumableSlotPrefab,
            weaponSlotPrefab,
            relicSlotPrefab,
            dropZone);
    }

    private void SetInternalPlayerContentVisible(bool visible)
    {
        if (inventoryPanelRect != null)
            inventoryPanelRect.gameObject.SetActive(visible);

        RectTransform statRect = ResolveInternalPlayerStatRect();
        if (statRect != null)
            statRect.gameObject.SetActive(visible);
    }

    private RectTransform ResolveInternalPlayerStatRect()
    {
        if (legacyPlayerStatPanel != null)
            return legacyPlayerStatPanel.transform as RectTransform;

        RectTransform[] children = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            RectTransform child = children[i];
            if (child == null || child == inventoryPanelRect)
                continue;

            PlayerStatPanelView statPanel = child.GetComponent<PlayerStatPanelView>();
            if (statPanel == null)
                continue;

            legacyPlayerStatPanel = statPanel;
            return child;
        }

        return null;
    }

    private void ResolvePlayerInventories()
    {
        Transform currentPlayer = ResolveCurrentPlayerRoot();

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

    private static Transform ResolveCurrentPlayerRoot()
    {
        if (PlayerRuntimeRegistry.CurrentPlayer != null)
            return PlayerRuntimeRegistry.CurrentPlayer.transform;
        if (PlayerInteractor2D.Instance != null)
            return PlayerInteractor2D.Instance.transform;

        return PlayerRuntimeRegistry.GetPlayerTransform();
    }

    private static Transform ResolveDropOrigin(Transform playerRoot)
    {
        if (playerRoot != null)
            return playerRoot;
        if (PlayerInteractor2D.Instance != null)
            return PlayerInteractor2D.Instance.transform;

        return PlayerRuntimeRegistry.GetPlayerTransform();
    }

    private void BuildChestSlots()
    {
        BuildSlots(chestContainer, chestGridRoot, chestSlotPrefab);
    }

    private void BuildSlots(IItemContainer container, Transform gridRoot, ItemSlotUI slotPrefab)
    {
        if (container == null || gridRoot == null || slotPrefab == null)
            return;

        for (int i = 0; i < container.SlotCount; i++)
        {
            ItemSlotUI slot = Instantiate(slotPrefab, gridRoot);
            slot.Bind(container, i);
            spawnedChestSlots.Add(slot);
        }
    }

    private void ClearChestSlots()
    {
        for (int i = 0; i < spawnedChestSlots.Count; i++)
        {
            if (spawnedChestSlots[i] == null)
                continue;

            if (Application.isPlaying)
                Destroy(spawnedChestSlots[i].gameObject);
            else
                DestroyImmediate(spawnedChestSlots[i].gameObject);
        }

        spawnedChestSlots.Clear();
    }

    private void DisposeChestAdapter()
    {
        chestAdapterDisposer?.Dispose();
        chestAdapterDisposer = null;
        chestContainer = null;
    }

    private sealed class ChestContainerAdapter : IItemContainer, IDisposable, IRelicLevelProvider, IRelicSlotReceiver
    {
        private readonly ChestInventory inventory;
        public event Action OnChanged;

        public ChestContainerAdapter(ChestInventory inventory)
        {
            this.inventory = inventory;
            if (this.inventory != null)
                this.inventory.OnChanged += HandleChanged;
        }

        public int SlotCount => inventory != null ? inventory.Capacity : 0;

        public ScriptableObject Get(int index)
        {
            return inventory != null ? inventory.Get(index) : null;
        }

        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1)
        {
            return true;
        }

        public bool TrySet(int index, ScriptableObject item)
        {
            return inventory != null && inventory.Set(index, item);
        }

        public bool TrySwap(int a, int b)
        {
            return inventory != null && inventory.Swap(a, b);
        }

        public bool TryGetRelicLevel(int index, out int level)
        {
            level = inventory != null ? inventory.GetRelicLevelInSlot(index) : 0;
            return level > 0;
        }

        public bool TrySetRelicWithLevel(int index, RelicDefinition relic, int level)
        {
            return inventory != null && inventory.SetRelicWithLevel(index, relic, level);
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
}

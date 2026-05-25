using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class ChestScreen : MonoBehaviour, IStackableUI, IMouseCursorDomainSource, ICloseRequestHandler
{
    private enum RerollRevealState
    {
        Idle,
        Closing,
        Shaking,
        Opening
    }

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

    [Header("Chest Reroll")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private HoldActionButton rerollHoldActionButton;
    [SerializeField] private HoldFillButtonView rerollHoldButtonView;
    [SerializeField] private Image rerollHoldProgressImage;
    [SerializeField] private TMP_Text rerollCountLabel;
    [SerializeField, Tooltip("{0}=remaining, {1}=limit, {2}=used")]
    private string rerollCountFormat = "{0}";
    [SerializeField] private KeyCode rerollHoldKey = KeyCode.Space;
    [SerializeField, Min(0.01f)] private float rerollHoldDuration = 1.5f;
    [SerializeField, Min(0.01f)] private float rerollCloseDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float rerollOpenDuration = 0.18f;
    [SerializeField] private Vector2 rerollShakeStrength = new(30f, 30f);
    [SerializeField, Min(0f)] private float rerollShakeFrequency = 18f;
    [SerializeField] private bool enableSpaceReroll = true;
    [SerializeField] private bool enableButtonHoldReroll = true;

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
    private RerollRevealState rerollRevealState = RerollRevealState.Idle;
    private float rerollRevealProgress = 1f;
    private bool rerollHoldActive;
    private bool rerollOpenVfxActive;
    private bool rerollSlotRevealVfxPending;
    private HoldActionButton subscribedRerollHoldActionButton;

    public bool IsActive => gameObject.activeSelf;
    public bool CanCloseOnEscape => true;
    public UIOpenGroup OpenGroup => UIOpenGroup.ExclusiveModal;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.ExclusiveModal;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;
    public MouseCursorDomain CursorDomain => MouseCursorDomain.Inventory;
    public bool IsFirstOpenRevealPlaying =>
        firstOpenRevealPresentation != null && firstOpenRevealPresentation.IsOpenPresentationPlaying;

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

    public void SetRerollCountFormat(string format)
    {
        rerollCountFormat = format;
        RefreshRerollUi();
    }

    public void OpenUI()
    {
        ResolvePresentation();
        ResolvePlayerInventoryPanel();
        ResetRerollHoldState();
        RefreshRerollUi();

        bool shouldPlaySlideFade = playSlideFadePresentationOnNextOpen;
        playSlideFadePresentationOnNextOpen = true;

        if (slideFadePresentation == null)
        {
            gameObject.SetActive(true);
            firstOpenRevealPresentation?.SnapOpen();
            RefreshRerollUi();
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

        RefreshRerollUi();
    }

    public void CloseUI()
    {
        ItemDragContext.CancelActiveDragSession();
        UIManager.Instance?.HideHoverImmediate();
        ResetRerollHoldState();
        RefreshRerollUi();

        ResolvePresentation();

        if (slideFadePresentation != null)
        {
            slideFadePresentation.PlayClose(NotifyChestClosed);
            return;
        }

        gameObject.SetActive(false);
        NotifyChestClosed();
    }

    public bool TryHandleCloseRequest()
    {
        return IsFirstOpenRevealPlaying;
    }

    private void Awake()
    {
        ResolvePresentation();
        ResolvePlayerInventoryPanel();
        ResolveRerollHoldControls();
        SubscribeRerollHoldActionButton();
        ConfigureRerollHoldActionButton();
        RefreshRerollUi();

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() =>
            {
                IStackableUI closeTarget = rootOwner ?? this;
                if (closeTarget is ICloseRequestHandler closeHandler && closeHandler.TryHandleCloseRequest())
                    return;

                if (UIManager.Instance != null)
                    UIManager.Instance.PopUI(closeTarget);
                else
                    closeTarget.CloseUI();
            });
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        UpdateRerollReveal();
    }

    private void OnEnable()
    {
        MouseCursorService.EnsureInstance().SetDomain(this, MouseCursorDomain.Inventory, priority: 100);
        ResolveRerollHoldControls();
        SubscribeRerollHoldActionButton();
        ConfigureRerollHoldActionButton();
        RefreshRerollUi();
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
        UnsubscribeRerollHoldActionButton();
        ResetRerollHoldState();
        RefreshRerollUi();
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
        ResetRerollHoldState();
        RefreshRerollUi();
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
        ResetRerollHoldState();
        RefreshRerollUi();
    }

    public void ClearChestBinding()
    {
        ClearChestSlots();
        DisposeChestAdapter();
        SetInternalPlayerContentVisible(rootOwner == null);
        ResetRerollHoldState();
        RefreshRerollUi();
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
        RectTransform playerCollisionRect = sharedPlayerInventoryPanel != null
            ? sharedPlayerInventoryPanel.CollisionBoundsRect
            : inventoryPanelRect;

        firstOpenRevealPresentation?.ConfigurePanels(chestPanelRect, playerPanelRect, playerStatRect, playerCollisionRect);
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

    private void ResolveRerollHoldControls()
    {
        if (rerollHoldActionButton == null)
        {
            if (rerollHoldButtonView != null)
                rerollHoldActionButton = rerollHoldButtonView.GetComponent<HoldActionButton>();

            if (rerollHoldActionButton == null && rerollButton != null)
                rerollHoldActionButton = rerollButton.GetComponent<HoldActionButton>();
        }

        if (rerollHoldButtonView == null)
        {
            if (rerollHoldActionButton != null)
                rerollHoldButtonView = rerollHoldActionButton.HoldView;

            if (rerollHoldButtonView == null && rerollButton != null)
                rerollHoldButtonView = rerollButton.GetComponent<HoldFillButtonView>();
        }

        if (rerollButton == null && rerollHoldActionButton != null)
            rerollButton = rerollHoldActionButton.GetComponent<Button>();
    }

    private void SubscribeRerollHoldActionButton()
    {
        ResolveRerollHoldControls();
        if (subscribedRerollHoldActionButton == rerollHoldActionButton)
            return;

        UnsubscribeRerollHoldActionButton();

        if (rerollHoldActionButton == null)
            return;

        rerollHoldActionButton.HoldStarted += HandleRerollHoldStarted;
        rerollHoldActionButton.HoldCanceled += HandleRerollHoldCanceled;
        rerollHoldActionButton.HoldCompleted += HandleRerollHoldCompleted;
        rerollHoldActionButton.ProgressChanged += HandleRerollHoldProgressChanged;
        subscribedRerollHoldActionButton = rerollHoldActionButton;
    }

    private void UnsubscribeRerollHoldActionButton()
    {
        if (subscribedRerollHoldActionButton == null)
            return;

        subscribedRerollHoldActionButton.HoldStarted -= HandleRerollHoldStarted;
        subscribedRerollHoldActionButton.HoldCanceled -= HandleRerollHoldCanceled;
        subscribedRerollHoldActionButton.HoldCompleted -= HandleRerollHoldCompleted;
        subscribedRerollHoldActionButton.ProgressChanged -= HandleRerollHoldProgressChanged;
        subscribedRerollHoldActionButton = null;
    }

    private void ConfigureRerollHoldActionButton()
    {
        ResolveRerollHoldControls();

        if (rerollHoldActionButton == null)
            return;

        rerollHoldActionButton.SetHoldSeconds(rerollHoldDuration);
        rerollHoldActionButton.SetKeyboardHold(enableSpaceReroll, rerollHoldKey, startWhilePressed: true);
        rerollHoldActionButton.SetPointerHoldEnabled(enableButtonHoldReroll);
    }

    private void UpdateRerollReveal()
    {
        if (IsFirstOpenRevealPlaying)
        {
            if (rerollHoldActive || (rerollHoldActionButton != null && rerollHoldActionButton.IsHolding))
                ResetRerollHoldState();

            RefreshRerollUi();
            return;
        }

        AdvanceRerollReveal();
        RefreshRerollUi();
    }

    private bool CanStartRerollHold()
    {
        if (ChestUIManager.Instance == null)
            return false;

        ResolvePresentation();
        return firstOpenRevealPresentation != null && ChestUIManager.Instance.CanRefreshOpenedChest();
    }

    private void HandleRerollHoldStarted()
    {
        if (!CanStartRerollHold())
        {
            rerollHoldActionButton?.ResetHold();
            return;
        }

        BeginRerollHold();
    }

    private void HandleRerollHoldCanceled()
    {
        if (rerollHoldActive)
            CancelRerollHold();

        RefreshRerollHoldProgressUi(0f);
    }

    private void HandleRerollHoldCompleted()
    {
        if (rerollHoldActive)
            CompleteRerollHold();

        RefreshRerollHoldProgressUi(0f);
    }

    private void HandleRerollHoldProgressChanged(float progress)
    {
        float normalized = Mathf.Clamp01(progress);
        RefreshRerollHoldProgressUi(normalized);

        if (!rerollHoldActive)
            return;
        if (normalized <= 0f && rerollHoldActionButton != null && !rerollHoldActionButton.IsHolding)
            return;

        ApplyRerollHoldProgress(normalized);
    }

    private void BeginRerollHold()
    {
        rerollHoldActive = true;
        rerollOpenVfxActive = false;
        rerollSlotRevealVfxPending = false;
        rerollRevealState = RerollRevealState.Closing;
        ItemDragContext.CancelActiveDragSession();
        UIManager.Instance?.HideHoverImmediate();
        ApplyRerollHoldProgress(0f);
        RefreshRerollUi();
    }

    private void CancelRerollHold()
    {
        rerollHoldActive = false;
        rerollOpenVfxActive = false;
        rerollSlotRevealVfxPending = false;
        if (rerollRevealState == RerollRevealState.Closing || rerollRevealState == RerollRevealState.Shaking)
            rerollRevealState = RerollRevealState.Opening;

        ApplyRerollRevealPose(enableInteraction: false, applyShake: false);
    }

    private void ApplyRerollHoldProgress(float progress)
    {
        float normalized = Mathf.Clamp01(progress);
        float closeProgressThreshold = Mathf.Clamp01(rerollCloseDuration / Mathf.Max(0.01f, rerollHoldDuration));
        closeProgressThreshold = Mathf.Max(0.0001f, closeProgressThreshold);

        if (normalized < closeProgressThreshold)
        {
            rerollRevealState = RerollRevealState.Closing;
            float closeNormalized = Mathf.Clamp01(normalized / closeProgressThreshold);
            rerollRevealProgress = Mathf.Lerp(1f, 0f, closeNormalized);
            ApplyRerollRevealPose(enableInteraction: false, applyShake: false);
            return;
        }

        rerollRevealState = RerollRevealState.Shaking;
        rerollRevealProgress = 0f;
        ApplyRerollRevealPose(enableInteraction: false, applyShake: true);
    }

    private void AdvanceRerollReveal()
    {
        if (rerollRevealState != RerollRevealState.Opening)
            return;

        float openDelta = Time.unscaledDeltaTime / Mathf.Max(0.01f, rerollOpenDuration);
        rerollRevealProgress = Mathf.MoveTowards(rerollRevealProgress, 1f, openDelta);
        bool isOpen = rerollRevealProgress >= 1f;
        ApplyRerollRevealPose(enableInteraction: isOpen, applyShake: false);

        if (!isOpen)
            return;

        rerollRevealProgress = 1f;
        if (rerollSlotRevealVfxPending)
        {
            firstOpenRevealPresentation?.PlayManualSlotRevealVfx();
            rerollSlotRevealVfxPending = false;
        }

        rerollRevealState = RerollRevealState.Idle;
        rerollOpenVfxActive = false;
        RefreshRerollUi();
    }

    private void CompleteRerollHold()
    {
        bool refreshed = ChestUIManager.Instance != null && ChestUIManager.Instance.TryRefreshOpenedChest();
        rerollHoldActive = false;
        rerollRevealState = RerollRevealState.Opening;
        rerollOpenVfxActive = false;
        rerollSlotRevealVfxPending = false;
        ApplyRerollRevealPose(enableInteraction: false, applyShake: false);

        if (refreshed)
        {
            firstOpenRevealPresentation?.PlayManualOpenRevealVfx(playSlotRevealParticles: false);
            rerollOpenVfxActive = true;
            rerollSlotRevealVfxPending = true;
        }

        RefreshRerollUi();
    }

    private void ApplyRerollRevealPose(bool enableInteraction, bool applyShake)
    {
        if (firstOpenRevealPresentation == null)
            return;

        firstOpenRevealPresentation.ApplyManualRevealProgress(
            rerollRevealProgress,
            enableInteraction,
            stopPresentationEffects: !rerollOpenVfxActive,
            resizePivotY: 0f);

        if (applyShake)
            ApplyRerollShakeOffset();
    }

    private void ApplyRerollShakeOffset()
    {
        if (chestPanelRect == null || rerollShakeFrequency <= 0f || rerollShakeStrength == Vector2.zero)
            return;

        float angle = Time.unscaledTime * rerollShakeFrequency * Mathf.PI * 2f;
        Vector2 offset = new Vector2(
            Mathf.Sin(angle) * rerollShakeStrength.x,
            Mathf.Cos(angle * 1.37f) * rerollShakeStrength.y);

        chestPanelRect.anchoredPosition += offset;
    }

    private void RefreshRerollUi()
    {
        ResolveRerollHoldControls();
        ConfigureRerollHoldActionButton();

        int remainingCount = 0;
        int refreshLimit = 0;
        int usedCount = 0;
        bool canRefresh = false;

        if (ChestUIManager.Instance != null)
        {
            remainingCount = ChestUIManager.Instance.GetOpenedChestRemainingRefreshCount();
            refreshLimit = ChestUIManager.Instance.GetOpenedChestRefreshLimit();
            usedCount = ChestUIManager.Instance.GetOpenedChestRefreshUsedCount();
            canRefresh = ChestUIManager.Instance.CanRefreshOpenedChest();
        }

        bool isHolding = rerollHoldActionButton != null && rerollHoldActionButton.IsHolding;
        bool canInteract = canRefresh &&
                           !IsFirstOpenRevealPlaying &&
                           (rerollRevealState == RerollRevealState.Idle || isHolding);

        GameObject rerollControlRoot = ResolveRerollControlRoot();
        if (rerollControlRoot != null)
            rerollControlRoot.SetActive(true);

        if (rerollHoldActionButton != null)
        {
            rerollHoldActionButton.SetInteractable(canInteract);
            if (rerollHoldButtonView != null && rerollHoldButtonView != rerollHoldActionButton.HoldView)
                rerollHoldButtonView.SetInteractableVisual(canInteract);
        }
        else
        {
            rerollHoldButtonView?.SetInteractableVisual(canInteract);
        }

        if (rerollButton != null)
        {
            rerollButton.gameObject.SetActive(true);
            if (rerollHoldActionButton == null)
                rerollButton.interactable = canInteract;
        }

        if (rerollCountLabel != null)
            rerollCountLabel.text = FormatRerollCount(remainingCount, refreshLimit, usedCount);

        if (rerollHoldActionButton == null)
            RefreshRerollHoldProgressUi(0f);
    }

    private GameObject ResolveRerollControlRoot()
    {
        if (rerollHoldActionButton != null)
            return rerollHoldActionButton.gameObject;
        if (rerollHoldButtonView != null)
            return rerollHoldButtonView.gameObject;
        if (rerollButton != null)
            return rerollButton.gameObject;

        return null;
    }

    private string FormatRerollCount(int remainingCount, int refreshLimit, int usedCount)
    {
        if (string.IsNullOrWhiteSpace(rerollCountFormat))
            return remainingCount.ToString();

        try
        {
            return string.Format(rerollCountFormat, remainingCount, refreshLimit, usedCount);
        }
        catch (FormatException)
        {
            return remainingCount.ToString();
        }
    }

    private void RefreshRerollHoldProgressUi(float progress)
    {
        float normalized = Mathf.Clamp01(progress);

        if (rerollHoldButtonView != null || rerollHoldProgressImage == null)
            return;

        rerollHoldProgressImage.fillAmount = normalized;
        rerollHoldProgressImage.enabled = normalized > 0f;
    }

    private void ResetRerollHoldState()
    {
        rerollRevealState = RerollRevealState.Idle;
        rerollRevealProgress = 1f;
        rerollHoldActive = false;
        rerollOpenVfxActive = false;
        rerollSlotRevealVfxPending = false;
        rerollHoldActionButton?.ResetHold();
        rerollHoldButtonView?.SetProgress(0f);
        RefreshRerollHoldProgressUi(0f);
    }

    private void BuildChestSlots()
    {
        BuildSlots(chestContainer, chestGridRoot, chestSlotPrefab);
        firstOpenRevealPresentation?.ConfigureItemRevealSlots(spawnedChestSlots);
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
        firstOpenRevealPresentation?.ConfigureItemRevealSlots(null);
    }

    private void DisposeChestAdapter()
    {
        chestAdapterDisposer?.Dispose();
        chestAdapterDisposer = null;
        chestContainer = null;
    }

}

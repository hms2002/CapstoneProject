using DG.Tweening;
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
    [SerializeField] private RectTransform selectedItemsRoot;
    [SerializeField] private CanvasGroup selectedItemsGroup;
    [SerializeField] private Button confirmSelectionButton;
    [SerializeField] private Outline confirmSelectionWarningOutline;
    private readonly List<ItemSlotUI> selectedSlots = new();
    private Sequence selectionMotion;
    private bool confirmingSelection;
    private ItemSlotUI rejectedWeaponSlot;
    private Tween rejectedWeaponWarningTween;
    private float nextSelectionValidationTime;
    private ItemSlotUI transitSlot;
    private Transform transitDestination;
    private readonly Dictionary<Behaviour, bool> suspendedSelectionLayouts = new();

    [Header("Player Inventory")]
    [SerializeField] private PlayerInventoryPanelView playerInventoryPanel;

    [Header("UI Refs")]
    [SerializeField] private Button closeButton;

    [Header("Presentation")]
    [SerializeField] private UISlideFadePresentation slideFadePresentation;
    [SerializeField] private ChestFirstOpenRevealPresentation firstOpenRevealPresentation;

    [Header("Acquisition Limit")]
    [SerializeField] private TMP_Text acquisitionCountLabel;
    [SerializeField] private CanvasGroup acquisitionCountGroup;
    private ItemSlotUI[] returnHighlightSlots = Array.Empty<ItemSlotUI>();
    private ScriptableObject[] previousReturnItems = Array.Empty<ScriptableObject>();
    private bool[] nextReturnHighlights = Array.Empty<bool>();
    private readonly Dictionary<ScriptableObject, int> returnHighlightBudget = new();
    private bool counterVisible;
    private Tween counterFadeTween;
    private ChestInventory counterInventory;
    private Tween acquisitionWarningTween;
    private Vector2 counterRestPosition;
    private bool counterPoseCaptured;

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
    private UnityEngine.Object guidedSelectionOwner;
    private bool forwardingGuidedSelection;

    public ChestInventory BoundInventory => chestInventory;
    public ItemSlotUI FirstVisibleSlot => spawnedChestSlots.Count > 0 ? spawnedChestSlots[0] : null;
    public Button ConfirmButton => confirmSelectionButton;
    public bool IsSelectionReady => CanChangeSelection && !selectionMotion.IsActive();
    public event Action SelectionCommitted;

    // Optional scene-owned guidance. With no owner, normal chest behavior is unchanged.
    public bool AcquireGuidedSelection(UnityEngine.Object owner)
    {
        if (owner == null || (guidedSelectionOwner != null && guidedSelectionOwner != owner)) return false;
        guidedSelectionOwner = owner;
        ResetRerollHoldState();
        RefreshRerollUi();
        return true;
    }

    public void ReleaseGuidedSelection(UnityEngine.Object owner)
    {
        if (guidedSelectionOwner != owner) return;
        guidedSelectionOwner = null;
        RefreshRerollUi();
    }

    public bool TrySelectGuidedFirstSlot(UnityEngine.Object owner)
    {
        ItemSlotUI slot = FirstVisibleSlot;
        if (owner == null || guidedSelectionOwner != owner || slot == null || !IsSelectionReady) return false;
        forwardingGuidedSelection = true;
        try { ToggleSelection(slot); }
        finally { forwardingGuidedSelection = false; }
        return selectedSlots.Contains(slot);
    }

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
        if (IsFirstOpenRevealPlaying)
            return true;
        if (confirmingSelection || selectedSlots.Count == 0)
            return false;

        WarningPopupPlayback.ShowMessage("아이템을 획득하거나 선택을 해제해 주십시오.");
        SetConfirmCloseWarning(true);
        return true;
    }

    private void SetConfirmCloseWarning(bool visible)
    {
        if (confirmSelectionWarningOutline != null)
            confirmSelectionWarningOutline.enabled = visible;
    }

    private void Awake()
    {
        if (confirmSelectionButton != null)
            confirmSelectionButton.onClick.AddListener(ConfirmSelection);
        CaptureCounterPose();
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
        UpdateAcquisitionPresentation();
        RefreshSelectionControls();
        if (selectedSlots.Count > 0 && !confirmingSelection && Time.unscaledTime >= nextSelectionValidationTime)
        {
            nextSelectionValidationTime = Time.unscaledTime + .15f;
            RefreshBlockedSelectionSlots();
        }
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
        guidedSelectionOwner = null;
        forwardingGuidedSelection = false;
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

        BindAcquisitionCounter(chestInventory);
        chestContainer = new ChestContainerAdapter(chestInventory, selectionOnly: true);
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

        BindReturnHighlights(playerInventoryPanel);
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

        BindAcquisitionCounter(chestInventory);
        chestContainer = new ChestContainerAdapter(chestInventory, selectionOnly: true);
        chestAdapterDisposer = chestContainer as IDisposable;

        ItemContainerGroupRegistry.SetGroup(
            chestContainer,
            sharedPlayerInventoryPanel != null ? sharedPlayerInventoryPanel.ConsumableContainer : null,
            sharedPlayerInventoryPanel != null ? sharedPlayerInventoryPanel.WeaponContainer : null,
            sharedPlayerInventoryPanel != null ? sharedPlayerInventoryPanel.RelicContainer : null);

        BindReturnHighlights(sharedPlayerInventoryPanel);
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
        if (guidedSelectionOwner != null) return false;
        if (ChestUIManager.Instance == null || confirmingSelection || selectionMotion.IsActive())
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
            ClearChestSlots();
            BuildChestSlots();
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

        bool isRerollUnlocked = refreshLimit > 0;
        bool isHolding = rerollHoldActionButton != null && rerollHoldActionButton.IsHolding;
        bool canInteract = guidedSelectionOwner == null && canRefresh &&
                           !confirmingSelection && !selectionMotion.IsActive() &&
                           !IsFirstOpenRevealPlaying &&
                           (rerollRevealState == RerollRevealState.Idle || isHolding);

        GameObject rerollControlRoot = ResolveRerollControlRoot();
        GameObject rerollGroupRoot = ResolveRerollGroupRoot(rerollControlRoot);

        SetRerollUiVisible(isRerollUnlocked, rerollGroupRoot, rerollControlRoot);
        if (!isRerollUnlocked)
        {
            if (rerollHoldActionButton != null)
                rerollHoldActionButton.SetInteractable(false);
            else
                rerollHoldButtonView?.SetInteractableVisual(false);

            ResetRerollHoldState();
            return;
        }

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

    private GameObject ResolveRerollGroupRoot(GameObject rerollControlRoot)
    {
        if (rerollControlRoot == null || rerollCountLabel == null)
            return rerollControlRoot;

        Transform controlParent = rerollControlRoot.transform.parent;
        Transform labelParent = rerollCountLabel.transform.parent;
        return controlParent != null && controlParent == labelParent
            ? controlParent.gameObject
            : rerollControlRoot;
    }

    private void SetRerollUiVisible(bool visible, GameObject rerollGroupRoot, GameObject rerollControlRoot)
    {
        if (rerollGroupRoot != null)
        {
            if (rerollGroupRoot.activeSelf != visible)
                rerollGroupRoot.SetActive(visible);

            if (rerollGroupRoot != rerollControlRoot)
                return;
        }

        if (rerollControlRoot != null && rerollControlRoot.activeSelf != visible)
            rerollControlRoot.SetActive(visible);

        if (rerollCountLabel != null && rerollCountLabel.gameObject.activeSelf != visible)
            rerollCountLabel.gameObject.SetActive(visible);
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
            if (container.Get(i) == null) continue;
            ItemSlotUI slot = Instantiate(slotPrefab, gridRoot);
            slot.Bind(container, i);
            slot.SetSelectionClickHandler(ToggleSelection);
            spawnedChestSlots.Add(slot);
        }
    }

    private void ClearChestSlots()
    {
        ClearRejectedWeaponWarning();
        SetConfirmCloseWarning(false);
        StopSelectionMotion();
        selectedSlots.Clear();
        confirmingSelection = false;
        for (int i = 0; i < spawnedChestSlots.Count; i++)
        {
            if (spawnedChestSlots[i] == null)
                continue;

            // Destroy is deferred; remove old slots from layout immediately before a reroll rebuild.
            spawnedChestSlots[i].gameObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(spawnedChestSlots[i].gameObject);
            else
                DestroyImmediate(spawnedChestSlots[i].gameObject);
        }

        spawnedChestSlots.Clear();
        firstOpenRevealPresentation?.ConfigureItemRevealSlots(null);
        RefreshAcquisitionCounter();
        RefreshSelectionControls();
    }

    private void DisposeChestAdapter()
    {
        BindAcquisitionCounter(null);
        chestAdapterDisposer?.Dispose();
        chestAdapterDisposer = null;
        chestContainer = null;
    }

    private void BindAcquisitionCounter(ChestInventory inventory)
    {
        foreach (ItemSlotUI slot in returnHighlightSlots)
            if (slot != null) slot.SetChestReturnHighlight(false);
        returnHighlightSlots = Array.Empty<ItemSlotUI>();
        previousReturnItems = Array.Empty<ScriptableObject>();
        nextReturnHighlights = Array.Empty<bool>();
        returnHighlightBudget.Clear();
        counterFadeTween?.Kill();
        counterFadeTween = null;
        counterVisible = false;
        if (acquisitionCountGroup != null) acquisitionCountGroup.alpha = 0f;
        CaptureCounterPose();
        if (counterInventory != null)
        {
            counterInventory.OnChanged -= RefreshAcquisitionCounter;
            counterInventory.AcquisitionRejected -= PlayAcquisitionWarning;
        }
        acquisitionWarningTween?.Kill();
        acquisitionWarningTween = null;
        if (acquisitionCountLabel != null)
        {
            acquisitionCountLabel.color = Color.white;
            acquisitionCountLabel.rectTransform.anchoredPosition = counterRestPosition;
        }
        counterInventory = inventory;
        if (counterInventory != null)
        {
            counterInventory.OnChanged += RefreshAcquisitionCounter;
            counterInventory.AcquisitionRejected += PlayAcquisitionWarning;
        }
        RefreshAcquisitionCounter();
    }

    private void BindReturnHighlights(PlayerInventoryPanelView panel)
    {
        returnHighlightSlots = panel != null ? panel.GetComponentsInChildren<ItemSlotUI>(true) : Array.Empty<ItemSlotUI>();
        previousReturnItems = new ScriptableObject[returnHighlightSlots.Length];
        nextReturnHighlights = new bool[returnHighlightSlots.Length];
        RefreshReturnHighlights();
    }

    private void RefreshReturnHighlights()
    {
        returnHighlightBudget.Clear();
        if (counterInventory != null)
            foreach (ScriptableObject item in counterInventory.OutstandingAcquisitions)
                if (item != null)
                {
                    returnHighlightBudget.TryGetValue(item, out int count);
                    returnHighlightBudget[item] = count + 1;
                }

        Array.Clear(nextReturnHighlights, 0, nextReturnHighlights.Length);
        // Prefer a newly acquired slot, then retain existing markers, then allocate restored receipts.
        for (int pass = 0; pass < 3; pass++)
            for (int i = 0; i < returnHighlightSlots.Length; i++)
            {
                ItemSlotUI slot = returnHighlightSlots[i];
                ScriptableObject item = slot != null ? slot.CurrentItem : null;
                if (item == null || nextReturnHighlights[i] ||
                    !returnHighlightBudget.TryGetValue(item, out int remaining) || remaining <= 0)
                    continue;
                if (pass == 0 && item == previousReturnItems[i]) continue;
                if (pass == 1 && !slot.IsChestReturnHighlighted) continue;
                nextReturnHighlights[i] = true;
                returnHighlightBudget[item] = remaining - 1;
            }
        for (int i = 0; i < returnHighlightSlots.Length; i++)
        {
            ItemSlotUI slot = returnHighlightSlots[i];
            previousReturnItems[i] = slot != null ? slot.CurrentItem : null;
            if (slot != null) slot.SetChestReturnHighlight(nextReturnHighlights[i]);
        }
    }

    private void UpdateAcquisitionPresentation()
    {
        RefreshReturnHighlights();
        bool visible = counterInventory != null && !IsFirstOpenRevealPlaying &&
            rerollRevealState == RerollRevealState.Idle;
        if (visible == counterVisible) return;
        counterVisible = visible;
        counterFadeTween?.Kill();
        if (acquisitionCountGroup == null) return;
        if (visible) counterFadeTween = acquisitionCountGroup.DOFade(1f, 0.25f).SetUpdate(true);
        else acquisitionCountGroup.alpha = 0f;
    }

    private void CaptureCounterPose()
    {
        if (counterPoseCaptured || acquisitionCountLabel == null) return;
        counterRestPosition = acquisitionCountLabel.rectTransform.anchoredPosition;
        counterPoseCaptured = true;
    }

    private void RefreshAcquisitionCounter()
    {
        if (acquisitionCountLabel != null)
            acquisitionCountLabel.text = $"선택 {selectedSlots.Count} / {ChestInventory.AcquisitionLimit}";
    }

    private bool CanChangeSelection => chestContainer != null && !confirmingSelection &&
        !IsFirstOpenRevealPlaying && rerollRevealState == RerollRevealState.Idle;

    private void RefreshSelectionControls()
    {
        if (selectedSlots.Count == 0)
            SetConfirmCloseWarning(false);
        bool available = CanChangeSelection;
        if (confirmSelectionButton != null)
            confirmSelectionButton.interactable = available && selectedSlots.Count > 0 && !selectionMotion.IsActive();
        if (selectedItemsGroup != null)
        {
            selectedItemsGroup.alpha = available ? 1f : 0f;
            selectedItemsGroup.interactable = available;
            selectedItemsGroup.blocksRaycasts = available;
        }
    }

    private void ToggleSelection(ItemSlotUI slot)
    {
        if (guidedSelectionOwner != null && !forwardingGuidedSelection) return;
        if (!CanChangeSelection || selectedItemsRoot == null || slot == null || !slot.HasItem || selectionMotion.IsActive())
            return;
        bool removing = selectedSlots.Contains(slot);
        if (!removing && slot.CurrentItem is WeaponDefinition && IsWeaponInventoryFull())
        {
            ClearRejectedWeaponWarning();
            rejectedWeaponSlot = slot;
            slot.SetSelectionBlocked(true);
            rejectedWeaponWarningTween = DOVirtual.DelayedCall(0.8f, ClearRejectedWeaponWarning, true);
            WarningPopupPlayback.ShowMessage("무기 인벤토리가 가득 찼습니다. 인벤토리 무기를 버리고 획득을 시도해 주세요");
            return;
        }
        if (!removing && selectedSlots.Count + chestInventory.AcquiredCount >= ChestInventory.AcquisitionLimit)
        {
            PlayAcquisitionWarning();
            return;
        }

        if (rejectedWeaponSlot == slot) ClearRejectedWeaponWarning();
        var positions = new Dictionary<ItemSlotUI, Vector3>();
        foreach (ItemSlotUI item in spawnedChestSlots) positions[item] = item.transform.position;
        if (removing) selectedSlots.Remove(slot);
        else selectedSlots.Add(slot);
        slot.SetChestSelectionOverlay(!removing);
        slot.SetSelectionBlocked(false);
        nextSelectionValidationTime = 0f;
        slot.transform.SetParent(removing ? chestGridRoot : selectedItemsRoot, false);
        int sibling = 0;
        // The list retains original inventory order, independent of selection order.
        foreach (ItemSlotUI item in spawnedChestSlots)
            if (!selectedSlots.Contains(item)) item.transform.SetSiblingIndex(sibling++);
        for (int i = 0; i < selectedSlots.Count; i++) selectedSlots[i].transform.SetSiblingIndex(i);

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)chestGridRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(selectedItemsRoot);
        SetSelectionLayoutsEnabled(false);
        selectionMotion = DOTween.Sequence().SetUpdate(true);
        foreach (ItemSlotUI item in spawnedChestSlots)
        {
            Vector3 destination = item.transform.localPosition;
            Vector3 worldDestination = item.transform.position;
            item.transform.position = positions[item];
            if (item == slot && selectedItemsGroup != null)
            {
                // Travel above the chest viewport so its RectMask does not clip the flying slot.
                transitSlot = slot;
                transitDestination = slot.transform.parent;
                slot.transform.SetParent(selectedItemsGroup.transform, true);
                slot.transform.SetAsLastSibling();
                selectionMotion.Join(slot.transform.DOMove(worldDestination, 0.22f).SetEase(Ease.OutCubic));
            }
            else selectionMotion.Join(item.transform.DOLocalMove(destination, 0.22f).SetEase(Ease.OutCubic));
        }
        selectionMotion.OnComplete(() =>
        {
            selectionMotion = null;
            RestoreTransitSlot();
            SetSelectionLayoutsEnabled(true);
            RefreshSelectionControls();
        });
        UIManager.Instance?.HideHoverImmediate();
        RefreshAcquisitionCounter();
        RefreshSelectionControls();
    }

    private static bool IsWeaponInventoryFull()
    {
        IItemContainer weapons = ItemContainerGroupRegistry.WeaponEquip;
        if (weapons == null || weapons.SlotCount == 0) return false;
        for (int i = 0; i < weapons.SlotCount; i++)
            if (weapons.Get(i) == null) return false;
        return true;
    }

    private void ClearRejectedWeaponWarning()
    {
        rejectedWeaponWarningTween?.Kill();
        rejectedWeaponWarningTween = null;
        if (rejectedWeaponSlot != null) rejectedWeaponSlot.SetSelectionBlocked(false);
        rejectedWeaponSlot = null;
    }

    private void SetSelectionLayoutsEnabled(bool enabled)
    {
        if (enabled)
        {
            foreach (var state in suspendedSelectionLayouts)
                if (state.Key != null) state.Key.enabled = state.Value;
            suspendedSelectionLayouts.Clear();
            return;
        }
        foreach (Transform root in new Transform[] { chestGridRoot, selectedItemsRoot })
        {
            if (root == null) continue;
            Suspend(root.GetComponent<GridLayoutGroup>());
            Suspend(root.GetComponent<ContentSizeFitter>());
        }
        void Suspend(Behaviour layout)
        {
            if (layout == null || suspendedSelectionLayouts.ContainsKey(layout)) return;
            suspendedSelectionLayouts.Add(layout, layout.enabled);
            layout.enabled = false;
        }
    }

    private void RestoreTransitSlot()
    {
        if (transitSlot != null && transitDestination != null)
        {
            transitSlot.transform.SetParent(transitDestination, true);
            int sibling = 0;
            foreach (ItemSlotUI slot in spawnedChestSlots)
                if (slot != null && !selectedSlots.Contains(slot)) slot.transform.SetSiblingIndex(sibling++);
            for (int i = 0; i < selectedSlots.Count; i++) selectedSlots[i].transform.SetSiblingIndex(i);
        }
        transitSlot = null;
        transitDestination = null;
    }

    private void StopSelectionMotion()
    {
        selectionMotion?.Kill();
        selectionMotion = null;
        RestoreTransitSlot();
        SetSelectionLayoutsEnabled(true);
    }

    private void RefreshBlockedSelectionSlots()
    {
        if (chestInventory == null) return;
        var remaining = new List<int>();
        var blocked = new HashSet<int>();
        foreach (ItemSlotUI slot in selectedSlots) remaining.Add(slot.BoundIndex);
        using var source = new ChestContainerAdapter(chestInventory);
        // Re-run the same reservation rules without each rejected slot to locate all blockers.
        while (remaining.Count > 0 && !ChestSelectionTransferService.TryCreatePlanWithFailure(source, remaining,
            ItemContainerGroupRegistry.ConsumableEquip, ItemContainerGroupRegistry.WeaponEquip,
            ItemContainerGroupRegistry.RelicEquip, out _, out _, out int failedIndex))
        {
            if (failedIndex < 0)
            {
                foreach (int index in remaining) blocked.Add(index);
                break;
            }
            blocked.Add(failedIndex);
            if (!remaining.Remove(failedIndex)) break;
        }
        foreach (ItemSlotUI slot in selectedSlots) slot.SetSelectionBlocked(blocked.Contains(slot.BoundIndex));
    }

    private void ConfirmSelection()
    {
        if (guidedSelectionOwner != null) return;
        if (!CanChangeSelection || selectedSlots.Count == 0 || selectionMotion.IsActive()) return;
        var indices = new List<int>();
        foreach (ItemSlotUI slot in selectedSlots) indices.Add(slot.BoundIndex);
        using var source = new ChestContainerAdapter(chestInventory);
        if (!ChestSelectionTransferService.TryCreatePlanWithFailure(source, indices,
            ItemContainerGroupRegistry.ConsumableEquip, ItemContainerGroupRegistry.WeaponEquip,
            ItemContainerGroupRegistry.RelicEquip, out var plan, out string warning, out _))
        {
            RefreshBlockedSelectionSlots();
            WarningPopupPlayback.ShowMessage(warning);
            return;
        }

        confirmingSelection = true;
        RefreshSelectionControls();
        InventoryTransferResult result = ChestSelectionTransferService.TryCommitPlanWithFailure(plan, out int failedIndex);
        if (!result.Succeeded)
        {
            selectedSlots.RemoveAll(item => !item.HasItem);
            confirmingSelection = false;
            foreach (ItemSlotUI slot in selectedSlots) slot.SetSelectionBlocked(slot.BoundIndex == failedIndex);
            nextSelectionValidationTime = Time.unscaledTime + 1f;
            if (result.HasWarning) WarningPopupPlayback.Show(result.WarningCode);
            else WarningPopupPlayback.ShowMessage("아이템을 획득할 수 없습니다. 인벤토리와 유물 상태를 확인해 주세요.");
            RefreshAcquisitionCounter();
            RefreshSelectionControls();
            return;
        }
        SetConfirmCloseWarning(false);
        ChestUIManager.Instance?.CompleteOpenedChest(chestInventory);
        SelectionCommitted?.Invoke();
        IStackableUI closeTarget = rootOwner ?? this;
        if (UIManager.Instance != null) UIManager.Instance.PopUI(closeTarget);
        else closeTarget.CloseUI();
    }

    private void PlayAcquisitionWarning()
    {
        if (acquisitionCountLabel == null || !isActiveAndEnabled) return;
        acquisitionWarningTween?.Kill();
        RectTransform rect = acquisitionCountLabel.rectTransform;
        rect.anchoredPosition = counterRestPosition;
        acquisitionCountLabel.color = Color.red;
        acquisitionWarningTween = DOTween.Sequence().SetUpdate(true)
            .Append(rect.DOShakeAnchorPos(0.35f, new Vector2(10f, 0f), 18, 0f))
            .Append(acquisitionCountLabel.DOColor(Color.white, 0.2f))
            .OnComplete(() => rect.anchoredPosition = counterRestPosition);
    }

}

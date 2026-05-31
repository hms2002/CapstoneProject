using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

public class InventoryScreen : MonoBehaviour, IStackableUI, IMouseCursorDomainSource, ICloseRequestHandler
{
    private enum OpenMode
    {
        PlayerOnly,
        Chest
    }

    [Header("Player Inventory")]
    [SerializeField] private PlayerInventoryPanelView playerInventoryPanel;
    [SerializeField] private RectTransform playerInventoryPanelRect;

    [Header("Player Stat")]
    [SerializeField] private PlayerStatPanelView playerStatPanel;

    [Header("Chest Inventory")]
    [SerializeField] private ChestScreen chestInventoryScreen;

    [Header("UI Refs")]
    [SerializeField] private Button closeButton;

    [Header("Presentation")]
    [SerializeField] private UISlideFadePresentation slideFadePresentation;

    [SerializeField, HideInInspector] private Transform consumableGridRoot;
    [SerializeField, HideInInspector] private Transform weaponGridRoot;
    [SerializeField, HideInInspector] private Transform relicGridRoot;
    [SerializeField, HideInInspector] private ItemSlotUI consumableSlotPrefab;
    [SerializeField, HideInInspector] private ItemSlotUI weaponSlotPrefab;
    [SerializeField, HideInInspector] private ItemSlotUI relicSlotPrefab;
    [SerializeField, HideInInspector] private DropZoneUI dropZone;

    private PlayerConsumableInventory playerConsumableInventory;
    private WeaponInventory2D playerWeaponInventory;
    private RelicInventory playerRelicInventory;
    private OpenMode openMode;
    private bool playSlideFadePresentationOnNextOpen = true;
    private Coroutine chestRevealInputBlockRoutine;
    private GameFlowInputBlocker chestRevealInputBlocker;
    private readonly HashSet<int> inspectionOnlyOwnerIds = new HashSet<int>();

    public bool IsActive => gameObject.activeSelf;
    public bool IsInspectionOnly => inspectionOnlyOwnerIds.Count > 0;
    public bool CanCloseOnEscape => true;
    public UIOpenGroup OpenGroup => UIOpenGroup.ExclusiveModal;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.ExclusiveModal;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;
    public MouseCursorDomain CursorDomain => MouseCursorDomain.Inventory;
    public bool IsChestFirstOpenRevealPlaying =>
        openMode == OpenMode.Chest &&
        chestInventoryScreen != null &&
        chestInventoryScreen.IsFirstOpenRevealPlaying;

    public void AcquireInspectionOnlyMode(object owner)
    {
        int ownerId = ResolveInspectionOwnerId(owner);
        if (ownerId == 0)
            return;

        if (!inspectionOnlyOwnerIds.Add(ownerId))
            return;

        ApplyInspectionOnlyState();
    }

    public void ReleaseInspectionOnlyMode(object owner)
    {
        int ownerId = ResolveInspectionOwnerId(owner);
        if (ownerId == 0)
            return;

        if (!inspectionOnlyOwnerIds.Remove(ownerId))
            return;

        ApplyInspectionOnlyState();
    }

    public void OpenUI()
    {
        ResolvePresentation();
        ResolvePlayerInventoryPanel();

        if (openMode == OpenMode.Chest)
            OpenChestMode();
        else
            OpenPlayerOnlyMode();
    }

    public void CloseUI()
    {
        ItemDragContext.CancelActiveDragSession();
        UIManager.Instance?.HideHoverImmediate();

        bool notifyChestClosed = openMode == OpenMode.Chest;
        ResolvePresentation();

        if (slideFadePresentation != null)
        {
            slideFadePresentation.PlayClose(() => FinishClose(notifyChestClosed));
            return;
        }

        gameObject.SetActive(false);
        FinishClose(notifyChestClosed);
    }

    public bool TryHandleCloseRequest()
    {
        return IsChestFirstOpenRevealPlaying;
    }

    private void Awake()
    {
        ResolvePresentation();
        ResolveChestInventoryScreen();

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() =>
            {
                if (TryHandleCloseRequest())
                    return;

                if (UIManager.Instance != null)
                    UIManager.Instance.PopUI(this);
                else
                    CloseUI();
            });
        }
    }

    private void OnEnable()
    {
        MouseCursorService.EnsureInstance().SetDomain(this, MouseCursorDomain.Inventory, priority: 100);
    }

    private void OnDisable()
    {
        ReleaseChestRevealInputBlock();
        ClearInspectionOnlyMode();
        ItemDragContext.CancelActiveDragSession();
        MouseCursorService.Instance?.ClearDomain(this);
        playerInventoryPanel?.ClearBinding();
        chestInventoryScreen?.ClearChestBinding();
        ItemContainerGroupRegistry.Clear();

        UIManager.Instance?.HideHoverImmediate();
    }

    private void ClearInspectionOnlyMode()
    {
        if (inspectionOnlyOwnerIds.Count == 0)
            return;

        inspectionOnlyOwnerIds.Clear();
        ApplyInspectionOnlyState();
    }

    private void ApplyInspectionOnlyState()
    {
        bool inspectionOnly = IsInspectionOnly;
        ItemContainerGroupRegistry.SetInspectionOnly(inspectionOnly);

        if (!inspectionOnly)
            return;

        ItemDragContext.CancelActiveDragSession();
        DropZoneUI.ActiveInstance?.Hide();
    }

    private static int ResolveInspectionOwnerId(object owner)
    {
        if (owner == null)
            return 0;

        if (owner is UnityEngine.Object unityObject)
            return unityObject.GetInstanceID();

        return RuntimeHelpers.GetHashCode(owner);
    }

    public void Bind(
        PlayerConsumableInventory consumableInventory,
        WeaponInventory2D weaponInventory,
        RelicInventory relicInventory,
        Transform lootOrigin,
        Transform playerRoot)
    {
        openMode = OpenMode.PlayerOnly;
        playSlideFadePresentationOnNextOpen = true;

        ResolvePlayerInventoryPanel();
        SetChestInventoryVisible(false);

        BindPlayerInventory(consumableInventory, weaponInventory, relicInventory, lootOrigin, playerRoot);
        ItemContainerGroupRegistry.SetGroup(
            null,
            playerInventoryPanel != null ? playerInventoryPanel.ConsumableContainer : null,
            playerInventoryPanel != null ? playerInventoryPanel.WeaponContainer : null,
            playerInventoryPanel != null ? playerInventoryPanel.RelicContainer : null);
    }

    public void BindChest(ChestInventory chestInventory, bool playSlideFadePresentation)
    {
        openMode = OpenMode.Chest;
        playSlideFadePresentationOnNextOpen = playSlideFadePresentation;

        ResolvePlayerInventoryPanel();
        ResolveChestInventoryScreen();
        ResolvePlayerInventories();

        Transform playerRoot = ResolveCurrentPlayerRoot();
        Transform dropOrigin = ResolveDropOrigin(playerRoot);
        BindPlayerInventory(playerConsumableInventory, playerWeaponInventory, playerRelicInventory, dropOrigin, playerRoot);

        if (chestInventoryScreen != null)
        {
            chestInventoryScreen.gameObject.SetActive(true);
            chestInventoryScreen.SetRootOwner(this);
            chestInventoryScreen.BindChestOnly(chestInventory, playerInventoryPanel);
        }
    }

    public void CancelPreparedOpen()
    {
        ItemDragContext.CancelActiveDragSession();
        playerInventoryPanel?.ClearBinding();
        chestInventoryScreen?.ClearChestBinding();
        SetChestInventoryVisible(false);
        ItemContainerGroupRegistry.Clear();
        UIManager.Instance?.HideHoverImmediate();
    }

    private void OpenPlayerOnlyMode()
    {
        SetChestInventoryVisible(false);

        if (slideFadePresentation != null)
            slideFadePresentation.PlayOpen();
        else
            gameObject.SetActive(true);
    }

    private void OpenChestMode()
    {
        ResolveChestInventoryScreen();
        if (chestInventoryScreen != null)
            chestInventoryScreen.gameObject.SetActive(true);

        bool shouldPlaySlideFade = playSlideFadePresentationOnNextOpen;
        playSlideFadePresentationOnNextOpen = true;

        if (slideFadePresentation == null)
        {
            gameObject.SetActive(true);
            chestInventoryScreen?.SnapOpenForInventoryRoot(playerInventoryPanel);
            return;
        }

        if (shouldPlaySlideFade)
        {
            chestInventoryScreen?.SnapOpenForInventoryRoot(playerInventoryPanel);
            slideFadePresentation.PlayOpen();
            return;
        }

        slideFadePresentation.SnapOpen();
        AcquireChestRevealInputBlock();
        chestInventoryScreen?.PlayRevealForInventoryRoot(playerInventoryPanel);
        TrackChestRevealInputBlockRelease();
    }

    private void FinishClose(bool notifyChestClosed)
    {
        ReleaseChestRevealInputBlock();

        if (notifyChestClosed && ChestUIManager.Instance != null)
            ChestUIManager.Instance.HandleChestClosed();
    }

    private void AcquireChestRevealInputBlock()
    {
        if (chestRevealInputBlocker != null && chestRevealInputBlocker.IsBlocking)
            return;

        chestRevealInputBlocker = GameFlowInputBlocker.GetOrAdd(this);
        chestRevealInputBlocker?.Acquire();
    }

    private void TrackChestRevealInputBlockRelease()
    {
        if (chestRevealInputBlocker == null || !chestRevealInputBlocker.IsBlocking)
            return;

        if (chestRevealInputBlockRoutine != null)
            StopCoroutine(chestRevealInputBlockRoutine);

        chestRevealInputBlockRoutine = StartCoroutine(ReleaseChestRevealInputBlockWhenReady());
    }

    private IEnumerator ReleaseChestRevealInputBlockWhenReady()
    {
        yield return null;

        while (IsChestFirstOpenRevealPlaying)
            yield return null;

        chestRevealInputBlockRoutine = null;
        ReleaseChestRevealInputBlock();
    }

    private void ReleaseChestRevealInputBlock()
    {
        if (chestRevealInputBlockRoutine != null)
        {
            StopCoroutine(chestRevealInputBlockRoutine);
            chestRevealInputBlockRoutine = null;
        }

        chestRevealInputBlocker?.Release();
    }

    private void BindPlayerInventory(
        PlayerConsumableInventory consumableInventory,
        WeaponInventory2D weaponInventory,
        RelicInventory relicInventory,
        Transform dropOrigin,
        Transform playerRoot)
    {
        if (playerInventoryPanel == null)
            return;

        playerInventoryPanel.Bind(consumableInventory, weaponInventory, relicInventory, dropOrigin, playerRoot);
    }

    private void ResolvePresentation()
    {
        if (slideFadePresentation != null)
            return;

        slideFadePresentation = GetComponent<UISlideFadePresentation>();
        if (slideFadePresentation == null)
            slideFadePresentation = gameObject.AddComponent<UISlideFadePresentation>();
    }

    private void ResolvePlayerInventoryPanel()
    {
        if (playerInventoryPanel == null)
        {
            playerInventoryPanel = GetComponent<PlayerInventoryPanelView>();
            if (playerInventoryPanel == null)
                playerInventoryPanel = GetComponentInChildren<PlayerInventoryPanelView>(true);
            if (playerInventoryPanel == null)
                playerInventoryPanel = gameObject.AddComponent<PlayerInventoryPanelView>();
        }

        PlayerStatPanelView sharedStatPanel = ResolvePlayerStatPanel();
        playerInventoryPanel.Configure(
            consumableGridRoot,
            weaponGridRoot,
            relicGridRoot,
            sharedStatPanel,
            consumableSlotPrefab,
            weaponSlotPrefab,
            relicSlotPrefab,
            dropZone,
            ResolvePlayerInventoryPanelRect());
        playerInventoryPanel.SetPlayerStatPanel(sharedStatPanel);
    }

    private void ResolveChestInventoryScreen()
    {
        if (chestInventoryScreen == null)
            chestInventoryScreen = GetComponentInChildren<ChestScreen>(true);

        if (chestInventoryScreen == null)
            chestInventoryScreen = FindFirstObjectByType<ChestScreen>(FindObjectsInactive.Include);

        if (chestInventoryScreen == null)
            return;

        chestInventoryScreen.SetRootOwner(this);

        RectTransform chestRect = chestInventoryScreen.transform as RectTransform;
        RectTransform rootRect = transform as RectTransform;
        if (Application.isPlaying && chestRect != null && rootRect != null && chestRect.parent != rootRect)
            chestRect.SetParent(rootRect, false);
    }

    private RectTransform ResolvePlayerInventoryPanelRect()
    {
        if (playerInventoryPanelRect != null)
            return playerInventoryPanelRect;

        playerInventoryPanelRect = ResolveDirectChildContaining(consumableGridRoot);
        if (playerInventoryPanelRect != null)
            return playerInventoryPanelRect;

        if (playerInventoryPanel != null && playerInventoryPanel.transform != transform)
            playerInventoryPanelRect = playerInventoryPanel.transform as RectTransform;

        return playerInventoryPanelRect;
    }

    private PlayerStatPanelView ResolvePlayerStatPanel()
    {
        if (playerStatPanel != null)
            return playerStatPanel;

        ChestScreen chestScreen = chestInventoryScreen != null
            ? chestInventoryScreen
            : GetComponentInChildren<ChestScreen>(true);
        PlayerStatPanelView[] statPanels = GetComponentsInChildren<PlayerStatPanelView>(true);
        for (int i = 0; i < statPanels.Length; i++)
        {
            PlayerStatPanelView candidate = statPanels[i];
            if (candidate == null)
                continue;

            if (chestScreen != null && candidate.transform.IsChildOf(chestScreen.transform))
                continue;

            playerStatPanel = candidate;
            return playerStatPanel;
        }

        if (statPanels != null && statPanels.Length > 0)
            playerStatPanel = statPanels[0];

        return playerStatPanel;
    }

    private RectTransform ResolveDirectChildContaining(Transform child)
    {
        if (child == null)
            return null;

        Transform current = child;
        while (current != null && current.parent != transform)
            current = current.parent;

        return current as RectTransform;
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

    private void SetChestInventoryVisible(bool visible)
    {
        ResolveChestInventoryScreen();
        if (chestInventoryScreen != null)
            chestInventoryScreen.gameObject.SetActive(visible);
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
}

using UnityEngine;

/// <summary>
/// 책임 : 상자 전리품 UI 열기와 현재 열린 상자의 새로고침 요청을 실제 Inventory UI에 연결한다.
/// </summary>
public class ChestUIManager : MonoBehaviour, IChestUiOpenBackend
{
    public static ChestUIManager Instance { get; private set; }

    [SerializeField] private InventoryScreen inventoryRoot;
    [SerializeField] private ChestScreen chestScreen;

    private TreasureChest openedChest;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ChestUiOpenPlayback.RegisterBackend(this);
        ResolveInventoryRootReference();
        ResolveChestScreenReference();

        if (chestScreen != null)
            chestScreen.gameObject.SetActive(false);
    }

    public bool OpenChest(
        TreasureChest chest,
        bool playSlideFadePresentation = true,
        GameFlowInputBlocker inputBlocker = null)
    {
        if (chest == null)
            return false;

        ResolveInventoryRootReference();
        ResolveChestScreenReference();
        if (inventoryRoot == null)
        {
            Debug.LogError("[ChestUIManager] Inventory root reference is missing.");
            return false;
        }

        if (openedChest == chest && inventoryRoot.IsActive)
            return true;

        openedChest = chest;
        inventoryRoot.BindChest(chest.GetInventory(), playSlideFadePresentation);

        bool opened = true;
        if (inputBlocker != null)
        {
            opened = inputBlocker.TryPushOwnedUI(inventoryRoot);
        }
        else if (UIManager.Instance != null)
        {
            opened = UIManager.Instance.TryPushUI(inventoryRoot);
        }
        else
        {
            inventoryRoot.OpenUI();
        }

        if (!opened)
        {
            inventoryRoot.CancelPreparedOpen();
            openedChest = null;
            if (PlayerInteractor2D.Instance != null)
                PlayerInteractor2D.Instance.SetInteractState(InteractState.Idle);
        }

        return opened;
    }

    public void HandleChestClosed()
    {
        if (PlayerInteractor2D.Instance != null)
            PlayerInteractor2D.Instance.SetInteractState(InteractState.Idle);

        openedChest = null;
    }

    public bool CanRefreshOpenedChest()
    {
        return openedChest != null && openedChest.CanRefreshLoot();
    }

    public int GetOpenedChestRefreshLimit()
    {
        return openedChest != null ? openedChest.RefreshCountLimit : 0;
    }

    public int GetOpenedChestRefreshUsedCount()
    {
        return openedChest != null ? openedChest.RefreshCountUsed : 0;
    }

    public int GetOpenedChestRemainingRefreshCount()
    {
        return openedChest != null ? openedChest.RemainingRefreshCount : 0;
    }

    public bool TryRefreshOpenedChest()
    {
        return openedChest != null && openedChest.TryRefreshLoot();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            ChestUiOpenPlayback.RegisterBackend(null);

        if (Instance == this)
            Instance = null;
    }

    private void ResolveInventoryRootReference()
    {
        if (inventoryRoot != null)
            return;

        inventoryRoot = GetComponentInChildren<InventoryScreen>(true);
        if (inventoryRoot == null)
            inventoryRoot = FindFirstObjectByType<InventoryScreen>(FindObjectsInactive.Include);
    }

    private void ResolveChestScreenReference()
    {
        if (chestScreen != null)
            return;

        chestScreen = GetComponentInChildren<ChestScreen>(true);
        if (chestScreen == null && inventoryRoot != null)
            chestScreen = inventoryRoot.GetComponentInChildren<ChestScreen>(true);
        if (chestScreen == null)
            chestScreen = FindFirstObjectByType<ChestScreen>(FindObjectsInactive.Include);
    }
}

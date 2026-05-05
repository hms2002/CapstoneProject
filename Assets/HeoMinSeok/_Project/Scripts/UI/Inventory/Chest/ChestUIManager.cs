using UnityEngine;

public class ChestUIManager : MonoBehaviour
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
        ResolveInventoryRootReference();
        ResolveChestScreenReference();

        if (chestScreen != null)
            chestScreen.gameObject.SetActive(false);
    }

    public bool OpenChest(TreasureChest chest, bool playSlideFadePresentation = true)
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
        if (UIManager.Instance != null)
            opened = UIManager.Instance.TryPushUI(inventoryRoot);
        else
            inventoryRoot.OpenUI();

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

    private void OnDestroy()
    {
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

using UnityEngine;

/// <summary>
/// Standalone inventory UI (not tied to chests).
/// - Opens/closes via hotkey
/// - Game continues while open (no timeScale changes)
/// </summary>
public class InventoryUIManager : MonoBehaviour
{
    public static InventoryUIManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private InventoryScreen inventoryScreen;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;

    [Header("(Optional) Player reference")]
    [Tooltip("If null, will fallback to SampleTopDownPlayer.Instance")]
    [SerializeField] private Transform lootOriginOverride;

    private void Awake()
    {
        Instance = this;
        if (inventoryScreen != null)
            inventoryScreen.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (inventoryScreen != null && inventoryScreen.gameObject.activeSelf)
                Close();
            else
                Open();
        }
    }

    public void Open()
    {
        if (inventoryScreen == null) return;

        // Build refs
        var player = lootOriginOverride != null
            ? lootOriginOverride
            : (SampleTopDownPlayer.Instance != null ? SampleTopDownPlayer.Instance.transform : null);
        var weaponInv = FindFirstObjectByType<WeaponInventory2D>();
        var relicInv = FindFirstObjectByType<RelicInventory>();
        var backpack = FindFirstObjectByType<PlayerBackpackInventory>();

        inventoryScreen.gameObject.SetActive(true);
        inventoryScreen.Bind(backpack, weaponInv, relicInv, player);

        // Ensure hover/detail doesn't linger from other UIs
        UIHoverManager.Instance?.SetActivePanels((RectTransform)inventoryScreen.transform, null);
        UIHoverManager.Instance?.HideImmediate(); // 이전 디테일 남아있지 않게
        ItemDetailPanel.Instance?.Hide();

        inventoryScreen.gameObject.SetActive(true);

    }

    public void Close()
    {
        if (inventoryScreen != null)
            inventoryScreen.gameObject.SetActive(false);

        // Close should always clear hover/detail
        UIHoverManager.Instance?.HideImmediate();
        UIHoverManager.Instance?.SetActivePanels(null, null);
        ItemDetailPanel.Instance?.Hide();

        inventoryScreen.gameObject.SetActive(false);
    }
}

using UnityEngine;

[CreateAssetMenu(fileName = "IDV_ItemDisplayVisual", menuName = "Game/Item Display Visual Profile")]
public sealed class ItemDisplayVisualProfileSO : ScriptableObject
{
    [Header("Shared")]
    [SerializeField] private GameObject sharedVisualPrefab;
    [SerializeField] private Sprite sharedSpriteOverride;
    [SerializeField] private bool overrideSharedSpriteSettings;
    [SerializeField] private ItemDisplaySpriteSettings sharedSpriteSettings = ItemDisplaySpriteSettings.Raw(ItemDisplayAnchorMode.Bottom, new Vector2(0f, 0.08f));

    [Header("Shop Override (Legacy - shop uses World Drop visual policy)")]
    [SerializeField] private GameObject shopVisualPrefab;
    [SerializeField] private Sprite shopSpriteOverride;
    [SerializeField] private bool overrideShopSpriteSettings;
    [SerializeField] private ItemDisplaySpriteSettings shopSpriteSettings = ItemDisplaySpriteSettings.Raw(ItemDisplayAnchorMode.Center, Vector2.zero);

    [Header("World Drop Override")]
    [SerializeField] private GameObject worldDropVisualPrefab;
    [SerializeField] private Sprite worldDropSpriteOverride;
    [SerializeField] private bool overrideWorldDropSpriteSettings;
    [SerializeField] private ItemDisplaySpriteSettings worldDropSpriteSettings = ItemDisplaySpriteSettings.Raw(ItemDisplayAnchorMode.Bottom, new Vector2(0f, 0.08f));

    [Header("Inventory Slot Icon Override")]
    [SerializeField] private bool overrideInventorySlotIconSettings;
    [SerializeField] private ItemDisplayIconSettings inventorySlotIconSettings = ItemDisplayIconSettings.Default();

    [Header("Drag Icon Override")]
    [SerializeField] private bool overrideDragIconSettings;
    [SerializeField] private ItemDisplayIconSettings dragIconSettings = ItemDisplayIconSettings.Default();

    public GameObject GetVisualPrefab(ItemDisplayContext context)
    {
        return worldDropVisualPrefab != null ? worldDropVisualPrefab : sharedVisualPrefab;
    }

    public Sprite GetSpriteOverride(ItemDisplayContext context)
    {
        return worldDropSpriteOverride != null ? worldDropSpriteOverride : sharedSpriteOverride;
    }

    public bool TryGetSpriteSettings(ItemDisplayContext context, out ItemDisplaySpriteSettings settings)
    {
        if ((context == ItemDisplayContext.ShopDisplay || context == ItemDisplayContext.WorldDrop) &&
            overrideWorldDropSpriteSettings &&
            worldDropSpriteSettings != null)
        {
            settings = worldDropSpriteSettings;
            return true;
        }

        if (overrideSharedSpriteSettings && sharedSpriteSettings != null)
        {
            settings = sharedSpriteSettings;
            return true;
        }

        settings = null;
        return false;
    }

    public bool TryGetIconSettings(ItemDisplayIconContext context, out ItemDisplayIconSettings settings)
    {
        if (context == ItemDisplayIconContext.InventorySlot && overrideInventorySlotIconSettings && inventorySlotIconSettings != null)
        {
            settings = inventorySlotIconSettings;
            return true;
        }

        if (context == ItemDisplayIconContext.DragIcon && overrideDragIconSettings && dragIconSettings != null)
        {
            settings = dragIconSettings;
            return true;
        }

        settings = null;
        return false;
    }

    private void OnValidate()
    {
        sharedSpriteSettings?.OnValidate();
        shopSpriteSettings?.OnValidate();
        worldDropSpriteSettings?.OnValidate();
        inventorySlotIconSettings?.OnValidate();
        dragIconSettings?.OnValidate();
    }
}

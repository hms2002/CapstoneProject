using UnityEngine;

[CreateAssetMenu(fileName = "IDV_ItemDisplayVisual", menuName = "Game/Item Display Visual Profile")]
public sealed class ItemDisplayVisualProfileSO : ScriptableObject
{
    [Header("Shared")]
    [SerializeField] private GameObject sharedVisualPrefab;
    [SerializeField] private Sprite sharedSpriteOverride;
    [SerializeField] private bool overrideSharedSpriteSettings;
    [SerializeField] private ItemDisplaySpriteSettings sharedSpriteSettings = ItemDisplaySpriteSettings.Raw(ItemDisplayAnchorMode.Bottom, new Vector2(0f, 0.08f));

    [Header("Shop Override")]
    [SerializeField] private GameObject shopVisualPrefab;
    [SerializeField] private Sprite shopSpriteOverride;
    [SerializeField] private bool overrideShopSpriteSettings;
    [SerializeField] private ItemDisplaySpriteSettings shopSpriteSettings = ItemDisplaySpriteSettings.Raw(ItemDisplayAnchorMode.Center, Vector2.zero);

    [Header("World Drop Override")]
    [SerializeField] private GameObject worldDropVisualPrefab;
    [SerializeField] private Sprite worldDropSpriteOverride;
    [SerializeField] private bool overrideWorldDropSpriteSettings;
    [SerializeField] private ItemDisplaySpriteSettings worldDropSpriteSettings = ItemDisplaySpriteSettings.Raw(ItemDisplayAnchorMode.Bottom, new Vector2(0f, 0.08f));

    public GameObject GetVisualPrefab(ItemDisplayContext context)
    {
        GameObject contextPrefab = context == ItemDisplayContext.ShopDisplay
            ? shopVisualPrefab
            : worldDropVisualPrefab;

        return contextPrefab != null ? contextPrefab : sharedVisualPrefab;
    }

    public Sprite GetSpriteOverride(ItemDisplayContext context)
    {
        Sprite contextSprite = context == ItemDisplayContext.ShopDisplay
            ? shopSpriteOverride
            : worldDropSpriteOverride;

        return contextSprite != null ? contextSprite : sharedSpriteOverride;
    }

    public bool TryGetSpriteSettings(ItemDisplayContext context, out ItemDisplaySpriteSettings settings)
    {
        if (context == ItemDisplayContext.ShopDisplay && overrideShopSpriteSettings && shopSpriteSettings != null)
        {
            settings = shopSpriteSettings;
            return true;
        }

        if (context == ItemDisplayContext.WorldDrop && overrideWorldDropSpriteSettings && worldDropSpriteSettings != null)
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

    private void OnValidate()
    {
        sharedSpriteSettings?.OnValidate();
        shopSpriteSettings?.OnValidate();
        worldDropSpriteSettings?.OnValidate();
    }
}

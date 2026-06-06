using UnityEngine;

[CreateAssetMenu(fileName = "ShopDefinition", menuName = "Dialogue/Merchant/Shop Definition")]
public sealed class ShopDefinitionSO : ScriptableObject
{
    [Header("Unlock")]
    [SerializeField] private bool requireShopUpgrade = true;

    [Header("Stock")]
    [SerializeField, Min(0)] private int baseVisibleSlotCount = 3;
    [SerializeField, Min(0)] private int maxWeaponSlots = 1;
    [SerializeField, Min(0)] private int maxConsumableSlots = 1;
    [SerializeField] private ShopStockRollWeights stockRollWeights = new ShopStockRollWeights
    {
        weaponWeight = 1,
        relicWeight = 1,
        consumableWeight = 1
    };

    [Header("Price")]
    [SerializeField] private MerchantPriceSettings priceSettings = new MerchantPriceSettings
    {
        weaponPrice = 120,
        commonRelicPrice = 100,
        rareRelicPrice = 180,
        epicRelicPrice = 260,
        consumablePrice = 40
    };

    public bool RequireShopUpgrade => requireShopUpgrade;
    public int BaseVisibleSlotCount => Mathf.Max(0, baseVisibleSlotCount);
    public int MaxWeaponSlots => Mathf.Max(0, maxWeaponSlots);
    public int MaxConsumableSlots => Mathf.Max(0, maxConsumableSlots);
    public ShopStockRollWeights StockRollWeights => stockRollWeights;
    public MerchantPriceSettings PriceSettings => priceSettings;
}

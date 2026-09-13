using UnityEngine;

[CreateAssetMenu(fileName = "ShopDefinition", menuName = "Dialogue/Merchant/Shop Definition")]
public sealed class ShopDefinitionSO : ScriptableObject
{
    [SerializeField] private bool usesRunGold;
    public bool UsesRunGold => usesRunGold;

    public int RollGoldPrice(ScriptableObject item) => ((item switch
    {
        WeaponDefinition => Random.Range(1000, 1301),
        RelicDefinition relic when relic.rarity == ItemRarity.Epic => Random.Range(900, 1201),
        RelicDefinition relic when relic.rarity == ItemRarity.Rare => Random.Range(500, 801),
        RelicDefinition => Random.Range(200, 401),
        ConsumableDefinition => Random.Range(200, 301),
        _ => 0
    }) + 5) / 10 * 10;

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

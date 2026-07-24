[System.Flags]
public enum LootPoolExclusionSource
{
    None = 0,
    PlayerInventory = 1 << 0,
    WorldPickups = 1 << 1,
    SceneWeaponDrops = 1 << 2,
    MerchantStock = 1 << 3
}

public readonly struct LootPoolContext
{
    public static LootPoolContext None => new LootPoolContext(LootPoolExclusionSource.None);
    public static LootPoolContext PlayerInventory => new LootPoolContext(LootPoolExclusionSource.PlayerInventory);
    public static LootPoolContext ShopStock => new LootPoolContext(
        LootPoolExclusionSource.PlayerInventory |
        LootPoolExclusionSource.WorldPickups |
        LootPoolExclusionSource.SceneWeaponDrops);
    public static LootPoolContext MerchantStockOnly => new LootPoolContext(LootPoolExclusionSource.MerchantStock);
    public static LootPoolContext PlayerInventoryAndMerchantStock => new LootPoolContext(
        LootPoolExclusionSource.PlayerInventory |
        LootPoolExclusionSource.MerchantStock);

    public LootPoolExclusionSource WeaponExclusionSources { get; }

    public LootPoolContext(LootPoolExclusionSource weaponExclusionSources)
    {
        WeaponExclusionSources = weaponExclusionSources;
    }

    public bool Includes(LootPoolExclusionSource source)
    {
        return source != LootPoolExclusionSource.None &&
            (WeaponExclusionSources & source) == source;
    }
}

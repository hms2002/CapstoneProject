using System;
using System.Collections.Generic;

public sealed class LootPoolService
{
    private readonly Func<LootPoolContext, LootPoolWeaponExclusionSourceSet> weaponExclusionSourceProvider;

    public LootPoolService()
        : this(LootPoolLiveWeaponExclusionSourceProvider.Collect)
    {
    }

    internal LootPoolService(Func<LootPoolContext, LootPoolWeaponExclusionSourceSet> weaponExclusionSourceProvider)
    {
        this.weaponExclusionSourceProvider = weaponExclusionSourceProvider;
        if (this.weaponExclusionSourceProvider == null)
            this.weaponExclusionSourceProvider = LootPoolLiveWeaponExclusionSourceProvider.Collect;
    }

    public HashSet<string> BuildPlayerWeaponExclusionSet()
    {
        return BuildWeaponExclusionSet(LootPoolContext.PlayerInventory);
    }

    public HashSet<string> BuildShopWeaponExclusionSet()
    {
        return BuildWeaponExclusionSet(LootPoolContext.ShopStock);
    }

    public HashSet<string> BuildMerchantWeaponExclusionSet()
    {
        return BuildWeaponExclusionSet(LootPoolContext.MerchantStockOnly);
    }

    public HashSet<string> BuildWeaponExclusionSet(LootPoolContext context)
    {
        LootPoolWeaponExclusionSourceSet sourceSet = weaponExclusionSourceProvider(context);
        LootPoolWeaponExclusionResult result =
            LootPoolWeaponExclusionProvider.Collect(new LootPoolWeaponExclusionRequest(context, sourceSet));
        return result.ToHashSet();
    }

    public WeaponDefinition GetRandomWeapon(HashSet<string> exclusionList)
    {
        return LootPoolItemSelectionService.GetRandomWeapon(exclusionList);
    }

    public RelicDefinition GetRandomRelicByRarity(ItemRarity targetRarity)
    {
        return LootPoolItemSelectionService.GetRandomRelicByRarity(targetRarity);
    }

    public ConsumableDefinition GetRandomConsumable()
    {
        return LootPoolItemSelectionService.GetRandomConsumable();
    }
}

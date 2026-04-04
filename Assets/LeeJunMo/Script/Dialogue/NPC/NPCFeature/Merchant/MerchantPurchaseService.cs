using UnityEngine;

public enum MerchantPurchaseResultType
{
    Success,
    InvalidRequest,
    SoldOut,
    MissingDefinition,
    NotEnoughCurrency,
    InventoryFull,
    MissingSystems
}

public readonly struct MerchantPurchaseResult
{
    public MerchantPurchaseResultType Type { get; }
    public bool Succeeded => Type == MerchantPurchaseResultType.Success;

    public MerchantPurchaseResult(MerchantPurchaseResultType type)
    {
        Type = type;
    }
}

public sealed class MerchantPurchaseService
{
    public MerchantPurchaseResult TryPurchase(
        IPlayerInteractor player,
        MerchantStockEntryState stockEntry,
        ScriptableObject definition)
    {
        if (player is not Component playerComponent || stockEntry == null)
            return new MerchantPurchaseResult(MerchantPurchaseResultType.InvalidRequest);

        if (stockEntry.isSold)
            return new MerchantPurchaseResult(MerchantPurchaseResultType.SoldOut);

        if (definition == null)
            return new MerchantPurchaseResult(MerchantPurchaseResultType.MissingDefinition);

        if (CurrencyManager.Instance == null)
            return new MerchantPurchaseResult(MerchantPurchaseResultType.MissingSystems);

        if (!CanAcquire(playerComponent, definition))
            return new MerchantPurchaseResult(MerchantPurchaseResultType.InventoryFull);

        if (!CurrencyManager.Instance.SpendMagicStone(stockEntry.price))
            return new MerchantPurchaseResult(MerchantPurchaseResultType.NotEnoughCurrency);

        if (!TryAcquire(playerComponent, definition))
        {
            CurrencyManager.Instance.AddMagicStone(stockEntry.price);
            return new MerchantPurchaseResult(MerchantPurchaseResultType.InventoryFull);
        }

        return new MerchantPurchaseResult(MerchantPurchaseResultType.Success);
    }

    private static bool CanAcquire(Component playerComponent, ScriptableObject definition)
    {
        return definition switch
        {
            WeaponDefinition weapon => CanAcquireWeapon(playerComponent, weapon),
            RelicDefinition relic => CanAcquireRelic(playerComponent, relic),
            ConsumableDefinition consumable => CanAcquireConsumable(playerComponent, consumable),
            _ => false
        };
    }

    private static bool TryAcquire(Component playerComponent, ScriptableObject definition)
    {
        return definition switch
        {
            WeaponDefinition weapon => TryAcquireWeapon(playerComponent, weapon),
            RelicDefinition relic => TryAcquireRelic(playerComponent, relic),
            ConsumableDefinition consumable => TryAcquireConsumable(playerComponent, consumable),
            _ => false
        };
    }

    private static bool CanAcquireWeapon(Component playerComponent, WeaponDefinition weapon)
    {
        WeaponInventory2D inventory = playerComponent.GetComponent<WeaponInventory2D>();
        return inventory != null && inventory.CanAcquireWithoutReplacement(weapon);
    }

    private static bool CanAcquireRelic(Component playerComponent, RelicDefinition relic)
    {
        RelicInventory inventory = playerComponent.GetComponent<RelicInventory>();
        if (inventory == null || relic == null)
            return false;

        if (inventory.TryGetRelicLevelById(relic.relicId, out int currentLevel))
        {
            int nextLevel = relic.ClampLevel(currentLevel + Mathf.Max(1, relic.dropLevel));
            return nextLevel > currentLevel;
        }

        return inventory.Count < inventory.Capacity;
    }

    private static bool CanAcquireConsumable(Component playerComponent, ConsumableDefinition consumable)
    {
        if (consumable == null)
            return false;

        PlayerConsumableInventory inventory = PlayerConsumableInventory.GetOrAdd(playerComponent.transform);
        if (inventory == null)
            return false;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            if (inventory.GetConsumableInSlot(i) == null)
                return true;
        }

        return false;
    }

    private static bool TryAcquireWeapon(Component playerComponent, WeaponDefinition weapon)
    {
        WeaponInventory2D inventory = playerComponent.GetComponent<WeaponInventory2D>();
        return inventory != null && inventory.TryAcquireWithoutReplacement(weapon);
    }

    private static bool TryAcquireRelic(Component playerComponent, RelicDefinition relic)
    {
        RelicInventory inventory = playerComponent.GetComponent<RelicInventory>();
        return inventory != null && inventory.TryAcquireOrUpgrade(relic);
    }

    private static bool TryAcquireConsumable(Component playerComponent, ConsumableDefinition consumable)
    {
        PlayerConsumableInventory inventory = PlayerConsumableInventory.GetOrAdd(playerComponent.transform);
        return inventory != null && inventory.TryAcquire(consumable);
    }
}

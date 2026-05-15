using UnityEngine;

internal readonly struct WorldPickupDeliveryRequest
{
    public IPlayerInteractor Player { get; }
    public ScriptableObject Item { get; }
    public int RelicLevel { get; }

    public WorldPickupDeliveryRequest(IPlayerInteractor player, ScriptableObject item, int relicLevel)
    {
        Player = player;
        Item = item;
        RelicLevel = relicLevel;
    }
}

internal readonly struct WorldPickupDeliveryResult
{
    public static WorldPickupDeliveryResult Success =>
        new WorldPickupDeliveryResult(true, WorldPickupDeliveryFailureReason.None, WarningPopupCode.None);

    public bool Succeeded { get; }
    public WorldPickupDeliveryFailureReason FailureReason { get; }
    public WarningPopupCode WarningCode { get; }

    private WorldPickupDeliveryResult(
        bool succeeded,
        WorldPickupDeliveryFailureReason failureReason,
        WarningPopupCode warningCode)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
        WarningCode = warningCode;
    }

    public static WorldPickupDeliveryResult Failed(
        WorldPickupDeliveryFailureReason failureReason,
        WarningPopupCode warningCode = WarningPopupCode.None)
    {
        return new WorldPickupDeliveryResult(false, failureReason, warningCode);
    }
}

internal enum WorldPickupDeliveryFailureReason
{
    None = 0,
    MissingItem,
    UnsupportedItem,
    MissingInventory,
    WeaponRejected,
    RelicRejected,
    ConsumableRejected
}

internal static class WorldPickupDeliveryService
{
    public static WorldPickupDeliveryResult TryDeliver(WorldPickupDeliveryRequest request)
    {
        return request.Item switch
        {
            null => WorldPickupDeliveryResult.Failed(WorldPickupDeliveryFailureReason.MissingItem),
            WeaponDefinition weapon => TryDeliverWeapon(request.Player, weapon),
            RelicDefinition relic => TryDeliverRelic(request.Player, relic, request.RelicLevel),
            ConsumableDefinition consumable => TryDeliverConsumable(request.Player, consumable),
            _ => WorldPickupDeliveryResult.Failed(WorldPickupDeliveryFailureReason.UnsupportedItem)
        };
    }

    private static WorldPickupDeliveryResult TryDeliverWeapon(IPlayerInteractor player, WeaponDefinition weapon)
    {
        WeaponInventory2D weaponInventory = ResolveWeaponInventory(player);
        if (weaponInventory == null)
            return WorldPickupDeliveryResult.Failed(WorldPickupDeliveryFailureReason.MissingInventory);

        return weaponInventory.TryPickupWeapon(weapon)
            ? WorldPickupDeliveryResult.Success
            : WorldPickupDeliveryResult.Failed(WorldPickupDeliveryFailureReason.WeaponRejected);
    }

    private static WorldPickupDeliveryResult TryDeliverRelic(
        IPlayerInteractor player,
        RelicDefinition relic,
        int relicLevel)
    {
        RelicInventory relicInventory = ResolveRelicInventory(player);
        if (relicInventory == null)
            return WorldPickupDeliveryResult.Failed(WorldPickupDeliveryFailureReason.MissingInventory);

        int levelOverride = relicLevel > 0 ? relicLevel : -1;
        RelicInventory.AcquireResult result = relicInventory.TryAcquireOrUpgradeDetailed(relic, levelOverride);
        if (result == RelicInventory.AcquireResult.Success)
            return WorldPickupDeliveryResult.Success;

        return WorldPickupDeliveryResult.Failed(
            WorldPickupDeliveryFailureReason.RelicRejected,
            InventoryDeliveryWarningResolver.FromRelicAcquireResult(result));
    }

    private static WorldPickupDeliveryResult TryDeliverConsumable(
        IPlayerInteractor player,
        ConsumableDefinition consumable)
    {
        PlayerConsumableInventory consumableInventory = ResolveConsumableInventory(player);
        if (consumableInventory == null)
            return WorldPickupDeliveryResult.Failed(WorldPickupDeliveryFailureReason.MissingInventory);

        PlayerConsumableInventory.AcquireResult result = consumableInventory.TryAcquireDetailed(consumable);
        if (result == PlayerConsumableInventory.AcquireResult.Success)
            return WorldPickupDeliveryResult.Success;

        return WorldPickupDeliveryResult.Failed(
            WorldPickupDeliveryFailureReason.ConsumableRejected,
            InventoryDeliveryWarningResolver.FromConsumableAcquireResult(result));
    }

    private static WeaponInventory2D ResolveWeaponInventory(IPlayerInteractor player)
    {
        if (player is Component component)
            return component.GetComponent<WeaponInventory2D>();

        return null;
    }

    private static RelicInventory ResolveRelicInventory(IPlayerInteractor player)
    {
        if (player is Component component)
            return component.GetComponent<RelicInventory>();

        return null;
    }

    private static PlayerConsumableInventory ResolveConsumableInventory(IPlayerInteractor player)
    {
        if (player is Component component)
            return PlayerConsumableInventory.GetOrAdd(component.transform);

        return null;
    }
}

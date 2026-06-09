using UnityEngine;

[CreateAssetMenu(fileName = "RunStartConsumableEffect", menuName = "Upgrade/Effect/Run Start Consumable")]
public sealed class RunStartConsumableUpgradeEffectSO : UpgradeEffectSO, IRunStartUpgradeEffect, IUpgradeRuntimeTargetEffect
{
    [SerializeField] private ConsumableDefinition consumable;
    [SerializeField, Min(0)] private int count = 1;

    public override UpgradeEffectKind EffectKind => UpgradeEffectKind.Player;

    public void ApplyAtRunStart(PlayerInteractor2D player)
    {
        UpgradeRuntimeTargetAccumulator accumulator = new UpgradeRuntimeTargetAccumulator();
        AccumulateRuntimeTarget(accumulator);
        accumulator.Apply(player, this);
    }

    public void AccumulateRuntimeTarget(UpgradeRuntimeTargetAccumulator accumulator)
    {
        if (accumulator == null)
            return;

        if (TryGetTarget(out ConsumableDefinition targetConsumable, out int targetCount))
            accumulator.AddMinimumConsumableTarget(targetConsumable, targetCount);
    }

    public bool TryGetTarget(out ConsumableDefinition targetConsumable, out int targetCount)
    {
        targetConsumable = consumable;
        targetCount = Mathf.Max(0, count);
        return targetConsumable != null && targetCount > 0;
    }

    public static bool EnsureConsumableMinimum(
        PlayerInteractor2D player,
        ConsumableDefinition targetConsumable,
        int minimumCount)
    {
        if (player == null || targetConsumable == null || minimumCount <= 0)
            return false;

        PlayerConsumableInventory inventory = PlayerConsumableInventory.GetOrAdd(player.transform);
        if (inventory == null)
            return false;

        inventory.EnsureMinimumConsumableCount(targetConsumable, minimumCount);
        return inventory.CountConsumable(targetConsumable) >= minimumCount;
    }
}

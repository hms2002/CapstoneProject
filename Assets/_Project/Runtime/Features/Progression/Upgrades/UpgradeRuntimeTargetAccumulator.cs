using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public interface IUpgradeRuntimeTargetEffect
{
    void AccumulateRuntimeTarget(UpgradeRuntimeTargetAccumulator accumulator);
}

public sealed class UpgradeRuntimeTargetAccumulator
{
    private readonly Dictionary<AttributeDefinition, float> exactAttributeTargets = new();
    private readonly Dictionary<ConsumableDefinition, int> minimumConsumableTargets = new();

    public bool HasTargets => exactAttributeTargets.Count > 0 || minimumConsumableTargets.Count > 0;

    public void AddExactAttributeTarget(AttributeDefinition attribute, float amount)
    {
        if (attribute == null || amount <= 0f)
            return;

        exactAttributeTargets.TryGetValue(attribute, out float currentAmount);
        exactAttributeTargets[attribute] = currentAmount + Mathf.Max(0f, amount);
    }

    public void AddMinimumConsumableTarget(ConsumableDefinition consumable, int count)
    {
        if (consumable == null || count <= 0)
            return;

        minimumConsumableTargets.TryGetValue(consumable, out int currentCount);
        minimumConsumableTargets[consumable] = currentCount + Mathf.Max(0, count);
    }

    public void Apply(PlayerInteractor2D player, Object source)
    {
        if (player == null || !HasTargets)
            return;

        ApplyExactAttributeTargets(player, source);
        ApplyMinimumConsumableTargets(player);
    }

    private void ApplyExactAttributeTargets(PlayerInteractor2D player, Object source)
    {
        if (exactAttributeTargets.Count == 0)
            return;

        AttributeSet attributeSet = player.GetComponent<AttributeSet>();
        if (attributeSet == null)
            return;

        foreach (KeyValuePair<AttributeDefinition, float> target in exactAttributeTargets)
        {
            attributeSet.TrySetCurrentValue(target.Key, Mathf.Max(0f, target.Value), source);
        }
    }

    private void ApplyMinimumConsumableTargets(PlayerInteractor2D player)
    {
        if (minimumConsumableTargets.Count == 0)
            return;

        PlayerConsumableInventory inventory = PlayerConsumableInventory.GetOrAdd(player.transform);
        if (inventory == null)
            return;

        foreach (KeyValuePair<ConsumableDefinition, int> target in minimumConsumableTargets)
        {
            inventory.EnsureMinimumConsumableCount(target.Key, Mathf.Max(0, target.Value));
        }
    }
}

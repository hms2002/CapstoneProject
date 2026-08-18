using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelRewardEffect_RandomRelicUpgrade", menuName = "Game/Progression/Level Reward Effects/Random Relic Upgrade")]
public sealed class RandomRelicUpgradeLevelRewardEffectSO : LevelRewardEffectSO
{
    [SerializeField, Min(1)] private int relicCount = 3;
    [SerializeField, Min(1)] private int gainedLevels = 2;

    [Serializable]
    private sealed class State { public List<string> upgradedRelicIds = new(); }

    public override LevelRewardEffectLifetime Lifetime => LevelRewardEffectLifetime.InstantOnce;

    public override bool CanApply(LevelRewardEligibilityContext context, out string failureReason)
    {
        if (!base.CanApply(context, out failureReason)) return false;
        RelicInventory inventory = context.Player.GetComponent<RelicInventory>();
        if (inventory == null || !HasUpgradeableRelic(inventory))
        {
            failureReason = "강화 가능한 유물이 없습니다.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public override ILevelRewardEffectHandle Apply(LevelRewardApplyContext context)
    {
        RelicInventory inventory = context.Player != null ? context.Player.GetComponent<RelicInventory>() : null;
        if (inventory == null) return null;

        var upgradeable = new List<RelicDefinition>();
        for (int slot = 0; slot < inventory.Capacity; slot++)
        {
            RelicDefinition relic = inventory.GetRelicInSlot(slot);
            if (relic != null && inventory.GetRelicLevelInSlot(slot) < Mathf.Max(1, relic.maxLevel))
                upgradeable.Add(relic);
        }

        var state = new State();
        var random = new System.Random(RunLevelRewardOffers.GetDeterministicEffectSeed(EffectId));
        int count = Mathf.Min(Mathf.Max(1, relicCount), upgradeable.Count);
        for (int i = 0; i < count; i++)
        {
            int picked = random.Next(i, upgradeable.Count);
            (upgradeable[i], upgradeable[picked]) = (upgradeable[picked], upgradeable[i]);
            RelicDefinition relic = upgradeable[i];
            if (inventory.TryAcquireOrUpgradeDetailed(relic, Mathf.Max(1, gainedLevels)) == RelicInventory.AcquireResult.Success)
                state.upgradedRelicIds.Add(relic.relicId);
        }

        context.EffectState.json = JsonUtility.ToJson(state);
        return null;
    }

    private static bool HasUpgradeableRelic(RelicInventory inventory)
    {
        for (int slot = 0; slot < inventory.Capacity; slot++)
        {
            RelicDefinition relic = inventory.GetRelicInSlot(slot);
            if (relic != null && inventory.GetRelicLevelInSlot(slot) < Mathf.Max(1, relic.maxLevel))
                return true;
        }

        return false;
    }
}

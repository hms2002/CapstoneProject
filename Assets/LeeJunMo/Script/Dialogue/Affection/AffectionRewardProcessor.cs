using System;
using System.Collections.Generic;

public sealed class AffectionRewardProcessor
{
    public bool HasRewardsInRange(NPCData data, int fromLevel, int toLevel)
    {
        if (data == null || data.affectionRewards == null)
            return false;

        foreach (AffectionReward reward in data.affectionRewards)
        {
            if (reward.targetLevel > fromLevel && reward.targetLevel <= toLevel)
                return true;
        }

        return false;
    }

    public void GrantRewards(NPCData data, int fromLevel, int toLevel, Action onComplete)
    {
        List<AffectionEffect> earnedEffects = CollectRewards(data, fromLevel, toLevel);

        if (earnedEffects.Count > 0 && RewardDisplayService.Instance != null)
        {
            RewardDisplayService.Instance.ShowReward(null, earnedEffects, onComplete);
            return;
        }

        onComplete?.Invoke();
    }

    private static List<AffectionEffect> CollectRewards(NPCData data, int fromLevel, int toLevel)
    {
        List<AffectionEffect> earnedEffects = new List<AffectionEffect>();

        if (data == null || data.affectionRewards == null)
            return earnedEffects;

        foreach (AffectionReward reward in data.affectionRewards)
        {
            if (reward.targetLevel <= fromLevel || reward.targetLevel > toLevel)
                continue;

            if (reward.effect == null)
                continue;

            reward.effect.Execute();
            earnedEffects.Add(reward.effect);
        }

        return earnedEffects;
    }
}

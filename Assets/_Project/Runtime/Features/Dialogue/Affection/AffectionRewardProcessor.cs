using System;
using System.Collections.Generic;

// 책임: 호감도 레벨 상승 구간에 해당하는 보상 효과를 수집하고 지급한다.
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

        if (earnedEffects.Count > 0)
        {
            RewardDisplayPlayback.ShowFlowOwnedReward(null, earnedEffects, onComplete);
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

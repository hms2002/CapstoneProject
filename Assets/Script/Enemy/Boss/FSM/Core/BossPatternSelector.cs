using UnityEngine;

public static class BossPatternSelector
{
    public static BossPatternEntry Select(BossControllerBase boss, BossBlackboard blackboard, BossPhaseConfig phaseConfig)
    {
        if (boss == null || blackboard == null || phaseConfig == null || phaseConfig.Patterns == null)
            return null;

        int totalWeight = 0;

        for (int i = 0; i < phaseConfig.Patterns.Count; i++)
        {
            BossPatternEntry patternEntry = phaseConfig.Patterns[i];
            if (patternEntry == null)
                continue;

            if (!patternEntry.IsSelectable(boss, blackboard))
                continue;

            totalWeight += patternEntry.SelectionWeight;
        }

        if (totalWeight <= 0)
            return null;

        int randomWeight = Random.Range(0, totalWeight);

        for (int i = 0; i < phaseConfig.Patterns.Count; i++)
        {
            BossPatternEntry patternEntry = phaseConfig.Patterns[i];
            if (patternEntry == null)
                continue;

            if (!patternEntry.IsSelectable(boss, blackboard))
                continue;

            randomWeight -= patternEntry.SelectionWeight;
            if (randomWeight < 0)
                return patternEntry;
        }

        return null;
    }
}
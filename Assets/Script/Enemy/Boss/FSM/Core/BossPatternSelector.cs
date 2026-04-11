using System.Collections.Generic;
using UnityEngine;

public static class BossPatternSelector
{
    public static BossPatternEntry Select(BossControllerBase boss, BossBlackboard blackboard, BossPhaseConfig phaseConfig)
    {
        if (boss == null || blackboard == null || phaseConfig == null || phaseConfig.Patterns == null) return null;

        int totalWeight = 0;
        List<(BossPatternEntry pattern, int weight)> candidates = new();

        for (int i = 0; i < phaseConfig.Patterns.Count; i++)
        {
            BossPatternEntry patternEntry = phaseConfig.Patterns[i];
            if (patternEntry == null) continue;

            BossPatternEvalResult result = boss.EvaluatePattern(patternEntry);
            int weight = result.GetWeight(patternEntry.SelectionWeight);
            if (weight <= 0) continue;

            totalWeight += weight;
            candidates.Add((patternEntry, weight));
        }

        if (totalWeight <= 0) return null;

        int randomWeight = Random.Range(0, totalWeight);

        for (int i = 0; i < candidates.Count; i++)
        {
            (BossPatternEntry pattern, int weight) candidate = candidates[i];

            randomWeight -= candidate.weight;
            if (randomWeight < 0)
                return candidate.pattern;
        }

        return null;
    }
}

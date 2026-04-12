using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

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
            if (weight <= 0)
            {
                AbilityDefinition ability = patternEntry.Ability;
                string patternName = ability != null ? ability.name : "None";
                Debug.Log(
                    $"[BossFSM] {boss.name}: 패턴 '{patternName}' 후보 탈락. state={result.State}, reason={result.Reason ?? "없음"}",
                    boss);
                continue;
            }

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

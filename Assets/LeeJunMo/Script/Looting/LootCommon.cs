using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemRarity
{
    Common,
    Rare,
    Epic
}

[Serializable]
public struct DropCountOption
{
    public int count;
    public int weight;
}

[Serializable]
public class CountRangeWeightProfile
{
    public int minCount = 1;
    public int maxCount = 1;
    public List<DropCountOption> weights = new List<DropCountOption>();

    public void TryInitializeFromLegacy(int legacyMin, int legacyMax, List<DropCountOption> legacyWeights, int fallbackCount = 0)
    {
        bool hadNoWeights = weights == null || weights.Count == 0;
        bool isDefaultRange = minCount == 1 && maxCount == 1;

        if (hadNoWeights && legacyWeights != null && legacyWeights.Count > 0)
            weights = new List<DropCountOption>(legacyWeights);

        if ((hadNoWeights || isDefaultRange) && legacyMin > 0)
            minCount = legacyMin;

        if ((hadNoWeights || isDefaultRange) && legacyMax > 0)
            maxCount = legacyMax;

        int fallbackMin = fallbackCount > 0 ? fallbackCount : legacyMin;
        int fallbackMax = fallbackCount > 0 ? fallbackCount : legacyMax;
        EnsureDefaults(fallbackMin, fallbackMax);
    }

    public void EnsureDefaults(int fallbackMin = 1, int fallbackMax = 1)
    {
        if (weights != null && weights.Count > 0)
        {
            int optionMin = int.MaxValue;
            int optionMax = int.MinValue;

            foreach (DropCountOption option in weights)
            {
                optionMin = Mathf.Min(optionMin, option.count);
                optionMax = Mathf.Max(optionMax, option.count);
            }

            if (minCount <= 0)
                minCount = optionMin;

            if (maxCount <= 0)
                maxCount = optionMax;
        }
        else
        {
            if (minCount <= 0)
                minCount = fallbackMin;

            if (maxCount <= 0)
                maxCount = fallbackMax;

            int fallbackCount = Mathf.Max(0, minCount);
            weights = new List<DropCountOption>
            {
                new DropCountOption { count = fallbackCount, weight = 100 }
            };
        }

        if (minCount < 0)
            minCount = 0;

        if (maxCount < minCount)
            maxCount = minCount;
    }
}

[Serializable]
public struct BossSpecificLoot
{
    public ScriptableObject item;
    [Range(0, 100)] public int dropChance;
}

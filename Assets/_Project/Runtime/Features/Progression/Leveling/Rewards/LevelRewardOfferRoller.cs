using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Deterministic three-card composition, stat-family novelty, and 70/25/5 tiers.</summary>
public static class LevelRewardOfferRoller
{
    private const int CardCount = 3;

    public static string Kind(LevelRewardDefinitionSO definition)
    {
        var stat = StatIncreaseLevelRewardEffectSO.Find(definition);
        return stat != null ? "stat:" + (int)stat.Stat : definition?.RewardId;
    }

    public static bool Roll(
        IReadOnlyList<LevelRewardDefinitionSO> eligible,
        ISet<string> previousKinds,
        Random random,
        List<string> destination)
    {
        destination.Clear();
        var families = new SortedDictionary<string, List<LevelRewardDefinitionSO>>(StringComparer.Ordinal);
        var special = new List<LevelRewardDefinitionSO>();
        foreach (var definition in eligible)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.RewardId)) continue;
            var stat = StatIncreaseLevelRewardEffectSO.Find(definition);
            if (stat == null) special.Add(definition);
            else
            {
                string kind = Kind(definition);
                if (!families.TryGetValue(kind, out var variants))
                    families.Add(kind, variants = new List<LevelRewardDefinitionSO>());
                variants.Add(definition);
            }
        }

        int statCount = Math.Min(families.Count, random.Next(100) < 60 ? 2 : 1);
        var chosenKinds = new HashSet<string>(StringComparer.Ordinal);
        AppendStats(families, previousKinds, chosenKinds, random, destination, statCount);

        Shuffle(special, random);
        int specialTarget = CardCount - destination.Count;
        AppendSpecial(special, previousKinds, chosenKinds, destination, specialTarget, false);
        AppendSpecial(special, previousKinds, chosenKinds, destination, CardCount - destination.Count, true);

        // Exhausted one-shot rewards leave their places to additional distinct stat families.
        AppendStats(families, previousKinds, chosenKinds, random, destination, CardCount - destination.Count);
        Shuffle(destination, random);
        return destination.Count > 0;
    }

    private static void AppendStats(
        SortedDictionary<string, List<LevelRewardDefinitionSO>> families,
        ISet<string> previousKinds,
        HashSet<string> chosenKinds,
        Random random,
        List<string> destination,
        int count)
    {
        for (int slot = 0; slot < count && destination.Count < CardCount; slot++)
        {
            var available = families.Where(pair => !chosenKinds.Contains(pair.Key)).ToList();
            if (available.Count == 0) return;
            var novel = available.Where(pair => !previousKinds.Contains(pair.Key)).ToList();
            if (novel.Count > 0) available = novel;

            // Eight ordinary stat groups and one element group have equal starting weight.
            // The remaining elements in that group are chosen uniformly.
            var groups = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var pair in available)
            {
                string group = StatIncreaseLevelRewardEffectSO.Find(pair.Value[0]).IsElement ? "elements" : pair.Key;
                if (!groups.TryGetValue(group, out var kinds)) groups.Add(group, kinds = new List<string>());
                kinds.Add(pair.Key);
            }
            var groupKinds = groups.Values.ElementAt(random.Next(groups.Count));
            string selectedKind = groupKinds[random.Next(groupKinds.Count)];
            var variants = families[selectedKind];
            int roll = random.Next(variants.Sum(definition => StatIncreaseLevelRewardEffectSO.Find(definition).TierWeight));
            foreach (var variant in variants)
            {
                roll -= StatIncreaseLevelRewardEffectSO.Find(variant).TierWeight;
                if (roll >= 0) continue;
                destination.Add(variant.RewardId);
                chosenKinds.Add(selectedKind);
                break;
            }
        }
    }

    private static void AppendSpecial(
        List<LevelRewardDefinitionSO> special,
        ISet<string> previousKinds,
        HashSet<string> chosenKinds,
        List<string> destination,
        int count,
        bool includePrevious)
    {
        int added = 0;
        foreach (var definition in special)
        {
            if (added >= count || destination.Count >= CardCount) break;
            string kind = Kind(definition);
            if (chosenKinds.Contains(kind) || previousKinds.Contains(kind) != includePrevious) continue;
            destination.Add(definition.RewardId);
            chosenKinds.Add(kind);
            added++;
        }
    }

    private static void Shuffle<T>(List<T> items, Random random)
    {
        for (int i = 0; i < items.Count; i++)
        {
            int selected = random.Next(i, items.Count);
            (items[i], items[selected]) = (items[selected], items[i]);
        }
    }
}

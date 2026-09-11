using System;
using System.Collections.Generic;

/// <summary>
/// Selects at most the configured candidate count using an isolated seed, or restores
/// exact saved presence decisions without filling newly empty or missing positions.
/// </summary>
public static class ChestPossibleSelection
{
    public static HashSet<string> Select(
        IReadOnlyList<string> candidateIds, int maximumCount, int seed,
        IReadOnlyList<DungeonObjectRuntimeStateData> savedStates = null)
    {
        var selected = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < candidateIds.Count; i++)
        {
            string id = candidateIds[i];
            if (!string.IsNullOrWhiteSpace(id) && seen.Add(id))
                candidates.Add(id);
        }

        if (savedStates != null)
        {
            for (int i = 0; i < savedStates.Count; i++)
            {
                DungeonObjectRuntimeStateData state = savedStates[i];
                if (state != null && state.isPresent && seen.Contains(state.stateId))
                    selected.Add(state.stateId);
            }
            return selected;
        }

        candidates.Sort(StringComparer.Ordinal);
        var random = new Random(unchecked(seed ^ 0x4C315A71));
        int count = Math.Min(Math.Max(0, maximumCount), candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int index = random.Next(i, candidates.Count);
            (candidates[i], candidates[index]) = (candidates[index], candidates[i]);
            selected.Add(candidates[i]);
        }
        return selected;
    }
}

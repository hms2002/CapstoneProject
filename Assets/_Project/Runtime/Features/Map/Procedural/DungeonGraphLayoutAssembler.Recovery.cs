using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns bounded layout recovery without dropping required rooms or mutating authoring assets.
/// Every recovery result still passes the normal template, quota and physical-placement checks.
/// </summary>
public sealed partial class DungeonGraphLayoutAssembler
{
    public DungeonLayoutResult Assemble(
        RoomThemeLibrarySO library, DungeonLayoutPolicySO policy, int seed,
        int requestedRoomCount, int maxPlacementAttemptsPerRoom, int minimumCorridorLength,
        float corridorLengthPerRoomCell, int corridorLengthVariation,
        IReadOnlyList<RoomTemplateSO> guaranteedRoomTemplates = null,
        IReadOnlyList<RequiredCombatRoomRule> requiredCombatRoomRules = null,
        int generationStage = 0, bool allowRecovery = true)
    {
        DungeonLayoutResult Build(DungeonLayoutPolicySO activePolicy, int level) => AssembleCore(
            library, activePolicy, seed, requestedRoomCount, maxPlacementAttemptsPerRoom,
            minimumCorridorLength, corridorLengthPerRoomCell, corridorLengthVariation,
            guaranteedRoomTemplates, requiredCombatRoomRules, generationStage, level);

        var result = Build(policy, 0);
        // Missing assets and contradictory quotas are authoring errors, not placement preferences.
        if (result.IsComplete || !allowRecovery ||
            !result.FailureReason.StartsWith("Graph-first layout failed")) return result;

        string strictFailure = result.FailureReason;
        result = Build(policy, 1);
        if (result.IsComplete)
        {
            result.SetRecovery(1, "Event minimum distance capped at 2; required rooms and dead ends preserved. " + strictFailure);
            return result;
        }

        var recoveryPolicy = policy.CreateRecoveryPolicy();
        try
        {
            result = Build(recoveryPolicy, 2);
            result.SetRecovery(2,
                "Tree fallback: cycles removed; event minimum distance capped at 1 and cycle preference relaxed. " +
                "Required rooms, dead ends, Boss distance, room count and combat quotas preserved. " + strictFailure);
            return result;
        }
        finally
        {
            if (Application.isPlaying) Object.Destroy(recoveryPolicy);
            else Object.DestroyImmediate(recoveryPolicy);
        }
    }

    private static int ResolvePlacementMinimumDistance(TopologyDraft topology, RoomTemplateSO template)
    {
        int distance = Mathf.Max(0, template.LayoutData.topologyPlacement.minimumGraphDistanceFromStart);
        if (topology.RecoveryLevel == 0 || template.LayoutData.roomType != RoomType.Event) return distance;
        return Mathf.Min(distance, topology.RecoveryLevel == 1 ? 2 : 1);
    }

    private static bool RequiresCyclePlacement(TopologyDraft topology, RoomTemplateSO template) =>
        template.LayoutData.topologyPlacement.mode == RoomTopologyPlacementMode.CycleDetour &&
        (topology.RecoveryLevel < 2 || template.LayoutData.roomType != RoomType.Event);

    private static bool TryReserveGuaranteedRooms(TopologyDraft topology, List<RoomTemplateSO> pending,
        List<int> assigned, bool preferDeadEnds, System.Random random, ref int budget, out string failure)
    {
        failure = string.Empty;
        if (pending.Count == 0) return true;
        if (--budget < 0) { failure = "Required-room reservation search budget exhausted."; return false; }

        int selected = 0, fewest = int.MaxValue;
        var compatible = new List<int>();
        var deadEnds = new List<int>();
        for (int i = 0; i < pending.Count; i++)
        {
            CollectGuaranteedTemplateNodes(topology, pending[i], assigned, compatible, deadEnds);
            if (compatible.Count == 0)
            {
                failure = $"No unassigned topology node can use guaranteed template '{pending[i].name}'.";
                return false;
            }
            if (compatible.Count < fewest) { selected = i; fewest = compatible.Count; }
        }

        var template = pending[selected];
        CollectGuaranteedTemplateNodes(topology, template, assigned, compatible, deadEnds);
        pending.RemoveAt(selected);
        while (compatible.Count > 0 && budget > 0)
        {
            var preferred = preferDeadEnds && deadEnds.Count > 0 ? deadEnds : compatible;
            int index = SelectGuaranteedNode(topology, preferred, assigned,
                template.LayoutData.topologyPlacement.mode, random);
            compatible.Remove(index);
            deadEnds.Remove(index);
            var node = topology.Nodes[index];
            var oldTemplate = node.Template;
            var oldRole = node.Role;
            node.Template = template;
            node.Role = template.LayoutData.roomType;
            assigned.Add(index);
            if (TryReserveGuaranteedRooms(topology, pending, assigned, preferDeadEnds, random, ref budget, out failure))
                return true;
            assigned.RemoveAt(assigned.Count - 1);
            node.Template = oldTemplate;
            node.Role = oldRole;
        }
        pending.Insert(selected, template);
        if (string.IsNullOrEmpty(failure)) failure = $"Required-room reservation exhausted for '{template.name}'.";
        return false;
    }
}

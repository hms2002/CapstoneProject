using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Assigns room templates with bounded backtracking while preserving topology, reserved roles and physical placement algorithms.</summary>
public sealed partial class DungeonGraphLayoutAssembler
{
    private const int TemplateSearchStepsPerPhase = 1024;
    private const int TemplatePhysicalAttemptsPerPhase = 16;
    private const int TemplatePhysicalCandidatesPerPhase = 4;

    private static bool TrySelectAndPlaceTemplates(
        RoomThemeLibrarySO library, DungeonLayoutPolicySO policy,
        IReadOnlyList<RoomTemplateSO> guaranteed, IReadOnlyList<RequiredCombatRoomRule> rules,
        TopologyDraft topology, System.Random random, int seed, int roomCount,
        int minimumLength, float lengthRatio, int lengthVariation,
        out DungeonLayoutResult result, out string failure)
    {
        var search = new TemplateSearch(library, policy, guaranteed, rules, topology, random,
            seed, roomCount, minimumLength, lengthRatio, lengthVariation);
        return search.TrySolve(out result, out failure);
    }

    /// <summary>Caches one node's compatible weighted template and quota memberships for forward checking.</summary>
    private sealed class TemplateCandidate
    {
        public RoomTemplateSO Template;
        public int Id;
        public double Weight;
        public bool Large;
        public bool[] Rules;
    }

    /// <summary>Owns only one topology attempt's candidate domains, undoable assignments, deterministic budgets and valid-result comparison.</summary>
    private sealed class TemplateSearch
    {
        private readonly DungeonLayoutPolicySO policy;
        private readonly TopologyDraft topology;
        private readonly System.Random random;
        private readonly int seed, roomCount, minimumLength, lengthVariation;
        private readonly float lengthRatio;
        private readonly List<RequiredCombatRoomRule> rules = new();
        private readonly List<TemplateCandidate>[] domains;
        private readonly List<RoomSocketDirection>[] directions;
        private readonly TemplateCandidate[] assigned;
        private readonly bool[] pinned;
        private readonly int[,] distances;
        private readonly byte[,] repeats;
        private readonly double[] tieBreaks;
        private readonly StringBuilder history = new();
        private string failure = "No physically valid template assignment was found.";
        private int phase, stepsLeft, totalSteps, physicalAttempts, physicalCandidates;
        private DungeonLayoutResult best;
        private int bestOverrun, bestLongest;

        public TemplateSearch(RoomThemeLibrarySO library, DungeonLayoutPolicySO policy,
            IReadOnlyList<RoomTemplateSO> guaranteed, IReadOnlyList<RequiredCombatRoomRule> requiredRules,
            TopologyDraft topology, System.Random random, int seed, int roomCount,
            int minimumLength, float lengthRatio, int lengthVariation)
        {
            this.policy = policy; this.topology = topology; this.random = random;
            this.seed = seed; this.roomCount = roomCount; this.minimumLength = minimumLength;
            this.lengthRatio = lengthRatio; this.lengthVariation = lengthVariation;
            if (requiredRules != null)
                foreach (var rule in requiredRules) if (rule != null && rule.Count > 0) rules.Add(rule);
            int count = topology.Nodes.Count;
            domains = new List<TemplateCandidate>[count];
            directions = new List<RoomSocketDirection>[count];
            assigned = new TemplateCandidate[count]; pinned = new bool[count];
            tieBreaks = new double[count]; distances = new int[count, count];
            var unique = new List<RoomTemplateSO>();
            for (int i = 0; i < count; i++)
            {
                PlannedNode node = topology.Nodes[i];
                directions[i] = new List<RoomSocketDirection>();
                CollectRequiredDirections(topology, i, directions[i]);
                pinned[i] = node.Template != null && ContainsTemplateReference(guaranteed, node.Template);
                var candidates = new List<RoomTemplateSO>();
                if (pinned[i]) candidates.Add(node.Template);
                else library.CollectRooms(node.Role, candidates);
                domains[i] = new List<TemplateCandidate>();
                foreach (RoomTemplateSO template in candidates)
                {
                    if (template == null || template.LayoutData.roomType != node.Role ||
                        !IsTemplateCompatible(template, directions[i]) ||
                        (!pinned[i] && ContainsTemplateReference(guaranteed, template)) ||
                        !SupportsPlacement(template, i)) continue;
                    bool duplicate = domains[i].Exists(c => c.Template == template);
                    if (duplicate) continue;
                    int id = unique.IndexOf(template);
                    if (id < 0) { id = unique.Count; unique.Add(template); }
                    var matches = new bool[rules.Count];
                    for (int r = 0; r < rules.Count; r++) matches[r] = rules[r].Matches(template);
                    domains[i].Add(new TemplateCandidate { Template = template, Id = id,
                        Weight = CalculateSocketFitAdjustedWeight(template, directions[i], policy),
                        Large = IsLargeCombatRoom(template), Rules = matches });
                }
                tieBreaks[i] = random.NextDouble();
                for (int j = 0; j < count; j++) distances[i, j] = CalculateGraphDistance(topology, i, j);
            }
            repeats = new byte[unique.Count, unique.Count];
            for (int a = 0; a < unique.Count; a++)
                for (int b = 0; b < unique.Count; b++)
                    repeats[a, b] = (byte)(IsSameRoomTemplate(unique[a], unique[b]) ? 2 :
                        RoomTemplateShapeUtility.IsSameShape(unique[a], unique[b]) ? 1 : 0);
        }

        private bool SupportsPlacement(RoomTemplateSO template, int node)
        {
            var placement = template.LayoutData.topologyPlacement;
            return CalculateGraphDistance(topology, 0, node) >= Mathf.Max(0, placement.minimumGraphDistanceFromStart) &&
                (!placement.requireDeadEnd || GetNodeDegree(topology, node) == 1) &&
                (placement.mode != RoomTopologyPlacementMode.CycleDetour || topology.Nodes[node].IsCycleDetour);
        }

        public bool TrySolve(out DungeonLayoutResult result, out string reason)
        {
            for (int i = 0; i < domains.Length; i++)
                if (domains[i].Count == 0)
                {
                    result = null;
                    reason = $"Node {i} ({topology.Nodes[i].Role}) has no hard-valid template for [{FormatDirections(directions[i])}].";
                    return false;
                }
            for (phase = 0; phase <= 3; phase++)
            {
                Array.Clear(assigned, 0, assigned.Length);
                stepsLeft = TemplateSearchStepsPerPhase;
                physicalAttempts = 0; physicalCandidates = 0;
                Search(0);
                string stop = stepsLeft <= 0 ? "node budget reached" :
                    physicalAttempts >= TemplatePhysicalAttemptsPerPhase ? "physical-attempt budget reached" :
                    physicalCandidates >= TemplatePhysicalCandidatesPerPhase ? "valid-candidate budget reached" :
                    best != null ? "valid assignment found" : "domain search exhausted (including physical validation)";
                history.AppendLine($"Phase {phase}: {stop}; steps={TemplateSearchStepsPerPhase - stepsLeft}, physical={physicalAttempts}, valid={physicalCandidates}.");
                if (best == null) continue;
                var details = new StringBuilder();
                details.AppendLine(best.TemplateSelection.Metrics.ToString());
                details.Append(history);
                AppendRepeatReasons(best, details);
                best.SetTemplateSelection(new DungeonTemplateSelectionReport(best.TemplateSelection.Metrics, phase, totalSteps, details.ToString()));
                result = best; reason = string.Empty;
                return true;
            }
            result = null;
            reason = $"Template search failed without relaxing hard constraints. {failure}\n{history}";
            return false;
        }

        private bool Search(int assignedCount)
        {
            if (stepsLeft <= 0 || physicalAttempts >= TemplatePhysicalAttemptsPerPhase ||
                physicalCandidates >= TemplatePhysicalCandidatesPerPhase) return true;
            if (!ForwardCheck(out int node, out List<TemplateCandidate> options)) return false;
            if (assignedCount == assigned.Length) return TryPhysicalCandidate();
            var order = new List<(TemplateCandidate Candidate, int Bucket, double Key)>();
            foreach (var option in options)
            {
                // Exponential weighted sampling without replacement, strictly within repetition tiers.
                double key = -Math.Log(Math.Max(double.Epsilon, random.NextDouble())) / option.Weight;
                order.Add((option, RepeatBucket(node, option), key));
            }
            order.Sort((a, b) => { int c = a.Bucket.CompareTo(b.Bucket); return c != 0 ? c : a.Key.CompareTo(b.Key); });
            foreach (var item in order)
            {
                if (stepsLeft-- <= 0) { stepsLeft = 0; return true; }
                totalSteps++;
                assigned[node] = item.Candidate;
                bool stop = Search(assignedCount + 1);
                assigned[node] = null;
                if (stop) return true;
            }
            return false;
        }

        private bool ForwardCheck(out int chosen, out List<TemplateCandidate> chosenOptions)
        {
            chosen = -1; chosenOptions = null;
            int usedLarge = 0;
            var used = new int[rules.Count];
            foreach (var choice in assigned)
                if (choice != null)
                {
                    if (choice.Large) usedLarge++;
                    for (int r = 0; r < rules.Count; r++) if (choice.Rules[r]) used[r]++;
                }
            if (usedLarge > policy.MaximumLargeCombatRoomCount) return false;
            for (int r = 0; r < rules.Count; r++) if (used[r] > rules[r].Count) return false;
            int mandatoryLarge = usedLarge;
            var lower = (int[])used.Clone(); var upper = (int[])used.Clone();
            for (int i = 0; i < domains.Length; i++)
            {
                if (assigned[i] != null) continue;
                var viable = new List<TemplateCandidate>();
                foreach (var candidate in domains[i])
                {
                    if (candidate.Large && usedLarge >= policy.MaximumLargeCombatRoomCount) continue;
                    bool capped = false;
                    for (int r = 0; r < rules.Count; r++)
                        if (candidate.Rules[r] && used[r] >= rules[r].Count) { capped = true; break; }
                    if (!capped && RepeatBucket(i, candidate) <= phase) viable.Add(candidate);
                }
                if (viable.Count == 0) return false;
                if (viable.TrueForAll(c => c.Large)) mandatoryLarge++;
                for (int r = 0; r < rules.Count; r++)
                {
                    bool any = false, all = true;
                    foreach (var candidate in viable) { any |= candidate.Rules[r]; all &= candidate.Rules[r]; }
                    if (any) upper[r]++;
                    if (all) lower[r]++;
                }
                if (chosen < 0 || viable.Count < chosenOptions.Count ||
                    viable.Count == chosenOptions.Count && (directions[i].Count > directions[chosen].Count ||
                    directions[i].Count == directions[chosen].Count && tieBreaks[i] < tieBreaks[chosen]))
                { chosen = i; chosenOptions = viable; }
            }
            if (mandatoryLarge > policy.MaximumLargeCombatRoomCount) return false;
            for (int r = 0; r < rules.Count; r++)
                if (lower[r] > rules[r].Count || upper[r] < rules[r].Count) return false;
            return true;
        }

        private int RepeatBucket(int node, TemplateCandidate candidate)
        {
            int bucket = 0;
            for (int other = 0; other < assigned.Length; other++)
            {
                if (other == node || assigned[other] == null) continue;
                int distance = distances[node, other];
                if (distance <= 0 || distance > policy.TemplateRepeatAvoidanceGraphDistance) continue;
                byte kind = repeats[candidate.Id, assigned[other].Id];
                if (kind == 0) continue;
                bucket = Math.Max(bucket, distance > 1 ? 1 : kind == 1 ? 2 : 3);
            }
            return bucket;
        }

        private bool TryPhysicalCandidate()
        {
            physicalAttempts++;
            for (int i = 0; i < assigned.Length; i++)
            {
                PlannedNode node = topology.Nodes[i];
                node.Template = assigned[i].Template;
                node.LocalBounds = ResolveLocalBounds(node.Template.LayoutData);
                node.SocketIndices.Clear();
                if (!TrySelectSocketIndices(node.Template.LayoutData, directions[i], random, node.SocketIndices) ||
                    !TryResolveNodeReferences(node, directions[i], out failure)) return false;
            }
            if (!TryCreatePhysicalLayout(seed, roomCount, topology, random, minimumLength, lengthRatio,
                lengthVariation, out DungeonLayoutResult result, out failure) || !ValidateFinalAssignment(result)) return false;
            physicalCandidates++;
            DungeonTemplateRepetitionMetrics metrics = MeasureRepetitions(result, policy.TemplateRepeatAvoidanceGraphDistance);
            result.SetTemplateSelection(new DungeonTemplateSelectionReport(metrics, phase, totalSteps, string.Empty));
            int overrun = CalculateMaximumCorridorPreferenceOverrun(result, minimumLength, lengthRatio, lengthVariation, out int longest);
            if (best == null || metrics.CompareTo(best.TemplateSelection.Metrics) < 0 ||
                metrics.CompareTo(best.TemplateSelection.Metrics) == 0 && (overrun < bestOverrun || overrun == bestOverrun && longest < bestLongest))
            { best = result; bestOverrun = overrun; bestLongest = longest; }
            return metrics.IsRepeatFree && overrun <= Math.Max(2, minimumLength + lengthVariation);
        }

        private bool ValidateFinalAssignment(DungeonLayoutResult result)
        {
            if (result.Rooms.Count != domains.Length || result.Connections.Count != topology.Edges.Count) return false;
            int large = 0;
            var counts = new int[rules.Count];
            for (int i = 0; i < result.Rooms.Count; i++)
            {
                RoomTemplateSO template = result.Rooms[i].Template;
                if (template != assigned[i].Template || template.LayoutData.roomType != topology.Nodes[i].Role ||
                    !IsTemplateCompatible(template, directions[i]) || !SupportsPlacement(template, i)) return false;
                if (pinned[i] && template != domains[i][0].Template) return false;
                if (IsLargeCombatRoom(template)) large++;
                for (int r = 0; r < rules.Count; r++) if (rules[r].Matches(template)) counts[r]++;
            }
            if (large > policy.MaximumLargeCombatRoomCount) return false;
            for (int r = 0; r < rules.Count; r++) if (counts[r] != rules[r].Count) return false;
            return true;
        }

        private void AppendRepeatReasons(DungeonLayoutResult result, StringBuilder details)
        {
            int[,] actualDistances = BuildPlacedDistances(result);
            for (int i = 0; i < result.Rooms.Count; i++)
                for (int j = i + 1; j < result.Rooms.Count; j++)
                {
                    int distance = actualDistances[i, j];
                    if (distance < 1 || distance > policy.TemplateRepeatAvoidanceGraphDistance) continue;
                    RoomTemplateSO a = result.Rooms[i].Template, b = result.Rooms[j].Template;
                    bool same = IsSameRoomTemplate(a, b);
                    if (!same && !RoomTemplateShapeUtility.IsSameShape(a, b)) continue;
                    bool pairAlternative = false;
                    foreach (var first in domains[i])
                        foreach (var second in domains[j])
                            if (same ? repeats[first.Id, second.Id] != 2 : repeats[first.Id, second.Id] == 0) pairAlternative = true;
                    string reason = pinned[i] && pinned[j] ? "both templates are guaranteed" :
                        !pairAlternative ? "no non-repeating pair in these nodes' hard-valid domains" :
                        "bounded search retained this pair under coupled constraints/physical fit; not proof that repetition is unavoidable";
                    details.AppendLine($"#{result.Rooms[i].PlacementId} {a.LayoutData.roomId} <-> #{result.Rooms[j].PlacementId} {b.LayoutData.roomId}: distance={distance}, {(same ? "same room" : "same shape")}; {reason}.");
                }
        }
    }

    private static DungeonTemplateRepetitionMetrics MeasureRepetitions(DungeonLayoutResult result, int maxDistance)
    {
        int[,] distances = BuildPlacedDistances(result);
        int adjacentTemplates = 0, adjacentShapes = 0, nearbyTemplates = 0, nearbyShapes = 0;
        for (int i = 0; i < result.Rooms.Count; i++)
            for (int j = i + 1; j < result.Rooms.Count; j++)
            {
                int distance = distances[i, j];
                if (distance < 1 || distance > maxDistance) continue;
                bool same = IsSameRoomTemplate(result.Rooms[i].Template, result.Rooms[j].Template);
                bool shape = same || RoomTemplateShapeUtility.IsSameShape(result.Rooms[i].Template, result.Rooms[j].Template);
                if (distance == 1) { if (same) adjacentTemplates++; if (shape) adjacentShapes++; }
                else { if (same) nearbyTemplates++; if (shape) nearbyShapes++; }
            }
        return new DungeonTemplateRepetitionMetrics(adjacentTemplates, adjacentShapes, nearbyTemplates, nearbyShapes);
    }

    private static int[,] BuildPlacedDistances(DungeonLayoutResult result)
    {
        int count = result.Rooms.Count;
        var indices = new Dictionary<int, int>();
        var distances = new int[count, count];
        for (int i = 0; i < count; i++)
        {
            indices.Add(result.Rooms[i].PlacementId, i);
            for (int j = 0; j < count; j++) distances[i, j] = i == j ? 0 : count + 1;
        }
        foreach (var edge in result.Connections)
        {
            int a = indices[edge.FirstRoomPlacementId], b = indices[edge.SecondRoomPlacementId];
            distances[a, b] = distances[b, a] = 1;
        }
        for (int k = 0; k < count; k++)
            for (int i = 0; i < count; i++)
                for (int j = 0; j < count; j++) distances[i, j] = Math.Min(distances[i, j], distances[i, k] + distances[k, j]);
        return distances;
    }
}

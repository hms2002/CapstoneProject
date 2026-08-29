using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 탐색 정책에 맞는 연결 그래프를 먼저 만든 뒤 노드 역할, 방 템플릿, 소켓과 월드 배치를 순서대로 결정한다.
/// - 보스 거리, 의미 있는 분기, 순환 연결, 필수 방 역할과 물리적 비겹침을 하나의 완성 결과로 검증한다.
/// - 테마, 타일, 몬스터 구현을 알지 않고 RoomThemeLibrarySO의 레이아웃 데이터만 소비한다.
/// </summary>
public sealed class DungeonGraphLayoutAssembler
{
    private static readonly RoomSocketDirection[] AllDirections =
    {
        RoomSocketDirection.Up,
        RoomSocketDirection.Right,
        RoomSocketDirection.Down,
        RoomSocketDirection.Left
    };

    /// <summary>
    /// 책임:
    /// - 그래프 노드 하나의 논리 좌표, 역할, 선택된 방/소켓과 물리 배치 중간값을 보관한다.
    /// </summary>
    private sealed class PlannedNode
    {
        public Vector2Int GridPosition;
        public bool IsMainPath;
        public int BranchGroup = -1;
        public RoomType Role = RoomType.Combat;
        public RoomTemplateSO Template;
        public readonly Dictionary<RoomSocketDirection, int> SocketIndices = new();
        public RectInt LocalBounds;
        public int ReferenceX;
        public int ReferenceY;
        public Vector2Int Origin;
        public RectInt WorldBounds;
    }

    /// <summary>
    /// 책임:
    /// - 그래프에서 두 노드가 연결된다는 사실과 순환로를 위해 추가된 간선인지 여부를 보관한다.
    /// </summary>
    private readonly struct PlannedEdge
    {
        public int FirstNodeIndex { get; }
        public int SecondNodeIndex { get; }
        public bool IsCycleEdge { get; }

        public PlannedEdge(int firstNodeIndex, int secondNodeIndex, bool isCycleEdge)
        {
            FirstNodeIndex = firstNodeIndex;
            SecondNodeIndex = secondNodeIndex;
            IsCycleEdge = isCycleEdge;
        }
    }

    /// <summary>
    /// 책임:
    /// - 한 번의 그래프 생성 시도에서 노드, 간선, 주 경로와 기획 지표를 함께 추적한다.
    /// </summary>
    private sealed class TopologyDraft
    {
        public readonly List<PlannedNode> Nodes = new();
        public readonly List<PlannedEdge> Edges = new();
        public readonly List<int> MainPathNodeIndices = new();
        public int BossNodeIndex;
        public int RequestedBossDistance;
        public int MeaningfulBranchCount;
        public int CycleConnectionCount;
    }

    public DungeonLayoutResult Assemble(
        RoomThemeLibrarySO library,
        DungeonLayoutPolicySO policy,
        int seed,
        int requestedRoomCount,
        int maxPlacementAttemptsPerRoom,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation)
    {
        int roomCount = Mathf.Max(2, requestedRoomCount);
        DungeonLayoutResult failedResult = new(seed, roomCount);
        if (library == null)
        {
            failedResult.MarkFailed("Room theme library is missing.");
            return failedResult;
        }

        if (policy == null)
        {
            failedResult.MarkFailed("Graph-first layout requires a DungeonLayoutPolicySO.");
            return failedResult;
        }

        if (!ValidateRoleBudget(policy, roomCount, out string budgetFailure))
        {
            failedResult.MarkFailed(budgetFailure);
            return failedResult;
        }

        List<RoomSocketDirection> allowedBossMovementDirections = new();
        CollectAllowedBossMovementDirections(library, allowedBossMovementDirections);
        if (allowedBossMovementDirections.Count == 0)
        {
            failedResult.MarkFailed("The room library has no usable Boss socket for graph-first placement.");
            return failedResult;
        }

        int attemptCount = Mathf.Max(
            policy.MaximumTopologyAttempts,
            Mathf.Clamp(maxPlacementAttemptsPerRoom, 1, 4096));
        int resolvedMinimumCorridorLength = Mathf.Max(0, minimumCorridorLength);
        float resolvedCorridorLengthPerRoomCell = float.IsFinite(corridorLengthPerRoomCell)
            ? Mathf.Clamp01(corridorLengthPerRoomCell)
            : 0f;
        int resolvedCorridorLengthVariation = Mathf.Clamp(corridorLengthVariation, 0, 32);
        string lastFailure = "No graph-first layout attempt was completed.";

        for (int attempt = 0; attempt < attemptCount; attempt++)
        {
            int attemptSeed = unchecked(seed + attempt * 486187739);
            System.Random random = new(attemptSeed);
            if (!TryCreateTopology(
                    policy,
                    roomCount,
                    allowedBossMovementDirections,
                    random,
                    out TopologyDraft topology,
                    out lastFailure))
            {
                continue;
            }

            ReorderBossLast(topology);
            if (!TryAssignRoomRoles(library, policy, topology, random, out lastFailure) ||
                !TrySelectTemplatesAndSockets(library, topology, random, out lastFailure) ||
                !TryCreatePhysicalLayout(
                    seed,
                    roomCount,
                    topology,
                    random,
                    resolvedMinimumCorridorLength,
                    resolvedCorridorLengthPerRoomCell,
                    resolvedCorridorLengthVariation,
                    out DungeonLayoutResult result,
                    out lastFailure))
            {
                continue;
            }

            int actualBossDistance = CalculateGraphDistance(
                topology,
                0,
                topology.BossNodeIndex);
            if (actualBossDistance < policy.MinimumBossGraphDistance ||
                actualBossDistance > policy.MaximumBossGraphDistance)
            {
                lastFailure =
                    $"Boss graph distance fell outside policy after cycle placement. " +
                    $"Distance={actualBossDistance}, " +
                    $"Expected={policy.MinimumBossGraphDistance}..{policy.MaximumBossGraphDistance}.";
                continue;
            }

            int deadEndCount = CountExplorationDeadEnds(topology);
            result.SetTopologyMetrics(
                actualBossDistance,
                topology.MeaningfulBranchCount,
                topology.CycleConnectionCount,
                deadEndCount);
            result.MarkComplete();
            return result;
        }

        failedResult.MarkFailed(
            $"Graph-first layout failed after {attemptCount} attempts. {lastFailure}");
        return failedResult;
    }

    private static bool ValidateRoleBudget(
        DungeonLayoutPolicySO policy,
        int roomCount,
        out string failure)
    {
        int specialRoomCount = policy.TreasureRoomCount +
            policy.EventRoomCount +
            policy.ShopRoomCount;
        int requiredRoomCount = 2 + specialRoomCount + policy.MinimumCombatRoomCount;
        if (requiredRoomCount > roomCount)
        {
            failure =
                $"Layout policy requires at least {requiredRoomCount} rooms for Start, Boss, " +
                $"special roles and Combat quota, but only {roomCount} were requested.";
            return false;
        }

        int minimumTopologyExtras =
            policy.MinimumMeaningfulBranches + policy.MinimumCycleConnections * 2;
        int maximumBossDistanceAllowedByBudget = roomCount - 1 - minimumTopologyExtras;
        if (maximumBossDistanceAllowedByBudget < policy.MinimumBossGraphDistance)
        {
            failure =
                $"Layout policy cannot fit its minimum boss distance, branches and cycles into {roomCount} rooms. " +
                $"Required boss distance={policy.MinimumBossGraphDistance}, " +
                $"Branches={policy.MinimumMeaningfulBranches}, Cycles={policy.MinimumCycleConnections}.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool TryCreateTopology(
        DungeonLayoutPolicySO policy,
        int roomCount,
        IReadOnlyList<RoomSocketDirection> allowedBossMovementDirections,
        System.Random random,
        out TopologyDraft topology,
        out string failure)
    {
        topology = null;
        if (!TryResolveTopologyCounts(
                policy,
                roomCount,
                random,
                out int bossDistance,
                out int branchCount,
                out int cycleCount,
                out failure))
        {
            return false;
        }

        TopologyDraft draft = new()
        {
            RequestedBossDistance = bossDistance,
            MeaningfulBranchCount = branchCount,
            CycleConnectionCount = cycleCount
        };
        if (!TryBuildMainPath(
                draft,
                bossDistance,
                allowedBossMovementDirections,
                random))
        {
            failure = "Could not create a non-touching critical path with a compatible Boss approach.";
            return false;
        }

        if (!TryAddCycleDetours(draft, cycleCount, random))
        {
            failure = $"Could not place {cycleCount} cycle detours around the critical path.";
            return false;
        }

        int remainingNodeCount = roomCount - draft.Nodes.Count;
        if (!TryAddMeaningfulBranches(draft, branchCount, remainingNodeCount, random))
        {
            failure = $"Could not place {branchCount} meaningful branches with {remainingNodeCount} available nodes.";
            return false;
        }

        if (draft.Nodes.Count != roomCount)
        {
            failure = $"Topology produced {draft.Nodes.Count}/{roomCount} nodes.";
            return false;
        }

        topology = draft;
        failure = string.Empty;
        return true;
    }

    private static bool TryResolveTopologyCounts(
        DungeonLayoutPolicySO policy,
        int roomCount,
        System.Random random,
        out int bossDistance,
        out int branchCount,
        out int cycleCount,
        out string failure)
    {
        int minimumRequiredExtras =
            policy.MinimumMeaningfulBranches + policy.MinimumCycleConnections * 2;
        int maximumBossDistance = Mathf.Min(
            policy.MaximumBossGraphDistance,
            roomCount - 1 - minimumRequiredExtras);
        int minimumBossDistance = policy.MinimumBossGraphDistance;
        if (maximumBossDistance < minimumBossDistance)
        {
            bossDistance = 0;
            branchCount = 0;
            cycleCount = 0;
            failure = "No feasible boss-distance value remains after reserving branch and cycle nodes.";
            return false;
        }

        bossDistance = NextInclusive(random, minimumBossDistance, maximumBossDistance);
        int extraNodeCount = roomCount - (bossDistance + 1);
        int maximumCycles = Mathf.Min(
            policy.MaximumCycleConnections,
            (extraNodeCount - policy.MinimumMeaningfulBranches) / 2);
        if (maximumCycles < policy.MinimumCycleConnections)
        {
            branchCount = 0;
            cycleCount = 0;
            failure = "No feasible cycle count remains after creating the critical path.";
            return false;
        }

        cycleCount = NextInclusive(
            random,
            policy.MinimumCycleConnections,
            maximumCycles);
        int maximumBranches = Mathf.Min(
            policy.MaximumMeaningfulBranches,
            extraNodeCount - cycleCount * 2);
        if (maximumBranches < policy.MinimumMeaningfulBranches)
        {
            branchCount = 0;
            failure = "No feasible branch count remains after reserving cycle detour nodes.";
            return false;
        }

        branchCount = NextInclusive(
            random,
            policy.MinimumMeaningfulBranches,
            maximumBranches);
        failure = string.Empty;
        return true;
    }

    private static bool TryBuildMainPath(
        TopologyDraft topology,
        int bossDistance,
        IReadOnlyList<RoomSocketDirection> allowedBossMovementDirections,
        System.Random random)
    {
        List<Vector2Int> positions = new() { Vector2Int.zero };
        HashSet<Vector2Int> occupied = new() { Vector2Int.zero };
        if (!TryExtendMainPath(
                positions,
                occupied,
                bossDistance,
                allowedBossMovementDirections,
                random))
        {
            return false;
        }

        for (int i = 0; i < positions.Count; i++)
        {
            topology.Nodes.Add(new PlannedNode
            {
                GridPosition = positions[i],
                IsMainPath = true
            });
            topology.MainPathNodeIndices.Add(i);
            if (i > 0)
                topology.Edges.Add(new PlannedEdge(i - 1, i, false));
        }

        topology.BossNodeIndex = positions.Count - 1;
        return true;
    }

    private static bool TryExtendMainPath(
        List<Vector2Int> positions,
        HashSet<Vector2Int> occupied,
        int targetEdgeCount,
        IReadOnlyList<RoomSocketDirection> allowedBossMovementDirections,
        System.Random random)
    {
        int completedEdgeCount = positions.Count - 1;
        if (completedEdgeCount >= targetEdgeCount)
            return true;

        bool placingBoss = completedEdgeCount == targetEdgeCount - 1;
        List<RoomSocketDirection> directions = placingBoss
            ? new List<RoomSocketDirection>(allowedBossMovementDirections)
            : new List<RoomSocketDirection>(AllDirections);
        Shuffle(directions, random);
        Vector2Int current = positions[positions.Count - 1];
        for (int i = 0; i < directions.Count; i++)
        {
            Vector2Int candidate = current + DirectionToVector(directions[i]);
            if (occupied.Contains(candidate) ||
                CountOccupiedNeighbors(candidate, occupied) != 1)
            {
                continue;
            }

            positions.Add(candidate);
            occupied.Add(candidate);
            if (TryExtendMainPath(
                    positions,
                    occupied,
                    targetEdgeCount,
                    allowedBossMovementDirections,
                    random))
            {
                return true;
            }

            occupied.Remove(candidate);
            positions.RemoveAt(positions.Count - 1);
        }

        return false;
    }

    private static bool TryAddCycleDetours(
        TopologyDraft topology,
        int cycleCount,
        System.Random random)
    {
        if (cycleCount <= 0)
            return true;

        List<int> mainEdgeIndices = new();
        for (int i = 0; i < topology.MainPathNodeIndices.Count - 1; i++)
            mainEdgeIndices.Add(i);
        Shuffle(mainEdgeIndices, random);

        HashSet<Vector2Int> occupied = CollectOccupiedCells(topology.Nodes);
        int addedCycles = 0;
        for (int candidateIndex = 0;
             candidateIndex < mainEdgeIndices.Count && addedCycles < cycleCount;
             candidateIndex++)
        {
            int mainEdgeIndex = mainEdgeIndices[candidateIndex];
            int firstNodeIndex = topology.MainPathNodeIndices[mainEdgeIndex];
            int secondNodeIndex = topology.MainPathNodeIndices[mainEdgeIndex + 1];
            Vector2Int first = topology.Nodes[firstNodeIndex].GridPosition;
            Vector2Int second = topology.Nodes[secondNodeIndex].GridPosition;
            Vector2Int edgeDirection = second - first;
            List<Vector2Int> perpendiculars = new()
            {
                new Vector2Int(-edgeDirection.y, edgeDirection.x),
                new Vector2Int(edgeDirection.y, -edgeDirection.x)
            };
            Shuffle(perpendiculars, random);

            for (int sideIndex = 0; sideIndex < perpendiculars.Count; sideIndex++)
            {
                Vector2Int firstDetour = first + perpendiculars[sideIndex];
                Vector2Int secondDetour = second + perpendiculars[sideIndex];
                if (occupied.Contains(firstDetour) || occupied.Contains(secondDetour) ||
                    HasUnexpectedOccupiedNeighbor(firstDetour, occupied, first) ||
                    HasUnexpectedOccupiedNeighbor(secondDetour, occupied, second))
                {
                    continue;
                }

                int firstDetourIndex = topology.Nodes.Count;
                topology.Nodes.Add(new PlannedNode { GridPosition = firstDetour });
                int secondDetourIndex = topology.Nodes.Count;
                topology.Nodes.Add(new PlannedNode { GridPosition = secondDetour });
                topology.Edges.Add(new PlannedEdge(firstNodeIndex, firstDetourIndex, true));
                topology.Edges.Add(new PlannedEdge(firstDetourIndex, secondDetourIndex, true));
                topology.Edges.Add(new PlannedEdge(secondDetourIndex, secondNodeIndex, true));
                occupied.Add(firstDetour);
                occupied.Add(secondDetour);
                addedCycles++;
                break;
            }
        }

        return addedCycles == cycleCount;
    }

    private static bool TryAddMeaningfulBranches(
        TopologyDraft topology,
        int branchCount,
        int availableNodeCount,
        System.Random random)
    {
        if (branchCount < 0 || availableNodeCount < branchCount)
            return false;
        if (availableNodeCount == 0)
            return branchCount == 0;

        HashSet<Vector2Int> occupied = CollectOccupiedCells(topology.Nodes);
        List<int> attachmentCandidates = new(topology.MainPathNodeIndices);
        attachmentCandidates.Remove(topology.BossNodeIndex);
        Shuffle(attachmentCandidates, random);
        List<int> branchEndpoints = new();
        HashSet<int> usedAttachments = new();

        for (int branchIndex = 0; branchIndex < branchCount; branchIndex++)
        {
            bool added = false;
            for (int attachmentListIndex = 0;
                 attachmentListIndex < attachmentCandidates.Count && !added;
                 attachmentListIndex++)
            {
                int attachmentNodeIndex = attachmentCandidates[attachmentListIndex];
                if (usedAttachments.Contains(attachmentNodeIndex) ||
                    GetNodeDegree(topology, attachmentNodeIndex) >= AllDirections.Length)
                {
                    continue;
                }

                List<RoomSocketDirection> directions = new(AllDirections);
                Shuffle(directions, random);
                Vector2Int attachmentCell = topology.Nodes[attachmentNodeIndex].GridPosition;
                for (int directionIndex = 0; directionIndex < directions.Count; directionIndex++)
                {
                    Vector2Int candidate =
                        attachmentCell + DirectionToVector(directions[directionIndex]);
                    if (occupied.Contains(candidate) ||
                        CountOccupiedNeighbors(candidate, occupied) != 1)
                    {
                        continue;
                    }

                    int nodeIndex = topology.Nodes.Count;
                    topology.Nodes.Add(new PlannedNode
                    {
                        GridPosition = candidate,
                        BranchGroup = branchIndex
                    });
                    topology.Edges.Add(new PlannedEdge(attachmentNodeIndex, nodeIndex, false));
                    occupied.Add(candidate);
                    usedAttachments.Add(attachmentNodeIndex);
                    branchEndpoints.Add(nodeIndex);
                    added = true;
                    break;
                }
            }

            if (!added)
                return false;
        }

        int remainingNodeCount = availableNodeCount - branchCount;
        for (int nodeOffset = 0; nodeOffset < remainingNodeCount; nodeOffset++)
        {
            bool added = false;
            List<int> endpointOrder = new();
            for (int endpointIndex = 0; endpointIndex < branchEndpoints.Count; endpointIndex++)
                endpointOrder.Add(endpointIndex);
            Shuffle(endpointOrder, random);

            for (int endpointOrderIndex = 0;
                 endpointOrderIndex < endpointOrder.Count && !added;
                 endpointOrderIndex++)
            {
                int endpointSlot = endpointOrder[endpointOrderIndex];
                int endpointNodeIndex = branchEndpoints[endpointSlot];
                Vector2Int endpointCell = topology.Nodes[endpointNodeIndex].GridPosition;
                List<RoomSocketDirection> directions = new(AllDirections);
                Shuffle(directions, random);
                for (int directionIndex = 0; directionIndex < directions.Count; directionIndex++)
                {
                    Vector2Int candidate = endpointCell + DirectionToVector(directions[directionIndex]);
                    if (occupied.Contains(candidate) ||
                        CountOccupiedNeighbors(candidate, occupied) != 1)
                    {
                        continue;
                    }

                    int nodeIndex = topology.Nodes.Count;
                    topology.Nodes.Add(new PlannedNode
                    {
                        GridPosition = candidate,
                        BranchGroup = topology.Nodes[endpointNodeIndex].BranchGroup
                    });
                    topology.Edges.Add(new PlannedEdge(endpointNodeIndex, nodeIndex, false));
                    occupied.Add(candidate);
                    branchEndpoints[endpointSlot] = nodeIndex;
                    added = true;
                    break;
                }
            }

            if (!added)
                return false;
        }

        return true;
    }

    private static void ReorderBossLast(TopologyDraft topology)
    {
        int oldBossIndex = topology.BossNodeIndex;
        if (oldBossIndex == topology.Nodes.Count - 1)
            return;

        List<PlannedNode> reorderedNodes = new(topology.Nodes.Count);
        Dictionary<int, int> remap = new();
        for (int oldIndex = 0; oldIndex < topology.Nodes.Count; oldIndex++)
        {
            if (oldIndex == oldBossIndex)
                continue;

            remap.Add(oldIndex, reorderedNodes.Count);
            reorderedNodes.Add(topology.Nodes[oldIndex]);
        }

        remap.Add(oldBossIndex, reorderedNodes.Count);
        reorderedNodes.Add(topology.Nodes[oldBossIndex]);

        List<PlannedEdge> reorderedEdges = new(topology.Edges.Count);
        for (int edgeIndex = 0; edgeIndex < topology.Edges.Count; edgeIndex++)
        {
            PlannedEdge edge = topology.Edges[edgeIndex];
            reorderedEdges.Add(new PlannedEdge(
                remap[edge.FirstNodeIndex],
                remap[edge.SecondNodeIndex],
                edge.IsCycleEdge));
        }

        for (int pathIndex = 0; pathIndex < topology.MainPathNodeIndices.Count; pathIndex++)
            topology.MainPathNodeIndices[pathIndex] = remap[topology.MainPathNodeIndices[pathIndex]];
        topology.Nodes.Clear();
        topology.Nodes.AddRange(reorderedNodes);
        topology.Edges.Clear();
        topology.Edges.AddRange(reorderedEdges);
        topology.BossNodeIndex = remap[oldBossIndex];
    }

    private static bool TryAssignRoomRoles(
        RoomThemeLibrarySO library,
        DungeonLayoutPolicySO policy,
        TopologyDraft topology,
        System.Random random,
        out string failure)
    {
        for (int nodeIndex = 0; nodeIndex < topology.Nodes.Count; nodeIndex++)
            topology.Nodes[nodeIndex].Role = RoomType.Combat;
        topology.Nodes[0].Role = RoomType.Start;
        topology.Nodes[topology.BossNodeIndex].Role = RoomType.Boss;

        List<int> assignedSpecialNodes = new();
        if (!TryAssignSpecialRole(
                library,
                topology,
                RoomType.Treasure,
                policy.TreasureRoomCount,
                policy.PreferSpecialRoomsAtDeadEnds,
                assignedSpecialNodes,
                random,
                out failure) ||
            !TryAssignSpecialRole(
                library,
                topology,
                RoomType.Event,
                policy.EventRoomCount,
                policy.PreferSpecialRoomsAtDeadEnds,
                assignedSpecialNodes,
                random,
                out failure) ||
            !TryAssignSpecialRole(
                library,
                topology,
                RoomType.Shop,
                policy.ShopRoomCount,
                policy.PreferSpecialRoomsAtDeadEnds,
                assignedSpecialNodes,
                random,
                out failure))
        {
            return false;
        }

        int combatRoomCount = 0;
        for (int nodeIndex = 0; nodeIndex < topology.Nodes.Count; nodeIndex++)
        {
            if (topology.Nodes[nodeIndex].Role == RoomType.Combat)
                combatRoomCount++;
        }

        if (combatRoomCount < policy.MinimumCombatRoomCount)
        {
            failure =
                $"Topology has {combatRoomCount} Combat rooms, below the policy minimum " +
                $"of {policy.MinimumCombatRoomCount}.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool TryAssignSpecialRole(
        RoomThemeLibrarySO library,
        TopologyDraft topology,
        RoomType role,
        int requestedCount,
        bool preferDeadEnds,
        List<int> assignedSpecialNodes,
        System.Random random,
        out string failure)
    {
        for (int placementIndex = 0; placementIndex < requestedCount; placementIndex++)
        {
            List<int> compatibleNodes = new();
            List<int> compatibleDeadEnds = new();
            for (int nodeIndex = 1; nodeIndex < topology.Nodes.Count; nodeIndex++)
            {
                if (nodeIndex == topology.BossNodeIndex ||
                    topology.Nodes[nodeIndex].Role != RoomType.Combat)
                {
                    continue;
                }

                List<RoomSocketDirection> requiredDirections = new();
                CollectRequiredDirections(topology, nodeIndex, requiredDirections);
                if (!HasCompatibleTemplate(library, role, requiredDirections))
                    continue;

                compatibleNodes.Add(nodeIndex);
                if (GetNodeDegree(topology, nodeIndex) == 1)
                    compatibleDeadEnds.Add(nodeIndex);
            }

            List<int> candidates = preferDeadEnds && compatibleDeadEnds.Count > 0
                ? compatibleDeadEnds
                : compatibleNodes;
            if (candidates.Count == 0)
            {
                failure =
                    $"No unassigned topology node can use a {role} template with its required socket directions.";
                return false;
            }

            int selectedNodeIndex = SelectSpreadNode(
                topology,
                candidates,
                assignedSpecialNodes,
                random);
            topology.Nodes[selectedNodeIndex].Role = role;
            assignedSpecialNodes.Add(selectedNodeIndex);
        }

        failure = string.Empty;
        return true;
    }

    private static int SelectSpreadNode(
        TopologyDraft topology,
        IReadOnlyList<int> candidates,
        IReadOnlyList<int> assignedSpecialNodes,
        System.Random random)
    {
        int selectedNodeIndex = candidates[0];
        int selectedScore = int.MinValue;
        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            int nodeIndex = candidates[candidateIndex];
            int distanceFromStart = CalculateGraphDistance(topology, 0, nodeIndex);
            int distanceFromOtherSpecials = topology.Nodes.Count;
            for (int assignedIndex = 0; assignedIndex < assignedSpecialNodes.Count; assignedIndex++)
            {
                distanceFromOtherSpecials = Mathf.Min(
                    distanceFromOtherSpecials,
                    CalculateGraphDistance(topology, assignedSpecialNodes[assignedIndex], nodeIndex));
            }

            if (assignedSpecialNodes.Count == 0)
                distanceFromOtherSpecials = distanceFromStart;
            int score = distanceFromStart * 16 + distanceFromOtherSpecials * 32 + random.Next(16);
            if (score <= selectedScore)
                continue;

            selectedScore = score;
            selectedNodeIndex = nodeIndex;
        }

        return selectedNodeIndex;
    }

    private static bool TrySelectTemplatesAndSockets(
        RoomThemeLibrarySO library,
        TopologyDraft topology,
        System.Random random,
        out string failure)
    {
        List<RoomTemplateSO> candidates = new();
        List<RoomSocketDirection> requiredDirections = new();
        for (int nodeIndex = 0; nodeIndex < topology.Nodes.Count; nodeIndex++)
        {
            PlannedNode node = topology.Nodes[nodeIndex];
            CollectRequiredDirections(topology, nodeIndex, requiredDirections);
            candidates.Clear();
            library.CollectRooms(node.Role, candidates);
            for (int candidateIndex = candidates.Count - 1; candidateIndex >= 0; candidateIndex--)
            {
                if (!IsTemplateCompatible(candidates[candidateIndex], requiredDirections))
                    candidates.RemoveAt(candidateIndex);
            }

            RoomTemplateSO selectedTemplate = SelectWeightedTemplate(candidates, random);
            if (selectedTemplate == null)
            {
                failure =
                    $"No usable {node.Role} template supports topology node {nodeIndex} " +
                    $"with directions [{FormatDirections(requiredDirections)}].";
                return false;
            }

            node.Template = selectedTemplate;
            node.LocalBounds = ResolveLocalBounds(selectedTemplate.LayoutData);
            node.SocketIndices.Clear();
            if (!TrySelectSocketIndices(
                    selectedTemplate.LayoutData,
                    requiredDirections,
                    random,
                    node.SocketIndices))
            {
                failure =
                    $"Template '{selectedTemplate.LayoutData.roomId}' could not resolve a grid-compatible socket set.";
                return false;
            }

            if (!TryResolveNodeReferences(node, requiredDirections, out failure))
                return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool TryCreatePhysicalLayout(
        int seed,
        int requestedRoomCount,
        TopologyDraft topology,
        System.Random random,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation,
        out DungeonLayoutResult result,
        out string failure)
    {
        result = null;
        Dictionary<int, int> leftExtents = new();
        Dictionary<int, int> rightExtents = new();
        Dictionary<int, int> bottomExtents = new();
        Dictionary<int, int> topExtents = new();
        List<int> columns = new();
        List<int> rows = new();
        CollectAxisExtents(
            topology,
            leftExtents,
            rightExtents,
            bottomExtents,
            topExtents,
            columns,
            rows);

        Dictionary<int, int> columnAnchors = CreateAxisAnchors(
            topology,
            columns,
            leftExtents,
            rightExtents,
            horizontal: true,
            minimumCorridorLength,
            corridorLengthPerRoomCell,
            corridorLengthVariation,
            random);
        Dictionary<int, int> rowAnchors = CreateAxisAnchors(
            topology,
            rows,
            bottomExtents,
            topExtents,
            horizontal: false,
            minimumCorridorLength,
            corridorLengthPerRoomCell,
            corridorLengthVariation,
            random);

        DungeonLayoutResult builtResult = new(seed, requestedRoomCount);
        for (int nodeIndex = 0; nodeIndex < topology.Nodes.Count; nodeIndex++)
        {
            PlannedNode node = topology.Nodes[nodeIndex];
            node.Origin = new Vector2Int(
                columnAnchors[node.GridPosition.x] - node.ReferenceX,
                rowAnchors[node.GridPosition.y] - node.ReferenceY);
            node.WorldBounds = new RectInt(
                node.LocalBounds.position + node.Origin,
                node.LocalBounds.size);
            builtResult.AddRoom(new DungeonRoomPlacement(
                nodeIndex,
                node.Template,
                node.Origin,
                node.WorldBounds));
        }

        if (HasRoomOverlap(topology))
        {
            failure = "Graph grid embedding produced overlapping room reservation bounds.";
            return false;
        }

        for (int edgeIndex = 0; edgeIndex < topology.Edges.Count; edgeIndex++)
        {
            PlannedEdge edge = topology.Edges[edgeIndex];
            PlannedNode first = topology.Nodes[edge.FirstNodeIndex];
            PlannedNode second = topology.Nodes[edge.SecondNodeIndex];
            RoomSocketDirection direction = GetDirection(first.GridPosition, second.GridPosition);
            if (direction == (RoomSocketDirection)(-1))
            {
                failure = $"Topology edge {edgeIndex} does not connect cardinal-neighbor nodes.";
                return false;
            }

            int firstSocketIndex = first.SocketIndices[direction];
            RoomSocketDirection secondDirection = Opposite(direction);
            int secondSocketIndex = second.SocketIndices[secondDirection];
            RoomSocketData firstSocket = first.Template.LayoutData.sockets[firstSocketIndex];
            RoomSocketData secondSocket = second.Template.LayoutData.sockets[secondSocketIndex];
            Vector2Int firstWorldCell = first.Origin + firstSocket.localCell;
            Vector2Int secondWorldCell = second.Origin + secondSocket.localCell;
            if (!AreSocketSpansAligned(direction, firstWorldCell, secondWorldCell))
            {
                failure =
                    $"Topology edge {edgeIndex} socket spans are not aligned. " +
                    $"First={firstWorldCell}, Second={secondWorldCell}, Direction={direction}.";
                return false;
            }

            int corridorLength = CalculateCorridorLength(
                direction,
                firstWorldCell,
                secondWorldCell);
            if (corridorLength < minimumCorridorLength)
            {
                failure =
                    $"Topology edge {edgeIndex} corridor is shorter than the configured minimum. " +
                    $"Length={corridorLength}, Minimum={minimumCorridorLength}.";
                return false;
            }

            RectInt corridorBounds = CreateCorridorBounds(
                direction,
                RoomSocketGeometry.ResolveWidth(firstSocket),
                firstWorldCell,
                corridorLength);
            if (CorridorOverlapsUnrelatedRoom(
                    corridorBounds,
                    topology,
                    edge.FirstNodeIndex,
                    edge.SecondNodeIndex) ||
                CorridorOverlapsExistingCorridor(corridorBounds, builtResult.Connections))
            {
                failure = $"Topology edge {edgeIndex} corridor reservation overlaps existing geometry.";
                return false;
            }

            builtResult.AddConnection(new DungeonSocketConnection(
                edge.FirstNodeIndex,
                firstSocketIndex,
                edge.SecondNodeIndex,
                secondSocketIndex,
                corridorLength,
                corridorBounds));
        }

        result = builtResult;
        failure = string.Empty;
        return true;
    }

    private static void CollectAxisExtents(
        TopologyDraft topology,
        Dictionary<int, int> leftExtents,
        Dictionary<int, int> rightExtents,
        Dictionary<int, int> bottomExtents,
        Dictionary<int, int> topExtents,
        List<int> columns,
        List<int> rows)
    {
        HashSet<int> columnSet = new();
        HashSet<int> rowSet = new();
        for (int nodeIndex = 0; nodeIndex < topology.Nodes.Count; nodeIndex++)
        {
            PlannedNode node = topology.Nodes[nodeIndex];
            int column = node.GridPosition.x;
            int row = node.GridPosition.y;
            columnSet.Add(column);
            rowSet.Add(row);
            SetMaximum(leftExtents, column, node.ReferenceX - node.LocalBounds.xMin);
            SetMaximum(rightExtents, column, node.LocalBounds.xMax - 1 - node.ReferenceX);
            SetMaximum(bottomExtents, row, node.ReferenceY - node.LocalBounds.yMin);
            SetMaximum(topExtents, row, node.LocalBounds.yMax - 1 - node.ReferenceY);
        }

        columns.AddRange(columnSet);
        rows.AddRange(rowSet);
        columns.Sort();
        rows.Sort();
    }

    private static Dictionary<int, int> CreateAxisAnchors(
        TopologyDraft topology,
        IReadOnlyList<int> coordinates,
        IReadOnlyDictionary<int, int> negativeExtents,
        IReadOnlyDictionary<int, int> positiveExtents,
        bool horizontal,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation,
        System.Random random)
    {
        Dictionary<int, int> anchors = new();
        if (coordinates.Count == 0)
            return anchors;

        anchors.Add(coordinates[0], 0);
        for (int coordinateIndex = 1; coordinateIndex < coordinates.Count; coordinateIndex++)
        {
            int previousCoordinate = coordinates[coordinateIndex - 1];
            int coordinate = coordinates[coordinateIndex];
            int gap = ResolveAxisBoundaryGap(
                topology,
                previousCoordinate,
                coordinate,
                horizontal,
                minimumCorridorLength,
                corridorLengthPerRoomCell,
                corridorLengthVariation,
                random);
            int coordinateDistance = Mathf.Max(1, coordinate - previousCoordinate);
            int separation = positiveExtents[previousCoordinate] +
                negativeExtents[coordinate] +
                1 +
                gap * coordinateDistance;
            anchors.Add(coordinate, anchors[previousCoordinate] + separation);
        }

        return anchors;
    }

    private static int ResolveAxisBoundaryGap(
        TopologyDraft topology,
        int lowerCoordinate,
        int upperCoordinate,
        bool horizontal,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation,
        System.Random random)
    {
        int maximumGap = minimumCorridorLength;
        for (int edgeIndex = 0; edgeIndex < topology.Edges.Count; edgeIndex++)
        {
            PlannedEdge edge = topology.Edges[edgeIndex];
            PlannedNode first = topology.Nodes[edge.FirstNodeIndex];
            PlannedNode second = topology.Nodes[edge.SecondNodeIndex];
            int firstCoordinate = horizontal ? first.GridPosition.x : first.GridPosition.y;
            int secondCoordinate = horizontal ? second.GridPosition.x : second.GridPosition.y;
            if (Mathf.Min(firstCoordinate, secondCoordinate) != lowerCoordinate ||
                Mathf.Max(firstCoordinate, secondCoordinate) != upperCoordinate)
            {
                continue;
            }

            int firstDepth = horizontal ? first.LocalBounds.width : first.LocalBounds.height;
            int secondDepth = horizontal ? second.LocalBounds.width : second.LocalBounds.height;
            int sizeDrivenLength = Mathf.CeilToInt(
                (Mathf.Max(0, firstDepth) + Mathf.Max(0, secondDepth)) *
                corridorLengthPerRoomCell);
            int variation = corridorLengthVariation > 0
                ? random.Next(corridorLengthVariation + 1)
                : 0;
            maximumGap = Mathf.Max(
                maximumGap,
                minimumCorridorLength + sizeDrivenLength + variation);
        }

        return maximumGap;
    }

    private static bool TryResolveNodeReferences(
        PlannedNode node,
        IReadOnlyList<RoomSocketDirection> requiredDirections,
        out string failure)
    {
        bool hasVerticalReference = false;
        bool hasHorizontalReference = false;
        int verticalReference = 0;
        int horizontalReference = 0;
        for (int directionIndex = 0; directionIndex < requiredDirections.Count; directionIndex++)
        {
            RoomSocketDirection direction = requiredDirections[directionIndex];
            RoomSocketData socket =
                node.Template.LayoutData.sockets[node.SocketIndices[direction]];
            if (direction == RoomSocketDirection.Up || direction == RoomSocketDirection.Down)
            {
                if (hasVerticalReference && verticalReference != socket.localCell.x)
                {
                    failure =
                        $"Template '{node.Template.LayoutData.roomId}' has vertically opposite sockets " +
                        "on different grid columns.";
                    return false;
                }

                verticalReference = socket.localCell.x;
                hasVerticalReference = true;
            }
            else
            {
                if (hasHorizontalReference && horizontalReference != socket.localCell.y)
                {
                    failure =
                        $"Template '{node.Template.LayoutData.roomId}' has horizontally opposite sockets " +
                        "on different grid rows.";
                    return false;
                }

                horizontalReference = socket.localCell.y;
                hasHorizontalReference = true;
            }
        }

        node.ReferenceX = hasVerticalReference
            ? verticalReference
            : node.LocalBounds.xMin + Mathf.Max(
                0,
                (node.LocalBounds.width - RoomSocketGeometry.RequiredWidth) / 2);
        node.ReferenceY = hasHorizontalReference
            ? horizontalReference
            : node.LocalBounds.yMin + Mathf.Max(
                0,
                (node.LocalBounds.height - RoomSocketGeometry.RequiredWidth) / 2);
        failure = string.Empty;
        return true;
    }

    private static bool HasCompatibleTemplate(
        RoomThemeLibrarySO library,
        RoomType role,
        IReadOnlyList<RoomSocketDirection> requiredDirections)
    {
        List<RoomTemplateSO> candidates = new();
        library.CollectRooms(role, candidates);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (IsTemplateCompatible(candidates[i], requiredDirections))
                return true;
        }

        return false;
    }

    private static bool IsTemplateCompatible(
        RoomTemplateSO template,
        IReadOnlyList<RoomSocketDirection> requiredDirections)
    {
        if (!IsTemplateUsable(template))
            return false;

        Dictionary<RoomSocketDirection, int> selectedSockets = new();
        return TrySelectSocketIndices(
            template.LayoutData,
            requiredDirections,
            random: null,
            selectedSockets);
    }

    private static bool TrySelectSocketIndices(
        RoomLayoutData layout,
        IReadOnlyList<RoomSocketDirection> requiredDirections,
        System.Random random,
        Dictionary<RoomSocketDirection, int> results)
    {
        results.Clear();
        Dictionary<RoomSocketDirection, List<int>> candidatesByDirection = new();
        RectInt bounds = ResolveLocalBounds(layout);
        for (int directionIndex = 0; directionIndex < requiredDirections.Count; directionIndex++)
        {
            RoomSocketDirection direction = requiredDirections[directionIndex];
            List<int> candidates = new();
            for (int socketIndex = 0; socketIndex < layout.sockets.Count; socketIndex++)
            {
                RoomSocketData socket = layout.sockets[socketIndex];
                if (socket.direction == direction && RoomSocketGeometry.IsValid(socket, bounds))
                    candidates.Add(socketIndex);
            }

            if (candidates.Count == 0)
                return false;
            candidatesByDirection.Add(direction, candidates);
        }

        if (!TrySelectOppositeAxisPair(
                layout,
                candidatesByDirection,
                RoomSocketDirection.Up,
                RoomSocketDirection.Down,
                compareX: true,
                random,
                results) ||
            !TrySelectOppositeAxisPair(
                layout,
                candidatesByDirection,
                RoomSocketDirection.Right,
                RoomSocketDirection.Left,
                compareX: false,
                random,
                results))
        {
            return false;
        }

        foreach (KeyValuePair<RoomSocketDirection, List<int>> pair in candidatesByDirection)
        {
            if (results.ContainsKey(pair.Key))
                continue;
            int selectedListIndex = random != null ? random.Next(pair.Value.Count) : 0;
            results.Add(pair.Key, pair.Value[selectedListIndex]);
        }

        return true;
    }

    private static bool TrySelectOppositeAxisPair(
        RoomLayoutData layout,
        IReadOnlyDictionary<RoomSocketDirection, List<int>> candidatesByDirection,
        RoomSocketDirection firstDirection,
        RoomSocketDirection secondDirection,
        bool compareX,
        System.Random random,
        Dictionary<RoomSocketDirection, int> results)
    {
        if (!candidatesByDirection.TryGetValue(firstDirection, out List<int> firstCandidates) ||
            !candidatesByDirection.TryGetValue(secondDirection, out List<int> secondCandidates))
        {
            return true;
        }

        List<Vector2Int> compatiblePairs = new();
        for (int firstIndex = 0; firstIndex < firstCandidates.Count; firstIndex++)
        {
            RoomSocketData firstSocket = layout.sockets[firstCandidates[firstIndex]];
            for (int secondIndex = 0; secondIndex < secondCandidates.Count; secondIndex++)
            {
                RoomSocketData secondSocket = layout.sockets[secondCandidates[secondIndex]];
                bool aligned = compareX
                    ? firstSocket.localCell.x == secondSocket.localCell.x
                    : firstSocket.localCell.y == secondSocket.localCell.y;
                if (aligned)
                    compatiblePairs.Add(new Vector2Int(firstCandidates[firstIndex], secondCandidates[secondIndex]));
            }
        }

        if (compatiblePairs.Count == 0)
            return false;
        Vector2Int selectedPair = compatiblePairs[random != null ? random.Next(compatiblePairs.Count) : 0];
        results.Add(firstDirection, selectedPair.x);
        results.Add(secondDirection, selectedPair.y);
        return true;
    }

    private static bool IsTemplateUsable(RoomTemplateSO template)
    {
        if (template == null)
            return false;

        RoomLayoutData layout = template.LayoutData;
        if (layout.selectionWeight <= 0f ||
            float.IsNaN(layout.selectionWeight) ||
            float.IsInfinity(layout.selectionWeight) ||
            layout.sockets == null)
        {
            return false;
        }

        RectInt bounds = ResolveLocalBounds(layout);
        return bounds.width > 0 && bounds.height > 0;
    }

    private static RoomTemplateSO SelectWeightedTemplate(
        IReadOnlyList<RoomTemplateSO> candidates,
        System.Random random)
    {
        double totalWeight = 0d;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += candidates[i].LayoutData.selectionWeight;
        if (totalWeight <= 0d)
            return null;

        double selectedWeight = random.NextDouble() * totalWeight;
        for (int i = 0; i < candidates.Count; i++)
        {
            selectedWeight -= candidates[i].LayoutData.selectionWeight;
            if (selectedWeight <= 0d)
                return candidates[i];
        }

        return candidates.Count > 0 ? candidates[candidates.Count - 1] : null;
    }

    private static void CollectAllowedBossMovementDirections(
        RoomThemeLibrarySO library,
        List<RoomSocketDirection> results)
    {
        results.Clear();
        List<RoomTemplateSO> bossRooms = new();
        library.CollectRooms(RoomType.Boss, bossRooms);
        for (int roomIndex = 0; roomIndex < bossRooms.Count; roomIndex++)
        {
            RoomTemplateSO room = bossRooms[roomIndex];
            if (!IsTemplateUsable(room))
                continue;
            RoomLayoutData layout = room.LayoutData;
            RectInt bounds = ResolveLocalBounds(layout);
            for (int socketIndex = 0; socketIndex < layout.sockets.Count; socketIndex++)
            {
                RoomSocketData socket = layout.sockets[socketIndex];
                if (!RoomSocketGeometry.IsValid(socket, bounds))
                    continue;
                RoomSocketDirection movementDirection = Opposite(socket.direction);
                if (!results.Contains(movementDirection))
                    results.Add(movementDirection);
            }
        }
    }

    private static void CollectRequiredDirections(
        TopologyDraft topology,
        int nodeIndex,
        List<RoomSocketDirection> results)
    {
        results.Clear();
        Vector2Int position = topology.Nodes[nodeIndex].GridPosition;
        for (int edgeIndex = 0; edgeIndex < topology.Edges.Count; edgeIndex++)
        {
            PlannedEdge edge = topology.Edges[edgeIndex];
            int neighborIndex;
            if (edge.FirstNodeIndex == nodeIndex)
                neighborIndex = edge.SecondNodeIndex;
            else if (edge.SecondNodeIndex == nodeIndex)
                neighborIndex = edge.FirstNodeIndex;
            else
                continue;

            RoomSocketDirection direction =
                GetDirection(position, topology.Nodes[neighborIndex].GridPosition);
            if (direction != (RoomSocketDirection)(-1) && !results.Contains(direction))
                results.Add(direction);
        }
    }

    private static int GetNodeDegree(TopologyDraft topology, int nodeIndex)
    {
        int degree = 0;
        for (int edgeIndex = 0; edgeIndex < topology.Edges.Count; edgeIndex++)
        {
            PlannedEdge edge = topology.Edges[edgeIndex];
            if (edge.FirstNodeIndex == nodeIndex || edge.SecondNodeIndex == nodeIndex)
                degree++;
        }

        return degree;
    }

    private static int CalculateGraphDistance(
        TopologyDraft topology,
        int startNodeIndex,
        int targetNodeIndex)
    {
        if (startNodeIndex == targetNodeIndex)
            return 0;
        int[] distances = new int[topology.Nodes.Count];
        for (int i = 0; i < distances.Length; i++)
            distances[i] = -1;
        Queue<int> queue = new();
        distances[startNodeIndex] = 0;
        queue.Enqueue(startNodeIndex);
        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            for (int edgeIndex = 0; edgeIndex < topology.Edges.Count; edgeIndex++)
            {
                PlannedEdge edge = topology.Edges[edgeIndex];
                int neighbor = edge.FirstNodeIndex == current
                    ? edge.SecondNodeIndex
                    : edge.SecondNodeIndex == current
                        ? edge.FirstNodeIndex
                        : -1;
                if (neighbor < 0 || distances[neighbor] >= 0)
                    continue;
                distances[neighbor] = distances[current] + 1;
                if (neighbor == targetNodeIndex)
                    return distances[neighbor];
                queue.Enqueue(neighbor);
            }
        }

        return -1;
    }

    private static int CountExplorationDeadEnds(TopologyDraft topology)
    {
        int count = 0;
        for (int nodeIndex = 1; nodeIndex < topology.Nodes.Count; nodeIndex++)
        {
            if (nodeIndex != topology.BossNodeIndex && GetNodeDegree(topology, nodeIndex) == 1)
                count++;
        }

        return count;
    }

    private static HashSet<Vector2Int> CollectOccupiedCells(IReadOnlyList<PlannedNode> nodes)
    {
        HashSet<Vector2Int> occupied = new();
        for (int i = 0; i < nodes.Count; i++)
            occupied.Add(nodes[i].GridPosition);
        return occupied;
    }

    private static int CountOccupiedNeighbors(
        Vector2Int position,
        HashSet<Vector2Int> occupied)
    {
        int count = 0;
        for (int directionIndex = 0; directionIndex < AllDirections.Length; directionIndex++)
        {
            if (occupied.Contains(position + DirectionToVector(AllDirections[directionIndex])))
                count++;
        }

        return count;
    }

    private static bool HasUnexpectedOccupiedNeighbor(
        Vector2Int position,
        HashSet<Vector2Int> occupied,
        Vector2Int allowedNeighbor)
    {
        for (int directionIndex = 0; directionIndex < AllDirections.Length; directionIndex++)
        {
            Vector2Int neighbor = position + DirectionToVector(AllDirections[directionIndex]);
            if (neighbor != allowedNeighbor && occupied.Contains(neighbor))
                return true;
        }

        return false;
    }

    private static bool HasRoomOverlap(TopologyDraft topology)
    {
        for (int firstIndex = 0; firstIndex < topology.Nodes.Count; firstIndex++)
        {
            for (int secondIndex = firstIndex + 1; secondIndex < topology.Nodes.Count; secondIndex++)
            {
                if (topology.Nodes[firstIndex].WorldBounds.Overlaps(
                        topology.Nodes[secondIndex].WorldBounds))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool CorridorOverlapsUnrelatedRoom(
        RectInt corridorBounds,
        TopologyDraft topology,
        int firstNodeIndex,
        int secondNodeIndex)
    {
        for (int nodeIndex = 0; nodeIndex < topology.Nodes.Count; nodeIndex++)
        {
            if (nodeIndex == firstNodeIndex || nodeIndex == secondNodeIndex)
                continue;
            if (corridorBounds.Overlaps(topology.Nodes[nodeIndex].WorldBounds))
                return true;
        }

        return false;
    }

    private static bool CorridorOverlapsExistingCorridor(
        RectInt corridorBounds,
        IReadOnlyList<DungeonSocketConnection> connections)
    {
        for (int connectionIndex = 0; connectionIndex < connections.Count; connectionIndex++)
        {
            RectInt existing = connections[connectionIndex].CorridorBounds;
            if (existing.width > 0 && existing.height > 0 && corridorBounds.Overlaps(existing))
                return true;
        }

        return false;
    }

    private static bool AreSocketSpansAligned(
        RoomSocketDirection direction,
        Vector2Int firstWorldCell,
        Vector2Int secondWorldCell)
    {
        return direction == RoomSocketDirection.Left || direction == RoomSocketDirection.Right
            ? firstWorldCell.y == secondWorldCell.y
            : firstWorldCell.x == secondWorldCell.x;
    }

    private static int CalculateCorridorLength(
        RoomSocketDirection direction,
        Vector2Int firstWorldCell,
        Vector2Int secondWorldCell)
    {
        Vector2Int delta = secondWorldCell - firstWorldCell;
        Vector2Int directionVector = DirectionToVector(direction);
        return delta.x * directionVector.x + delta.y * directionVector.y - 1;
    }

    private static RectInt CreateCorridorBounds(
        RoomSocketDirection direction,
        int socketWidth,
        Vector2Int worldCell,
        int corridorLength)
    {
        if (corridorLength <= 0)
            return default;
        Vector2Int directionVector = DirectionToVector(direction);
        Vector2Int tangent = RoomSocketGeometry.GetTangent(direction);
        Vector2Int firstCorner = worldCell + directionVector - tangent;
        Vector2Int oppositeCorner =
            worldCell + directionVector * corridorLength + tangent * socketWidth;
        int xMin = Mathf.Min(firstCorner.x, oppositeCorner.x);
        int yMin = Mathf.Min(firstCorner.y, oppositeCorner.y);
        int xMax = Mathf.Max(firstCorner.x, oppositeCorner.x);
        int yMax = Mathf.Max(firstCorner.y, oppositeCorner.y);
        return new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
    }

    private static RectInt ResolveLocalBounds(RoomLayoutData layout)
    {
        return layout.localBounds.width > 0 && layout.localBounds.height > 0
            ? layout.localBounds
            : new RectInt(Vector2Int.zero, layout.size);
    }

    private static RoomSocketDirection GetDirection(Vector2Int from, Vector2Int to)
    {
        Vector2Int delta = to - from;
        if (delta == Vector2Int.up)
            return RoomSocketDirection.Up;
        if (delta == Vector2Int.right)
            return RoomSocketDirection.Right;
        if (delta == Vector2Int.down)
            return RoomSocketDirection.Down;
        if (delta == Vector2Int.left)
            return RoomSocketDirection.Left;
        return (RoomSocketDirection)(-1);
    }

    private static RoomSocketDirection Opposite(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => RoomSocketDirection.Down,
            RoomSocketDirection.Right => RoomSocketDirection.Left,
            RoomSocketDirection.Down => RoomSocketDirection.Up,
            RoomSocketDirection.Left => RoomSocketDirection.Right,
            _ => direction
        };
    }

    private static Vector2Int DirectionToVector(RoomSocketDirection direction)
    {
        return direction switch
        {
            RoomSocketDirection.Up => Vector2Int.up,
            RoomSocketDirection.Right => Vector2Int.right,
            RoomSocketDirection.Down => Vector2Int.down,
            RoomSocketDirection.Left => Vector2Int.left,
            _ => Vector2Int.zero
        };
    }

    private static void SetMaximum(Dictionary<int, int> values, int key, int value)
    {
        if (!values.TryGetValue(key, out int current) || value > current)
            values[key] = value;
    }

    private static int NextInclusive(System.Random random, int minimum, int maximum)
    {
        return maximum <= minimum ? minimum : random.Next(minimum, maximum + 1);
    }

    private static string FormatDirections(IReadOnlyList<RoomSocketDirection> directions)
    {
        if (directions.Count == 0)
            return string.Empty;
        string text = directions[0].ToString();
        for (int i = 1; i < directions.Count; i++)
            text += $", {directions[i]}";
        return text;
    }

    private static void Shuffle<T>(IList<T> values, System.Random random)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
        }
    }
}

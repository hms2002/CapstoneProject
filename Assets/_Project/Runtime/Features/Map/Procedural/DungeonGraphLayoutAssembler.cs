using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 탐색 정책에 맞는 연결 그래프를 먼저 만든 뒤 노드 역할, 방 템플릿, 소켓과 월드 배치를 순서대로 결정한다.
/// - 보스 거리, 의미 있는 분기, 순환 연결, 필수 방 역할과 물리적 비겹침을 하나의 완성 결과로 검증한다.
/// - 생성 프로필이 지정한 필수 템플릿을 호환 노드에 정확히 한 번 선점시켜 역할 수 보장과 콘텐츠 보장을 함께 지킨다.
/// - 방 크기 기반 권장 복도 간격이 충돌하면 절대 최소 길이까지 단계적으로 압축해 같은 그래프를 재사용한다.
/// - 성공 후보가 여러 개면 권장 길이 초과가 가장 작은 배치를 선택하되, 품질 목표를 못 맞춰도 최선 후보로 생성 성공을 보장한다.
/// - 테마, 타일, 몬스터 구현을 알지 않고 RoomThemeLibrarySO의 레이아웃 데이터만 소비한다.
/// </summary>
public sealed class DungeonGraphLayoutAssembler
{
    private const double DefaultExactSocketDirectionMatchWeightMultiplier = 3d;
    private const double DefaultExtraSocketDirectionWeightMultiplier = 0.45d;

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
        public bool IsCycleDetour;
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
    /// - 같은 축 정렬을 공유하는 노드 그룹과 그룹 사이 최소 간격 제약을 보관한다.
    /// - 논리 좌표 순서를 지키는 차분 제약을 풀어 각 그룹의 압축된 월드 기준축 좌표를 계산한다.
    /// </summary>
    private sealed class AxisConstraintLayout
    {
        private readonly int[] groupByNode;
        private readonly int[] logicalCoordinateByGroup;
        private readonly int[] anchors;
        private readonly Dictionary<long, int> minimumSeparations = new();

        public AxisConstraintLayout(
            int[] nodeGroups,
            int[] groupLogicalCoordinates)
        {
            groupByNode = nodeGroups;
            logicalCoordinateByGroup = groupLogicalCoordinates;
            anchors = new int[groupLogicalCoordinates.Length];
        }

        public int GetGroup(int nodeIndex) => groupByNode[nodeIndex];

        public int GetAnchorForNode(int nodeIndex) =>
            anchors[groupByNode[nodeIndex]];

        public int GetAnchorForGroup(int groupIndex) => anchors[groupIndex];

        public bool AddMinimumSeparation(
            int lowerGroupIndex,
            int upperGroupIndex,
            int minimumSeparation)
        {
            if (lowerGroupIndex == upperGroupIndex ||
                logicalCoordinateByGroup[lowerGroupIndex] >=
                logicalCoordinateByGroup[upperGroupIndex])
            {
                return false;
            }

            long key = CreateConstraintKey(lowerGroupIndex, upperGroupIndex);
            int resolvedSeparation = Mathf.Max(0, minimumSeparation);
            if (minimumSeparations.TryGetValue(key, out int existing) &&
                existing >= resolvedSeparation)
            {
                return false;
            }

            minimumSeparations[key] = resolvedSeparation;
            return true;
        }

        public void Solve()
        {
            Array.Clear(anchors, 0, anchors.Length);
            var orderedGroups = new List<int>(anchors.Length);
            for (int groupIndex = 0; groupIndex < anchors.Length; groupIndex++)
                orderedGroups.Add(groupIndex);
            orderedGroups.Sort((first, second) =>
            {
                int coordinateComparison = logicalCoordinateByGroup[first]
                    .CompareTo(logicalCoordinateByGroup[second]);
                return coordinateComparison != 0
                    ? coordinateComparison
                    : first.CompareTo(second);
            });

            for (int orderIndex = 0; orderIndex < orderedGroups.Count; orderIndex++)
            {
                int upperGroupIndex = orderedGroups[orderIndex];
                foreach (KeyValuePair<long, int> pair in minimumSeparations)
                {
                    DecodeConstraintKey(
                        pair.Key,
                        out int lowerGroupIndex,
                        out int candidateUpperGroupIndex);
                    if (candidateUpperGroupIndex != upperGroupIndex)
                        continue;

                    anchors[upperGroupIndex] = Mathf.Max(
                        anchors[upperGroupIndex],
                        anchors[lowerGroupIndex] + pair.Value);
                }
            }
        }

        private static long CreateConstraintKey(int lowerGroupIndex, int upperGroupIndex)
        {
            return ((long)lowerGroupIndex << 32) | (uint)upperGroupIndex;
        }

        private static void DecodeConstraintKey(
            long key,
            out int lowerGroupIndex,
            out int upperGroupIndex)
        {
            lowerGroupIndex = (int)(key >> 32);
            upperGroupIndex = (int)key;
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
        int corridorLengthVariation,
        IReadOnlyList<RoomTemplateSO> guaranteedRoomTemplates = null,
        IReadOnlyList<RequiredCombatRoomRule> requiredCombatRoomRules = null)
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

        requiredCombatRoomRules ??= policy.RequiredCombatRoomRules;

        if (!ValidateGuaranteedRoomTemplates(
                library,
                guaranteedRoomTemplates,
                out string guaranteedRoomFailure))
        {
            failedResult.MarkFailed(guaranteedRoomFailure);
            return failedResult;
        }

        if (!ValidateRequiredCombatRoomRules(
                library,
                requiredCombatRoomRules,
                out string requiredCombatFailure))
        {
            failedResult.MarkFailed(requiredCombatFailure);
            return failedResult;
        }

        if (!ValidateRoleBudget(
                policy,
                roomCount,
                guaranteedRoomTemplates,
                requiredCombatRoomRules,
                out string budgetFailure))
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
        DungeonLayoutResult bestPhysicalResult = null;
        int bestMaximumPreferenceOverrun = int.MaxValue;
        int bestLongestCorridorLength = int.MaxValue;
        int physicalCandidateCount = 0;
        int qualityCandidateLimit = Mathf.Min(attemptCount, 64);

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
            if (!TryAssignRoomRoles(
                    library,
                    policy,
                    guaranteedRoomTemplates,
                    requiredCombatRoomRules,
                    topology,
                    random,
                    out lastFailure) ||
                !TrySelectTemplatesAndSockets(
                    library,
                    policy,
                    guaranteedRoomTemplates,
                    requiredCombatRoomRules,
                    topology,
                    random,
                    out lastFailure) ||
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
            physicalCandidateCount++;
            int maximumPreferenceOverrun = CalculateMaximumCorridorPreferenceOverrun(
                result,
                resolvedMinimumCorridorLength,
                resolvedCorridorLengthPerRoomCell,
                resolvedCorridorLengthVariation,
                out int longestCorridorLength);
            int acceptablePreferenceOverrun = Mathf.Max(
                2,
                resolvedMinimumCorridorLength + resolvedCorridorLengthVariation);
            if (maximumPreferenceOverrun <= acceptablePreferenceOverrun)
            {
                result.MarkComplete();
                return result;
            }

            if (bestPhysicalResult == null ||
                maximumPreferenceOverrun < bestMaximumPreferenceOverrun ||
                maximumPreferenceOverrun == bestMaximumPreferenceOverrun &&
                longestCorridorLength < bestLongestCorridorLength)
            {
                bestPhysicalResult = result;
                bestMaximumPreferenceOverrun = maximumPreferenceOverrun;
                bestLongestCorridorLength = longestCorridorLength;
            }

            if (physicalCandidateCount >= qualityCandidateLimit)
                break;
        }

        if (bestPhysicalResult != null)
        {
            bestPhysicalResult.MarkComplete();
            return bestPhysicalResult;
        }

        failedResult.MarkFailed(
            $"Graph-first layout failed after {attemptCount} attempts. {lastFailure}");
        return failedResult;
    }

    private static int CalculateMaximumCorridorPreferenceOverrun(
        DungeonLayoutResult layout,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation,
        out int longestCorridorLength)
    {
        int maximumOverrun = 0;
        longestCorridorLength = 0;
        for (int connectionIndex = 0;
             connectionIndex < layout.Connections.Count;
             connectionIndex++)
        {
            DungeonSocketConnection connection = layout.Connections[connectionIndex];
            DungeonRoomPlacement first = FindPlacement(
                layout,
                connection.FirstRoomPlacementId);
            DungeonRoomPlacement second = FindPlacement(
                layout,
                connection.SecondRoomPlacementId);
            RoomSocketData firstSocket =
                first.Template.LayoutData.sockets[connection.FirstSocketIndex];
            bool horizontal = firstSocket.direction == RoomSocketDirection.Left ||
                firstSocket.direction == RoomSocketDirection.Right;
            int firstDepth = horizontal
                ? first.WorldBounds.width
                : first.WorldBounds.height;
            int secondDepth = horizontal
                ? second.WorldBounds.width
                : second.WorldBounds.height;
            int maximumPreferredLength = minimumCorridorLength + Mathf.CeilToInt(
                (firstDepth + secondDepth) * corridorLengthPerRoomCell) +
                corridorLengthVariation;
            maximumOverrun = Mathf.Max(
                maximumOverrun,
                connection.CorridorLength - maximumPreferredLength);
            longestCorridorLength = Mathf.Max(
                longestCorridorLength,
                connection.CorridorLength);
        }

        return maximumOverrun;
    }

    private static DungeonRoomPlacement FindPlacement(
        DungeonLayoutResult layout,
        int placementId)
    {
        for (int roomIndex = 0; roomIndex < layout.Rooms.Count; roomIndex++)
        {
            if (layout.Rooms[roomIndex].PlacementId == placementId)
                return layout.Rooms[roomIndex];
        }

        throw new InvalidOperationException(
            $"Layout connection references missing room placement {placementId}.");
    }

    private static bool ValidateGuaranteedRoomTemplates(
        RoomThemeLibrarySO library,
        IReadOnlyList<RoomTemplateSO> guaranteedRoomTemplates,
        out string failure)
    {
        if (guaranteedRoomTemplates == null || guaranteedRoomTemplates.Count == 0)
        {
            failure = string.Empty;
            return true;
        }

        var uniqueTemplates = new HashSet<RoomTemplateSO>();
        for (int templateIndex = 0; templateIndex < guaranteedRoomTemplates.Count; templateIndex++)
        {
            RoomTemplateSO template = guaranteedRoomTemplates[templateIndex];
            if (template == null)
            {
                failure = $"Guaranteed room entry {templateIndex} is missing.";
                return false;
            }

            if (!uniqueTemplates.Add(template))
            {
                failure = $"Guaranteed room '{template.name}' is listed more than once.";
                return false;
            }

            if (!library.ContainsRoom(template))
            {
                failure =
                    $"Guaranteed room '{template.name}' does not belong to library '{library.name}'.";
                return false;
            }

            RoomType roomType = template.LayoutData.roomType;
            if (roomType == RoomType.Start ||
                roomType == RoomType.Boss ||
                roomType == RoomType.Exit)
            {
                failure =
                    $"Guaranteed room '{template.name}' uses reserved role {roomType}. " +
                    "Only expansion room roles can be guaranteed.";
                return false;
            }

            if (!IsTemplateUsable(template))
            {
                failure = $"Guaranteed room '{template.name}' is not a usable room template.";
                return false;
            }
        }

        failure = string.Empty;
        return true;
    }

    private static int CountGuaranteedRoomType(
        IReadOnlyList<RoomTemplateSO> guaranteedRoomTemplates,
        RoomType roomType)
    {
        if (guaranteedRoomTemplates == null)
            return 0;

        int count = 0;
        for (int templateIndex = 0; templateIndex < guaranteedRoomTemplates.Count; templateIndex++)
        {
            RoomTemplateSO template = guaranteedRoomTemplates[templateIndex];
            if (template != null && template.LayoutData.roomType == roomType)
                count++;
        }

        return count;
    }

    private static bool ValidateRequiredCombatRoomRules(
        RoomThemeLibrarySO library,
        IReadOnlyList<RequiredCombatRoomRule> requiredCombatRoomRules,
        out string failure)
    {
        if (requiredCombatRoomRules == null || requiredCombatRoomRules.Count == 0)
        {
            failure = string.Empty;
            return true;
        }

        for (int ruleIndex = requiredCombatRoomRules.Count - 1; ruleIndex >= 0; ruleIndex--)
        {
            RequiredCombatRoomRule rule = requiredCombatRoomRules[ruleIndex];
            if (rule == null || rule.Count <= 0)
                continue;

            List<RoomTemplateSO> candidates = new();
            CollectRequiredCombatTemplateCandidates(
                library,
                rule,
                requiredDirections: null,
                excludedTemplates: null,
                candidates);
            if (candidates.Count > 0)
                continue;

            failure =
                $"Required Combat room rule {ruleIndex} has no matching template. " +
                $"Size={rule.SizeTag}, KillLock={rule.KillLockRewardTag}, Count={rule.Count}.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static int CountRequiredCombatRooms(
        IReadOnlyList<RoomTemplateSO> guaranteedRoomTemplates,
        IReadOnlyList<RequiredCombatRoomRule> requiredCombatRoomRules)
    {
        if (requiredCombatRoomRules == null)
            return 0;

        int count = 0;
        for (int ruleIndex = requiredCombatRoomRules.Count - 1; ruleIndex >= 0; ruleIndex--)
        {
            RequiredCombatRoomRule rule = requiredCombatRoomRules[ruleIndex];
            if (rule != null)
                count += Mathf.Max(
                    0,
                    rule.Count - CountGuaranteedCombatRoomsMatchingRule(
                        guaranteedRoomTemplates,
                        rule));
        }

        return count;
    }

    private static int CountGuaranteedCombatRoomsMatchingRule(
        IReadOnlyList<RoomTemplateSO> guaranteedRoomTemplates,
        RequiredCombatRoomRule rule)
    {
        if (guaranteedRoomTemplates == null || rule == null)
            return 0;

        int count = 0;
        for (int templateIndex = 0; templateIndex < guaranteedRoomTemplates.Count; templateIndex++)
        {
            RoomTemplateSO template = guaranteedRoomTemplates[templateIndex];
            if (template != null && rule.Matches(template))
                count++;
        }

        return count;
    }

    private static bool ValidateRoleBudget(
        DungeonLayoutPolicySO policy,
        int roomCount,
        IReadOnlyList<RoomTemplateSO> guaranteedRoomTemplates,
        IReadOnlyList<RequiredCombatRoomRule> requiredCombatRoomRules,
        out string failure)
    {
        int guaranteedTreasureCount = CountGuaranteedRoomType(
            guaranteedRoomTemplates,
            RoomType.Treasure);
        int guaranteedEventCount = CountGuaranteedRoomType(
            guaranteedRoomTemplates,
            RoomType.Event);
        int guaranteedShopCount = CountGuaranteedRoomType(
            guaranteedRoomTemplates,
            RoomType.Shop);
        int guaranteedCombatCount = CountGuaranteedRoomType(
            guaranteedRoomTemplates,
            RoomType.Combat);
        int requiredCombatRoomCount = CountRequiredCombatRooms(
            guaranteedRoomTemplates,
            requiredCombatRoomRules);
        int requiredRoomCount = 2 +
            Mathf.Max(policy.TreasureRoomCount, guaranteedTreasureCount) +
            Mathf.Max(policy.EventRoomCount, guaranteedEventCount) +
            Mathf.Max(policy.ShopRoomCount, guaranteedShopCount) +
            Mathf.Max(
                policy.MinimumCombatRoomCount,
                guaranteedCombatCount + requiredCombatRoomCount);
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
                topology.Nodes.Add(new PlannedNode
                {
                    GridPosition = firstDetour,
                    IsCycleDetour = true
                });
                int secondDetourIndex = topology.Nodes.Count;
                topology.Nodes.Add(new PlannedNode
                {
                    GridPosition = secondDetour,
                    IsCycleDetour = true
                });
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
        IReadOnlyList<RoomTemplateSO> guaranteedRoomTemplates,
        IReadOnlyList<RequiredCombatRoomRule> requiredCombatRoomRules,
        TopologyDraft topology,
        System.Random random,
        out string failure)
    {
        for (int nodeIndex = 0; nodeIndex < topology.Nodes.Count; nodeIndex++)
            topology.Nodes[nodeIndex].Role = RoomType.Combat;
        topology.Nodes[0].Role = RoomType.Start;
        topology.Nodes[topology.BossNodeIndex].Role = RoomType.Boss;

        List<int> assignedSpecialNodes = new();
        if (guaranteedRoomTemplates != null)
        {
            for (int templateIndex = 0; templateIndex < guaranteedRoomTemplates.Count; templateIndex++)
            {
                if (!TryAssignGuaranteedTemplate(
                        topology,
                        guaranteedRoomTemplates[templateIndex],
                        policy.PreferSpecialRoomsAtDeadEnds,
                        assignedSpecialNodes,
                        random,
                        out failure))
                {
                    return false;
                }
            }
        }

        if (!TryAssignRequiredCombatRoomRules(
                library,
                topology,
                requiredCombatRoomRules,
                guaranteedRoomTemplates,
                policy.PreferSpecialRoomsAtDeadEnds,
                assignedSpecialNodes,
                random,
                out failure))
        {
            return false;
        }

        int remainingTreasureCount = Mathf.Max(
            0,
            policy.TreasureRoomCount - CountGuaranteedRoomType(
                guaranteedRoomTemplates,
                RoomType.Treasure));
        int remainingEventCount = Mathf.Max(
            0,
            policy.EventRoomCount - CountGuaranteedRoomType(
                guaranteedRoomTemplates,
                RoomType.Event));
        int remainingShopCount = Mathf.Max(
            0,
            policy.ShopRoomCount - CountGuaranteedRoomType(
                guaranteedRoomTemplates,
                RoomType.Shop));
        if (!TryAssignSpecialRole(
                library,
                topology,
                RoomType.Treasure,
                remainingTreasureCount,
                policy.PreferSpecialRoomsAtDeadEnds,
                assignedSpecialNodes,
                random,
                out failure) ||
            !TryAssignSpecialRole(
                library,
                topology,
                RoomType.Event,
                remainingEventCount,
                policy.PreferSpecialRoomsAtDeadEnds,
                assignedSpecialNodes,
                random,
                out failure) ||
            !TryAssignSpecialRole(
                library,
                topology,
                RoomType.Shop,
                remainingShopCount,
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

    private static bool TryAssignRequiredCombatRoomRules(
        RoomThemeLibrarySO library,
        TopologyDraft topology,
        IReadOnlyList<RequiredCombatRoomRule> requiredCombatRoomRules,
        IReadOnlyList<RoomTemplateSO> guaranteedRoomTemplates,
        bool preferDeadEnds,
        List<int> assignedNodes,
        System.Random random,
        out string failure)
    {
        if (requiredCombatRoomRules == null || requiredCombatRoomRules.Count == 0)
        {
            failure = string.Empty;
            return true;
        }

        List<int> compatibleNodes = new();
        List<int> compatibleDeadEnds = new();
        List<RoomTemplateSO> templateCandidates = new();
        List<RoomSocketDirection> requiredDirections = new();
        for (int ruleIndex = requiredCombatRoomRules.Count - 1; ruleIndex >= 0; ruleIndex--)
        {
            RequiredCombatRoomRule rule = requiredCombatRoomRules[ruleIndex];
            if (rule == null || rule.Count <= 0)
                continue;

            int remainingCount = rule.Count - CountAssignedMatchingCombatRooms(topology, rule);
            for (int placementIndex = 0; placementIndex < remainingCount; placementIndex++)
            {
                compatibleNodes.Clear();
                compatibleDeadEnds.Clear();
                for (int nodeIndex = 1; nodeIndex < topology.Nodes.Count; nodeIndex++)
                {
                    if (nodeIndex == topology.BossNodeIndex ||
                        topology.Nodes[nodeIndex].Role != RoomType.Combat ||
                        topology.Nodes[nodeIndex].Template != null ||
                        assignedNodes.Contains(nodeIndex))
                    {
                        continue;
                    }

                    CollectRequiredDirections(topology, nodeIndex, requiredDirections);
                    templateCandidates.Clear();
                    CollectRequiredCombatTemplateCandidates(
                        library,
                        rule,
                        requiredDirections,
                        guaranteedRoomTemplates,
                        templateCandidates);
                    if (templateCandidates.Count == 0)
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
                        $"No unassigned Combat topology node can satisfy required rule {ruleIndex}. " +
                        $"Size={rule.SizeTag}, KillLock={rule.KillLockRewardTag}, Count={rule.Count}.";
                    return false;
                }

                int selectedNodeIndex = SelectSpreadNode(
                    topology,
                    candidates,
                    assignedNodes,
                    random);
                PlannedNode node = topology.Nodes[selectedNodeIndex];
                CollectRequiredDirections(topology, selectedNodeIndex, requiredDirections);
                templateCandidates.Clear();
                CollectRequiredCombatTemplateCandidates(
                    library,
                    rule,
                    requiredDirections,
                    guaranteedRoomTemplates,
                    templateCandidates);
                RemoveAdjacentDuplicateCandidatesIfPossible(
                    topology,
                    selectedNodeIndex,
                    templateCandidates);
                RoomTemplateSO selectedTemplate = SelectWeightedTemplate(
                    templateCandidates,
                    requiredDirections,
                    policy: null,
                    random);
                if (selectedTemplate == null)
                {
                    failure =
                        $"Required Combat rule {ruleIndex} selected node {selectedNodeIndex}, " +
                        "but no compatible template remained after filtering.";
                    return false;
                }

                node.Role = RoomType.Combat;
                node.Template = selectedTemplate;
                assignedNodes.Add(selectedNodeIndex);
            }
        }

        failure = string.Empty;
        return true;
    }

    private static int CountAssignedMatchingCombatRooms(
        TopologyDraft topology,
        RequiredCombatRoomRule rule)
    {
        if (topology == null || rule == null)
            return 0;

        int count = 0;
        for (int nodeIndex = 0; nodeIndex < topology.Nodes.Count; nodeIndex++)
        {
            RoomTemplateSO template = topology.Nodes[nodeIndex].Template;
            if (template != null && rule.Matches(template))
                count++;
        }

        return count;
    }

    private static bool TryAssignGuaranteedTemplate(
        TopologyDraft topology,
        RoomTemplateSO template,
        bool preferDeadEnds,
        List<int> assignedNodes,
        System.Random random,
        out string failure)
    {
        RoomTopologyPlacementData placementRule = template.LayoutData.topologyPlacement;
        int minimumDistance = Mathf.Max(0, placementRule.minimumGraphDistanceFromStart);
        List<int> compatibleNodes = new();
        List<int> compatibleDeadEnds = new();
        for (int nodeIndex = 1; nodeIndex < topology.Nodes.Count; nodeIndex++)
        {
            if (nodeIndex == topology.BossNodeIndex ||
                topology.Nodes[nodeIndex].Role != RoomType.Combat ||
                assignedNodes.Contains(nodeIndex))
            {
                continue;
            }

            List<RoomSocketDirection> requiredDirections = new();
            CollectRequiredDirections(topology, nodeIndex, requiredDirections);
            if (!IsTemplateCompatible(template, requiredDirections))
                continue;

            int graphDistance = CalculateGraphDistance(topology, 0, nodeIndex);
            bool isDeadEnd = GetNodeDegree(topology, nodeIndex) == 1;
            if (graphDistance < minimumDistance ||
                (placementRule.requireDeadEnd && !isDeadEnd) ||
                (placementRule.mode == RoomTopologyPlacementMode.CycleDetour &&
                 !topology.Nodes[nodeIndex].IsCycleDetour))
            {
                continue;
            }

            compatibleNodes.Add(nodeIndex);
            if (isDeadEnd)
                compatibleDeadEnds.Add(nodeIndex);
        }

        List<int> candidates = preferDeadEnds && compatibleDeadEnds.Count > 0
            ? compatibleDeadEnds
            : compatibleNodes;
        if (candidates.Count == 0)
        {
            failure =
                $"No unassigned topology node can use guaranteed template " +
                $"'{template.LayoutData.roomId}' with placement rule " +
                $"{placementRule.mode}, minimum distance {minimumDistance}, " +
                $"dead end required={placementRule.requireDeadEnd}.";
            return false;
        }

        int selectedNodeIndex = SelectGuaranteedNode(
            topology,
            candidates,
            assignedNodes,
            placementRule.mode,
            random);
        PlannedNode selectedNode = topology.Nodes[selectedNodeIndex];
        selectedNode.Role = template.LayoutData.roomType;
        selectedNode.Template = template;
        assignedNodes.Add(selectedNodeIndex);
        failure = string.Empty;
        return true;
    }

    private static int SelectGuaranteedNode(
        TopologyDraft topology,
        IReadOnlyList<int> candidates,
        IReadOnlyList<int> assignedSpecialNodes,
        RoomTopologyPlacementMode placementMode,
        System.Random random)
    {
        if (placementMode != RoomTopologyPlacementMode.FarthestFromStart)
            return SelectSpreadNode(topology, candidates, assignedSpecialNodes, random);

        int farthestDistance = int.MinValue;
        var farthestCandidates = new List<int>();
        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            int nodeIndex = candidates[candidateIndex];
            int graphDistance = CalculateGraphDistance(topology, 0, nodeIndex);
            if (graphDistance < farthestDistance)
                continue;

            if (graphDistance > farthestDistance)
            {
                farthestDistance = graphDistance;
                farthestCandidates.Clear();
            }

            farthestCandidates.Add(nodeIndex);
        }

        return SelectSpreadNode(topology, farthestCandidates, assignedSpecialNodes, random);
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
                    topology.Nodes[nodeIndex].Role != RoomType.Combat ||
                    assignedSpecialNodes.Contains(nodeIndex))
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
        DungeonLayoutPolicySO policy,
        IReadOnlyList<RoomTemplateSO> guaranteedRoomTemplates,
        IReadOnlyList<RequiredCombatRoomRule> requiredCombatRoomRules,
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
            RoomTemplateSO selectedTemplate = node.Template;
            if (selectedTemplate != null &&
                !IsTemplateCompatible(selectedTemplate, requiredDirections))
            {
                failure =
                    $"Guaranteed template '{selectedTemplate.LayoutData.roomId}' no longer supports " +
                    $"topology node {nodeIndex} directions [{FormatDirections(requiredDirections)}].";
                return false;
            }

            if (selectedTemplate == null)
            {
                candidates.Clear();
                library.CollectRooms(node.Role, candidates);
                for (int candidateIndex = candidates.Count - 1; candidateIndex >= 0; candidateIndex--)
                {
                    RoomTemplateSO candidate = candidates[candidateIndex];
                    if (ContainsTemplateReference(guaranteedRoomTemplates, candidate) ||
                        !IsTemplateCompatible(candidate, requiredDirections))
                    {
                        candidates.RemoveAt(candidateIndex);
                    }
                }

                if (node.Role == RoomType.Combat)
                {
                    RemoveQuotaExceededCombatCandidatesIfPossible(
                        topology,
                        requiredCombatRoomRules,
                        candidates);
                }

                RemoveAdjacentDuplicateCandidatesIfPossible(
                    topology,
                    nodeIndex,
                    candidates);
                selectedTemplate = SelectWeightedTemplate(
                    candidates,
                    requiredDirections,
                    policy,
                    random);
            }

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

    private static bool ContainsTemplateReference(
        IReadOnlyList<RoomTemplateSO> templates,
        RoomTemplateSO candidate)
    {
        if (templates == null || candidate == null)
            return false;

        for (int templateIndex = 0; templateIndex < templates.Count; templateIndex++)
        {
            if (templates[templateIndex] == candidate)
                return true;
        }

        return false;
    }

    private static void CollectRequiredCombatTemplateCandidates(
        RoomThemeLibrarySO library,
        RequiredCombatRoomRule rule,
        IReadOnlyList<RoomSocketDirection> requiredDirections,
        IReadOnlyList<RoomTemplateSO> excludedTemplates,
        List<RoomTemplateSO> results)
    {
        if (results == null)
            return;

        results.Clear();
        if (library == null || rule == null)
            return;

        List<RoomTemplateSO> candidates = new();
        library.CollectRooms(RoomType.Combat, candidates);
        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            RoomTemplateSO candidate = candidates[candidateIndex];
            if (ContainsTemplateReference(excludedTemplates, candidate) ||
                !rule.Matches(candidate))
            {
                continue;
            }

            if (requiredDirections != null &&
                !IsTemplateCompatible(candidate, requiredDirections))
            {
                continue;
            }

            results.Add(candidate);
        }
    }

    private static void RemoveQuotaExceededCombatCandidatesIfPossible(
        TopologyDraft topology,
        IReadOnlyList<RequiredCombatRoomRule> requiredCombatRoomRules,
        List<RoomTemplateSO> candidates)
    {
        if (topology == null ||
            requiredCombatRoomRules == null ||
            candidates == null ||
            candidates.Count <= 1)
        {
            return;
        }

        for (int ruleIndex = requiredCombatRoomRules.Count - 1; ruleIndex >= 0; ruleIndex--)
        {
            RequiredCombatRoomRule rule = requiredCombatRoomRules[ruleIndex];
            if (rule == null ||
                rule.Count <= 0 ||
                CountAssignedMatchingCombatRooms(topology, rule) < rule.Count)
            {
                continue;
            }

            int remainingCandidateCount = 0;
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                if (!rule.Matches(candidates[candidateIndex]))
                    remainingCandidateCount++;
            }

            if (remainingCandidateCount == 0)
                continue;

            for (int candidateIndex = candidates.Count - 1; candidateIndex >= 0; candidateIndex--)
            {
                if (rule.Matches(candidates[candidateIndex]))
                    candidates.RemoveAt(candidateIndex);
            }
        }
    }

    private static void RemoveAdjacentDuplicateCandidatesIfPossible(
        TopologyDraft topology,
        int nodeIndex,
        List<RoomTemplateSO> candidates)
    {
        if (topology == null || candidates == null || candidates.Count <= 1)
            return;

        int remainingCandidateCount = 0;
        for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            if (!HasAdjacentSelectedTemplate(topology, nodeIndex, candidates[candidateIndex]))
                remainingCandidateCount++;
        }

        if (remainingCandidateCount == 0)
            return;

        for (int candidateIndex = candidates.Count - 1; candidateIndex >= 0; candidateIndex--)
        {
            if (HasAdjacentSelectedTemplate(topology, nodeIndex, candidates[candidateIndex]))
                candidates.RemoveAt(candidateIndex);
        }
    }

    private static bool HasAdjacentSelectedTemplate(
        TopologyDraft topology,
        int nodeIndex,
        RoomTemplateSO candidate)
    {
        if (candidate == null)
            return false;

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

            if (IsSameRoomTemplate(topology.Nodes[neighborIndex].Template, candidate))
                return true;
        }

        return false;
    }

    private static bool IsSameRoomTemplate(RoomTemplateSO first, RoomTemplateSO second)
    {
        if (first == null || second == null)
            return false;

        if (first == second)
            return true;

        string firstId = first.LayoutData.roomId;
        string secondId = second.LayoutData.roomId;
        return !string.IsNullOrWhiteSpace(firstId) &&
               string.Equals(firstId, secondId, StringComparison.Ordinal);
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
        const int relaxationStepCount = 5;
        int physicalLayoutSeed = random.Next();
        string lastFailure = string.Empty;
        for (int stepIndex = 0; stepIndex < relaxationStepCount; stepIndex++)
        {
            float preferredExtraLengthScale = 1f -
                stepIndex / (float)(relaxationStepCount - 1);
            if (!TryCreatePhysicalLayoutAtScale(
                    seed,
                    requestedRoomCount,
                    topology,
                    new System.Random(physicalLayoutSeed),
                    minimumCorridorLength,
                    corridorLengthPerRoomCell,
                    corridorLengthVariation,
                    preferredExtraLengthScale,
                    out result,
                    out lastFailure))
            {
                continue;
            }

            if (stepIndex > 0)
                result.MarkCorridorLengthRelaxed();
            failure = string.Empty;
            return true;
        }

        result = null;
        failure =
            $"Physical layout failed after relaxing preferred corridor extras to zero. " +
            lastFailure;
        return false;
    }

    private static bool TryCreatePhysicalLayoutAtScale(
        int seed,
        int requestedRoomCount,
        TopologyDraft topology,
        System.Random random,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation,
        float preferredExtraLengthScale,
        out DungeonLayoutResult result,
        out string failure)
    {
        result = null;
        if (!TryResolveConstraintNodeBounds(
                topology,
                random,
                minimumCorridorLength,
                corridorLengthPerRoomCell,
                corridorLengthVariation,
                preferredExtraLengthScale,
                out failure))
        {
            return false;
        }

        DungeonLayoutResult builtResult = new(seed, requestedRoomCount);
        for (int nodeIndex = 0; nodeIndex < topology.Nodes.Count; nodeIndex++)
        {
            PlannedNode node = topology.Nodes[nodeIndex];
            builtResult.AddRoom(new DungeonRoomPlacement(
                nodeIndex,
                node.Template,
                node.Origin,
                node.WorldBounds,
                CalculateGraphDistance(topology, 0, nodeIndex),
                GetNodeDegree(topology, nodeIndex) == 1,
                node.IsCycleDetour));
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

    private static bool TryResolveConstraintNodeBounds(
        TopologyDraft topology,
        System.Random random,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation,
        float preferredExtraLengthScale,
        out string failure)
    {
        if (!TryCreateAxisConstraintLayout(
                topology,
                horizontal: true,
                minimumCorridorLength,
                corridorLengthPerRoomCell,
                corridorLengthVariation,
                preferredExtraLengthScale,
                random,
                out AxisConstraintLayout horizontalLayout,
                out failure) ||
            !TryCreateAxisConstraintLayout(
                topology,
                horizontal: false,
                minimumCorridorLength,
                corridorLengthPerRoomCell,
                corridorLengthVariation,
                preferredExtraLengthScale,
                random,
                out AxisConstraintLayout verticalLayout,
                out failure))
        {
            return false;
        }

        int iterationLimit = Mathf.Max(1, topology.Nodes.Count * topology.Nodes.Count + 1);
        for (int iteration = 0; iteration < iterationLimit; iteration++)
        {
            horizontalLayout.Solve();
            verticalLayout.Solve();
            AssignConstraintNodeBounds(topology, horizontalLayout, verticalLayout);

            bool foundOverlap = false;
            bool addedConstraint = false;
            for (int firstIndex = 0; firstIndex < topology.Nodes.Count; firstIndex++)
            {
                for (int secondIndex = firstIndex + 1;
                     secondIndex < topology.Nodes.Count;
                     secondIndex++)
                {
                    if (!topology.Nodes[firstIndex].WorldBounds.Overlaps(
                            topology.Nodes[secondIndex].WorldBounds))
                    {
                        continue;
                    }

                    foundOverlap = true;
                    addedConstraint |= TryAddRoomOverlapConstraint(
                        topology,
                        firstIndex,
                        secondIndex,
                        horizontalLayout,
                        verticalLayout);
                }
            }

            if (!foundOverlap)
            {
                failure = string.Empty;
                return true;
            }

            if (!addedConstraint)
            {
                failure =
                    "Room overlap could not be separated without breaking a required socket alignment.";
                return false;
            }
        }

        failure =
            $"Room overlap constraints did not converge within {iterationLimit} iterations.";
        return false;
    }

    private static bool TryCreateAxisConstraintLayout(
        TopologyDraft topology,
        bool horizontal,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation,
        float preferredExtraLengthScale,
        System.Random random,
        out AxisConstraintLayout layout,
        out string failure)
    {
        int nodeCount = topology.Nodes.Count;
        int[] parents = new int[nodeCount];
        for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            parents[nodeIndex] = nodeIndex;

        for (int edgeIndex = 0; edgeIndex < topology.Edges.Count; edgeIndex++)
        {
            PlannedEdge edge = topology.Edges[edgeIndex];
            PlannedNode first = topology.Nodes[edge.FirstNodeIndex];
            PlannedNode second = topology.Nodes[edge.SecondNodeIndex];
            RoomSocketDirection direction = GetDirection(
                first.GridPosition,
                second.GridPosition);
            if (direction == (RoomSocketDirection)(-1))
            {
                layout = null;
                failure = $"Topology edge {edgeIndex} does not connect cardinal-neighbor nodes.";
                return false;
            }

            bool sharesAxisAnchor = horizontal
                ? direction == RoomSocketDirection.Up || direction == RoomSocketDirection.Down
                : direction == RoomSocketDirection.Left || direction == RoomSocketDirection.Right;
            if (sharesAxisAnchor)
                UnionNodes(parents, edge.FirstNodeIndex, edge.SecondNodeIndex);
        }

        Dictionary<int, int> groupByRoot = new();
        List<int> logicalCoordinates = new();
        int[] groupByNode = new int[nodeCount];
        for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        {
            int root = FindRoot(parents, nodeIndex);
            if (!groupByRoot.TryGetValue(root, out int groupIndex))
            {
                groupIndex = logicalCoordinates.Count;
                groupByRoot.Add(root, groupIndex);
                logicalCoordinates.Add(ResolveLogicalCoordinate(
                    topology.Nodes[nodeIndex],
                    horizontal));
            }

            int logicalCoordinate = ResolveLogicalCoordinate(
                topology.Nodes[nodeIndex],
                horizontal);
            if (logicalCoordinates[groupIndex] != logicalCoordinate)
            {
                layout = null;
                failure =
                    $"A required socket alignment crosses multiple logical " +
                    $"{(horizontal ? "columns" : "rows")}.";
                return false;
            }

            groupByNode[nodeIndex] = groupIndex;
        }

        layout = new AxisConstraintLayout(groupByNode, logicalCoordinates.ToArray());
        for (int edgeIndex = 0; edgeIndex < topology.Edges.Count; edgeIndex++)
        {
            PlannedEdge edge = topology.Edges[edgeIndex];
            PlannedNode first = topology.Nodes[edge.FirstNodeIndex];
            PlannedNode second = topology.Nodes[edge.SecondNodeIndex];
            int firstCoordinate = ResolveLogicalCoordinate(first, horizontal);
            int secondCoordinate = ResolveLogicalCoordinate(second, horizontal);
            if (firstCoordinate == secondCoordinate)
                continue;

            int lowerNodeIndex = firstCoordinate < secondCoordinate
                ? edge.FirstNodeIndex
                : edge.SecondNodeIndex;
            int upperNodeIndex = firstCoordinate < secondCoordinate
                ? edge.SecondNodeIndex
                : edge.FirstNodeIndex;
            PlannedNode lowerNode = topology.Nodes[lowerNodeIndex];
            PlannedNode upperNode = topology.Nodes[upperNodeIndex];
            int preferredExtraLength = Mathf.CeilToInt(
                (ResolveAxisDepth(lowerNode, horizontal) +
                 ResolveAxisDepth(upperNode, horizontal)) *
                Mathf.Max(0f, corridorLengthPerRoomCell));
            if (corridorLengthVariation > 0)
                preferredExtraLength += random.Next(corridorLengthVariation + 1);
            int gap = Mathf.Max(0, minimumCorridorLength) + Mathf.CeilToInt(
                preferredExtraLength * Mathf.Clamp01(preferredExtraLengthScale));
            int separation = ResolvePositiveExtent(lowerNode, horizontal) +
                ResolveNegativeExtent(upperNode, horizontal) +
                1 +
                gap;
            layout.AddMinimumSeparation(
                layout.GetGroup(lowerNodeIndex),
                layout.GetGroup(upperNodeIndex),
                separation);
        }

        failure = string.Empty;
        return true;
    }

    private static void AssignConstraintNodeBounds(
        TopologyDraft topology,
        AxisConstraintLayout horizontalLayout,
        AxisConstraintLayout verticalLayout)
    {
        for (int nodeIndex = 0; nodeIndex < topology.Nodes.Count; nodeIndex++)
        {
            PlannedNode node = topology.Nodes[nodeIndex];
            node.Origin = new Vector2Int(
                horizontalLayout.GetAnchorForNode(nodeIndex) - node.ReferenceX,
                verticalLayout.GetAnchorForNode(nodeIndex) - node.ReferenceY);
            node.WorldBounds = new RectInt(
                node.LocalBounds.position + node.Origin,
                node.LocalBounds.size);
        }
    }

    private static bool TryAddRoomOverlapConstraint(
        TopologyDraft topology,
        int firstNodeIndex,
        int secondNodeIndex,
        AxisConstraintLayout horizontalLayout,
        AxisConstraintLayout verticalLayout)
    {
        bool hasHorizontalCandidate = TryResolveOverlapConstraintCandidate(
            topology,
            firstNodeIndex,
            secondNodeIndex,
            horizontal: true,
            horizontalLayout,
            out int horizontalLowerGroup,
            out int horizontalUpperGroup,
            out int horizontalSeparation,
            out int horizontalMovement);
        bool hasVerticalCandidate = TryResolveOverlapConstraintCandidate(
            topology,
            firstNodeIndex,
            secondNodeIndex,
            horizontal: false,
            verticalLayout,
            out int verticalLowerGroup,
            out int verticalUpperGroup,
            out int verticalSeparation,
            out int verticalMovement);

        if (hasHorizontalCandidate &&
            (!hasVerticalCandidate || horizontalMovement <= verticalMovement))
        {
            if (horizontalLayout.AddMinimumSeparation(
                    horizontalLowerGroup,
                    horizontalUpperGroup,
                    horizontalSeparation))
            {
                return true;
            }
        }

        return hasVerticalCandidate && verticalLayout.AddMinimumSeparation(
            verticalLowerGroup,
            verticalUpperGroup,
            verticalSeparation);
    }

    private static bool TryResolveOverlapConstraintCandidate(
        TopologyDraft topology,
        int firstNodeIndex,
        int secondNodeIndex,
        bool horizontal,
        AxisConstraintLayout layout,
        out int lowerGroup,
        out int upperGroup,
        out int separation,
        out int additionalMovement)
    {
        PlannedNode first = topology.Nodes[firstNodeIndex];
        PlannedNode second = topology.Nodes[secondNodeIndex];
        int firstCoordinate = ResolveLogicalCoordinate(first, horizontal);
        int secondCoordinate = ResolveLogicalCoordinate(second, horizontal);
        int firstGroup = layout.GetGroup(firstNodeIndex);
        int secondGroup = layout.GetGroup(secondNodeIndex);
        if (firstCoordinate == secondCoordinate || firstGroup == secondGroup)
        {
            lowerGroup = -1;
            upperGroup = -1;
            separation = 0;
            additionalMovement = int.MaxValue;
            return false;
        }

        bool firstIsLower = firstCoordinate < secondCoordinate;
        PlannedNode lowerNode = firstIsLower ? first : second;
        PlannedNode upperNode = firstIsLower ? second : first;
        lowerGroup = firstIsLower ? firstGroup : secondGroup;
        upperGroup = firstIsLower ? secondGroup : firstGroup;
        separation = ResolvePositiveExtent(lowerNode, horizontal) +
            ResolveNegativeExtent(upperNode, horizontal) +
            1;
        additionalMovement = separation -
            (layout.GetAnchorForGroup(upperGroup) - layout.GetAnchorForGroup(lowerGroup));
        return additionalMovement > 0;
    }

    private static int ResolveLogicalCoordinate(PlannedNode node, bool horizontal) =>
        horizontal ? node.GridPosition.x : node.GridPosition.y;

    private static int ResolveAxisDepth(PlannedNode node, bool horizontal) =>
        horizontal ? Mathf.Max(0, node.LocalBounds.width) : Mathf.Max(0, node.LocalBounds.height);

    private static int ResolveNegativeExtent(PlannedNode node, bool horizontal) =>
        horizontal
            ? node.ReferenceX - node.LocalBounds.xMin
            : node.ReferenceY - node.LocalBounds.yMin;

    private static int ResolvePositiveExtent(PlannedNode node, bool horizontal) =>
        horizontal
            ? node.LocalBounds.xMax - 1 - node.ReferenceX
            : node.LocalBounds.yMax - 1 - node.ReferenceY;

    private static int FindRoot(int[] parents, int nodeIndex)
    {
        while (parents[nodeIndex] != nodeIndex)
        {
            parents[nodeIndex] = parents[parents[nodeIndex]];
            nodeIndex = parents[nodeIndex];
        }

        return nodeIndex;
    }

    private static void UnionNodes(int[] parents, int firstNodeIndex, int secondNodeIndex)
    {
        int firstRoot = FindRoot(parents, firstNodeIndex);
        int secondRoot = FindRoot(parents, secondNodeIndex);
        if (firstRoot != secondRoot)
            parents[secondRoot] = firstRoot;
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
        IReadOnlyList<RoomSocketDirection> requiredDirections,
        DungeonLayoutPolicySO policy,
        System.Random random)
    {
        double totalWeight = 0d;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += CalculateSocketFitAdjustedWeight(
                candidates[i],
                requiredDirections,
                policy);
        if (totalWeight <= 0d)
            return null;

        double selectedWeight = random.NextDouble() * totalWeight;
        for (int i = 0; i < candidates.Count; i++)
        {
            selectedWeight -= CalculateSocketFitAdjustedWeight(
                candidates[i],
                requiredDirections,
                policy);
            if (selectedWeight <= 0d)
                return candidates[i];
        }

        return candidates.Count > 0 ? candidates[candidates.Count - 1] : null;
    }

    private static double CalculateSocketFitAdjustedWeight(
        RoomTemplateSO template,
        IReadOnlyList<RoomSocketDirection> requiredDirections,
        DungeonLayoutPolicySO policy)
    {
        if (template == null)
            return 0d;

        float baseWeight = template.LayoutData.selectionWeight;
        if (baseWeight <= 0f || float.IsNaN(baseWeight) || float.IsInfinity(baseWeight))
            return 0d;

        int extraDirectionCount = CountExtraValidSocketDirections(
            template.LayoutData,
            requiredDirections);
        double exactMatchMultiplier = policy != null
            ? policy.ExactSocketDirectionMatchWeightMultiplier
            : DefaultExactSocketDirectionMatchWeightMultiplier;
        double extraSocketMultiplier = policy != null
            ? policy.ExtraSocketDirectionWeightMultiplier
            : DefaultExtraSocketDirectionWeightMultiplier;
        double fitMultiplier = extraDirectionCount == 0
            ? exactMatchMultiplier
            : System.Math.Pow(extraSocketMultiplier, extraDirectionCount);
        return baseWeight * fitMultiplier;
    }

    private static int CountExtraValidSocketDirections(
        RoomLayoutData layout,
        IReadOnlyList<RoomSocketDirection> requiredDirections)
    {
        int extraDirectionCount = 0;
        RectInt bounds = ResolveLocalBounds(layout);
        for (int directionIndex = 0; directionIndex < AllDirections.Length; directionIndex++)
        {
            RoomSocketDirection direction = AllDirections[directionIndex];
            if (IsRequiredDirection(requiredDirections, direction))
                continue;

            if (HasValidSocketInDirection(layout, bounds, direction))
                extraDirectionCount++;
        }

        return extraDirectionCount;
    }

    private static bool HasValidSocketInDirection(
        RoomLayoutData layout,
        RectInt bounds,
        RoomSocketDirection direction)
    {
        if (layout.sockets == null)
            return false;

        for (int socketIndex = 0; socketIndex < layout.sockets.Count; socketIndex++)
        {
            RoomSocketData socket = layout.sockets[socketIndex];
            if (socket.direction == direction && RoomSocketGeometry.IsValid(socket, bounds))
                return true;
        }

        return false;
    }

    private static bool IsRequiredDirection(
        IReadOnlyList<RoomSocketDirection> requiredDirections,
        RoomSocketDirection direction)
    {
        if (requiredDirections == null)
            return false;

        for (int directionIndex = 0; directionIndex < requiredDirections.Count; directionIndex++)
        {
            if (requiredDirections[directionIndex] == direction)
                return true;
        }

        return false;
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

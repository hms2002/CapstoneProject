#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Exercises per-exit dead ends, Start depth balance, offset placement and constrained template selection without changing authored assets.</summary>
public sealed class DungeonTemplateSearchPlayModeTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type Assembler = typeof(DungeonGraphLayoutAssembler);
    private readonly List<Object> owned = new();
    private readonly List<RoomTemplateSO> templates = new();
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    [TestCase(10, 1, 10, 0, 0)]
    [TestCase(10, 2, 5, 5, 0)]
    [TestCase(10, 3, 2, 4, 4)]
    [TestCase(8, 3, 2, 3, 3)]
    public void StageComposition_AllocatesCountsRatherThanIndependentRolls(int total, int stage, int a, int b, int c)
    {
        CollectionAssert.AreEqual(new[] { a, b, c }, DungeonStageComposition.Allocate(total, stage, 17));
    }

    [Test]
    public void StageComposition_RoundingIsStableAndDoesNotAlwaysFavorOneTier()
    {
        var winners = new HashSet<int>();
        for (int seed = 0; seed < 64; seed++)
        {
            int[] counts = DungeonStageComposition.Allocate(7, 2, seed);
            Assert.That(counts.Sum(), Is.EqualTo(7));
            Assert.That(counts[2], Is.Zero);
            CollectionAssert.AreEqual(counts, DungeonStageComposition.Allocate(7, 2, seed));
            winners.Add(counts[0] > counts[1] ? 1 : 2);
        }
        Assert.That(winners.Count, Is.EqualTo(2));
    }

    [TestCase("Dragon", 1)] [TestCase("Dragon", 2)] [TestCase("Dragon", 3)]
    [TestCase("Shadow", 1)] [TestCase("Shadow", 2)] [TestCase("Shadow", 3)]
    [TestCase("Slime", 1)] [TestCase("Slime", 2)] [TestCase("Slime", 3)]
    public void ProductionStageComposition_PreservesExactQuotasAndCurrentLarge(string theme, int stage)
    {
        var profile = AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
            $"Assets/_Project/Data/Dungeon/GenerationProfiles/Procedural{theme}GenerationProfile.asset");
        for (int n = 0; n < 3; n++)
        {
            int seed = unchecked(profile.Seed + n * 997);
            var result = new DungeonGraphLayoutAssembler().Assemble(profile.RoomLibrary, profile.LayoutPolicy,
                seed, profile.RoomCount, profile.MaxPlacementAttemptsPerRoom, profile.MinimumCorridorLength,
                profile.CorridorLengthPerRoomCell, profile.CorridorLengthVariation,
                profile.GuaranteedRoomTemplates, generationStage: stage);
            Assert.That(result.IsComplete, Is.True, result.FailureReason);
            var combat = result.Rooms.Select(r => r.Template).Where(t => t.LayoutData.roomType == RoomType.Combat).ToArray();
            var large = combat.Where(t => RoomTemplateCombatMetadataUtility.ResolveSizeTag(t) == RoomCombatSizeTag.Large).ToArray();
            Assert.That(large.Length, Is.EqualTo(1));
            Assert.That(DungeonStageComposition.Tier(large[0]), Is.EqualTo(stage));
            var normal = combat.Where(t => RoomTemplateCombatMetadataUtility.ResolveSizeTag(t) == RoomCombatSizeTag.Normal).ToArray();
            var expected = DungeonStageComposition.Allocate(normal.Length, stage, seed);
            for (int tier = 1; tier <= 3; tier++)
                Assert.That(normal.Count(t => DungeonStageComposition.Tier(t) == tier), Is.EqualTo(expected[tier - 1]));
            CheckPhysicalConnections(result);
        }
    }
    [TearDown] public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear(); templates.Clear();
    }

    [TestCase(1)]
    [TestCase(5)]
    [TestCase(3)]
    [TestCase(7)]
    [TestCase(15)]
    public void StartDirections_AllConnectWithinRoomBudget(int mask)
    {
        Template("Start", RoomType.Start, mask: mask);
        Template("Boss", RoomType.Boss); Template("Treasure", RoomType.Treasure);
        Template("A"); Template("B"); Template("C"); Template("D");
        var library = TestLibrary();
        for (int seed = 0; seed < 8; seed++)
        {
            var result = new DungeonGraphLayoutAssembler().Assemble(library, Policy(), seed, 12, 512, 2, 0f, 0);
            Assert.That(result.IsComplete, Is.True, $"mask={mask}, seed={seed}: {result.FailureReason}");
            Assert.That(result.Rooms.Count, Is.EqualTo(12));
            CheckStartConnections(result);
            CheckPhysicalConnections(result);
        }
    }

    [Test]
    public void StartDirections_DuplicateAndInvalidSockets_DoNotAddBranches()
    {
        var start = Template("Start", RoomType.Start, mask: 5);
        var layout = start.LayoutData;
        layout.sockets.Add(new RoomSocketData { direction = RoomSocketDirection.Up, localCell = new(2, 6), width = 2 });
        layout.sockets.Add(new RoomSocketData { direction = RoomSocketDirection.Right, localCell = new(100, 100), width = 2 });
        start.EditorSetData(layout, start.BuildData);
        Template("Boss", RoomType.Boss); Template("Treasure", RoomType.Treasure); Template("Combat");
        var result = new DungeonGraphLayoutAssembler().Assemble(TestLibrary(), Policy(), 17, 12, 512, 2, 0f, 0);
        Assert.That(result.IsComplete, Is.True, result.FailureReason);
        CheckStartConnections(result);
        Assert.That(result.Connections.Count(c => c.FirstRoomPlacementId == 0 || c.SecondRoomPlacementId == 0), Is.EqualTo(2));
    }

    [Test]
    public void StartDirections_InsufficientBudgetFailsInsteadOfClosingSockets()
    {
        Template("Start", RoomType.Start); Template("Boss", RoomType.Boss);
        Template("Treasure", RoomType.Treasure); Template("Combat");
        var policy = Policy();
        Set(policy, "minimumBossGraphDistance", 2); Set(policy, "maximumBossGraphDistance", 2);
        Set(policy, "minimumMeaningfulBranches", 0);
        Set(policy, "minimumCycleConnections", 0); Set(policy, "maximumCycleConnections", 0);
        Set(policy, "treasureRoomCount", 0); Set(policy, "minimumCombatRoomCount", 0);
        // Four neighboring rooms alone need five nodes, before extending the boss path.
        var result = new DungeonGraphLayoutAssembler().Assemble(TestLibrary(), policy, 11, 4, 32, 2, 0f, 0);
        Assert.That(result.IsComplete, Is.False);
        Assert.That(result.Rooms, Is.Empty);
        StringAssert.Contains("Start", result.FailureReason);
    }

    [Test]
    public void StartDirections_MultipleStartsCannotGainUnusedSocketsDuringTemplateSearch()
    {
        var narrow = Template("NarrowStart", RoomType.Start, mask: 5);
        var wide = Template("WideStart", RoomType.Start);
        Template("Boss", RoomType.Boss); Template("Treasure", RoomType.Treasure);
        Template("A"); Template("B"); Template("C");
        var seen = new HashSet<RoomTemplateSO>();
        var library = TestLibrary();
        for (int seed = 0; seed < 16; seed++)
        {
            var result = new DungeonGraphLayoutAssembler().Assemble(library, Policy(), seed, 12, 512, 2, 0f, 0);
            Assert.That(result.IsComplete, Is.True, result.FailureReason);
            CheckStartConnections(result);
            seen.Add(result.Rooms[0].Template);
        }
        Assert.That(seen, Is.EquivalentTo(new[] { narrow, wide }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StartDirections_OffsetOppositeSocketsConnectWithoutChangingAuthoredCells(bool translatedBounds)
    {
        var start = Template("MisalignedStart", RoomType.Start);
        OffsetSockets(start, translatedBounds);
        var before = start.LayoutData.sockets.Select(s => s.localCell).ToArray();
        Template("Boss", RoomType.Boss); Template("Treasure", RoomType.Treasure); Template("Combat");
        var library = TestLibrary();
        for (int seed = 0; seed < 8; seed++)
        {
            var result = new DungeonGraphLayoutAssembler().Assemble(library, Policy(), seed, 12, 512, 2, 0f, 0);
            Assert.That(result.IsComplete, Is.True, result.FailureReason);
            Assert.That(result.Rooms.Count, Is.EqualTo(12));
            CheckStartConnections(result);
            CheckPhysicalConnections(result);
            Assert.That(start.LayoutData.sockets.Select(s => s.localCell), Is.EqualTo(before));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OffsetSockets_AccumulateAcrossStraightChains(bool vertical)
    {
        var start = Template("Start", RoomType.Start, mask: vertical ? 1 : 2);
        var boss = Template("Boss", RoomType.Boss, mask: vertical ? 4 : 8);
        for (int i = 0; i < 3; i++) OffsetSockets(Template($"Offset_{i}"), i % 2 == 0);
        var points = Enumerable.Range(0, 5).Select(i => vertical ? new Vector2Int(0, i) : new Vector2Int(i, 0)).ToArray();
        Assert.That(Solve(Search(Policy(), Topology(points), 8), out var result, out var failure), Is.True, failure);
        CheckPhysicalConnections(result);
        var firstCell = result.Rooms[0].Origin + start.LayoutData.sockets[0].localCell;
        var lastCell = result.Rooms[4].Origin + boss.LayoutData.sockets[0].localCell;
        // Each intervening room contributes -1 column upward or -2 rows rightward.
        Assert.That(vertical ? lastCell.x - firstCell.x : lastCell.y - firstCell.y, Is.EqualTo(vertical ? -3 : -6));
    }

    [Test]
    public void OffsetSockets_CloseCycleAndPreservePhysicalSeparation()
    {
        for (int i = 0; i < 6; i++) OffsetSockets(Template($"Offset_{i}"), i % 2 == 0);
        var points = new[] { new Vector2Int(0, 0), new(1, 0), new(2, 0), new(2, 1), new(1, 1), new(0, 1) };
        for (int seed = 0; seed < 8; seed++)
        {
            var topology = Topology(points, allCombat: true, closeCycle: true);
            Assert.That(Solve(Search(Policy(), topology, seed), out var result, out var failure), Is.True, failure);
            Assert.That(result.Connections.Count, Is.EqualTo(6));
            CheckPhysicalConnections(result);
        }
    }

    [Test]
    public void StartTerminals_SharedCycleTailCannotSatisfyTwoExits()
    {
        var topology = BranchedTopology(new[] { new Vector2Int(0, 0), new(1, 0), new(1, 1), new(0, 1), new(1, 2) },
            (0, 1), (1, 2), (2, 3), (3, 0), (2, 4));
        Assert.That(MissingTerminals(topology), Is.EqualTo(2));
    }

    [Test]
    public void StartTerminals_TwoSharedLeavesCanMatchTwoDistinctExits()
    {
        var topology = BranchedTopology(new[] { new Vector2Int(0, 0), new(1, 0), new(1, 1), new(0, 1), new(1, 2), new(2, 1) },
            (0, 1), (1, 2), (2, 3), (3, 0), (2, 4), (2, 5));
        Assert.That(MissingTerminals(topology), Is.Zero);
    }

    [Test]
    public void StartTerminals_StartCycleGetsSeparateTail_WithoutRemovingCycle()
    {
        var topology = StartCycleWithoutTerminal();
        Assert.That(MissingTerminals(topology), Is.EqualTo(1));
        object[] args = { topology, 9, 3, new System.Random(17), null };
        Assert.That(AddTerminals(args), Is.True, (string)args[4]);
        var nodes = (IList)Get(topology, "Nodes");
        Assert.That(nodes.Count, Is.EqualTo(9));
        Assert.That(((IList)Get(topology, "Edges")).Count - nodes.Count + 1, Is.EqualTo(1));
        Assert.That(Get(nodes[8], "BranchGroup"), Is.EqualTo(2));
        Assert.That(((int[])Get(Reach(topology), "ExitMasks"))[8], Is.EqualTo(3));
        Assert.That(((int[])Get(Reach(topology), "Depths"))[8], Is.EqualTo(3));
        Assert.That(MissingTerminals(topology), Is.Zero);
        // Rechecking already covered exits does not create another room or another branch.
        Assert.That(AddTerminals(args), Is.True);
        Assert.That(nodes.Count, Is.EqualTo(9));
    }

    [TestCase(8, 3)]
    [TestCase(9, 2)]
    public void StartTerminals_RejectsInsufficientRoomOrBranchBudget(int rooms, int branches)
    {
        var topology = StartCycleWithoutTerminal();
        object[] args = { topology, rooms, branches, new System.Random(17), null };
        Assert.That(AddTerminals(args), Is.False);
        Assert.That(((IList)Get(topology, "Nodes")).Count, Is.EqualTo(8));
        StringAssert.Contains("dead-end terminal", (string)args[4]);
    }

    [Test]
    public void StartTerminals_TailReservationPreservesAShortBossTerminal()
    {
        var topology = BranchedTopology(new[] { new Vector2Int(0, 0), new(1, 0), new(2, 0),
            new(0, 1), new(1, 1), new(-1, 0), new(0, -1) },
            (0, 1), (1, 2), (0, 3), (3, 4), (4, 1), (0, 5), (0, 6));
        Set(topology, "BossNodeIndex", 2);
        var nodes = (IList)Get(topology, "Nodes");
        Set(nodes[5], "BranchGroup", 0); Set(nodes[6], "BranchGroup", 1);
        object[] args = { topology, 8, 3, new System.Random(17), null };
        Assert.That(AddTerminals(args), Is.True, (string)args[4]);
        Assert.That(nodes.Count, Is.EqualTo(8));
        Assert.That(((IList)Get(topology, "Edges")).Count, Is.EqualTo(8));
        Assert.That(((int[])Get(Reach(topology), "Depths"))[7], Is.EqualTo(2));
        Assert.That(((int[])Get(Reach(topology), "ExitMasks"))[7], Is.EqualTo(1));
        Assert.That(MissingTerminals(topology), Is.Zero);
    }

    [Test]
    public void StartTerminals_MatchingLeavesSharedEndpointForTheConstrainedExit()
    {
        var topology = BranchedTopology(new[] { new Vector2Int(0, 0), new(1, 0), new(1, 1), new(0, 1),
            new(2, 1), new(0, 2), new(0, 3) },
            (0, 1), (1, 2), (2, 3), (3, 0), (2, 4), (3, 5), (5, 6));
        // Up can use either leaf; Right can only use the shared leaf. Node-order greedy matching loses Right.
        Assert.That(((int[])Get(Reach(topology), "ExitMasks"))[4], Is.EqualTo(3));
        Assert.That(((int[])Get(Reach(topology), "ExitMasks"))[6], Is.EqualTo(1));
        Assert.That(MissingTerminals(topology), Is.Zero);
    }

    [Test]
    public void StartTerminals_CyclesNeverUseTheBossEdge()
    {
        for (int seed = 0; seed < 32; seed++)
        {
            var topology = Line(7);
            object[] args = { topology, 1, new List<RoomSocketDirection> { RoomSocketDirection.Right }, false, new System.Random(seed) };
            Assert.That((bool)Assembler.GetMethod("TryAddCycleDetours", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True);
            Assert.That(((int[])Get(Reach(topology), "Degrees"))[6], Is.EqualTo(1));
        }
    }

    [Test]
    public void StartTerminals_FourExitsCannotBorrowTerminalBudgetFromACycle()
    {
        Template("Start", RoomType.Start); Template("Boss", RoomType.Boss);
        Template("Treasure", RoomType.Treasure); Template("Combat");
        var policy = Policy(); Set(policy, "maximumMeaningfulBranches", 2);
        var result = new DungeonGraphLayoutAssembler().Assemble(TestLibrary(), policy, 17, 12, 32, 2, 0f, 0);
        Assert.That(result.IsComplete, Is.False);
        Assert.That(result.Rooms, Is.Empty);
        StringAssert.Contains("dedicated dead-end terminal per exit", result.FailureReason);
    }

    private static object StartCycleWithoutTerminal()
    {
        var topology = BranchedTopology(new[] { new Vector2Int(0, 0), new(1, 0), new(2, 0), new(3, 0),
            new(0, 1), new(1, 1), new(-1, 0), new(0, -1) },
            (0, 1), (1, 2), (2, 3), (0, 4), (4, 5), (5, 1), (0, 6), (0, 7));
        Set(topology, "BossNodeIndex", 3);
        var nodes = (IList)Get(topology, "Nodes");
        Set(nodes[6], "BranchGroup", 0); Set(nodes[7], "BranchGroup", 1);
        return topology;
    }

    [Test]
    public void StartGrowth_ExtendsExistingBranchesWithoutCountingThemTwice()
    {
        var topology = StartCycleWithoutTerminal();
        Set(topology, "MinimumStartExitDepth", 2);
        object[] args = { topology, 11, 3, new System.Random(17), null };
        Assert.That(AddTerminals(args), Is.True, (string)args[4]);
        Assert.That(((IList)Get(topology, "Nodes")).Count, Is.EqualTo(11));
        Assert.That(MissingTerminals(topology), Is.Zero);
        object[] grow = { topology, 3, 0, new System.Random(17) };
        Assert.That((bool)Assembler.GetMethod("TryAddMeaningfulBranches", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, grow), Is.True);
    }

    [Test]
    public void StartGrowth_ReservesSpokeDepthBeforeLongerBossOrOptionalBranches()
    {
        var policy = Policy();
        for (int seed = 0; seed < 32; seed++)
        {
            object[] args = { policy, 12, 3, true, new System.Random(seed), null, null, null, null };
            Assert.That((bool)Assembler.GetMethod("TryResolveTopologyCounts", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True);
            Assert.That((int)args[5], Is.EqualTo(4));
            Assert.That((int)args[6], Is.EqualTo(3));
            Assert.That((int)args[7], Is.EqualTo(1));
        }
    }

    [Test]
    public void StartTerminals_StartCycleCannotUseASingleEdgeBossPath()
    {
        var topology = Line(2);
        object[] args = { topology, 1, new List<RoomSocketDirection> { RoomSocketDirection.Right, RoomSocketDirection.Up }, true, new System.Random(1) };
        Assert.That((bool)Assembler.GetMethod("TryAddCycleDetours", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.False);
        Assert.That(((int[])Get(Reach(topology), "Degrees"))[1], Is.EqualTo(1));
    }

    private static int MissingTerminals(object topology)
    {
        object reach = Reach(topology);
        return (int)reach.GetType().GetProperty("MissingTerminalMask").GetValue(reach);
    }

    private static bool AddTerminals(object[] args) => (bool)Assembler
        .GetMethod("TryAddMissingStartTerminals", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

    [Test]
    public void StartBalance_CycleSharesEqualShortestPathsButNotLongerDetours()
    {
        var topology = Topology(new[] { new Vector2Int(0, 0), new(1, 0), new(1, 1), new(0, 1) }, allCombat: true, closeCycle: true);
        object reach = Reach(topology);
        Assert.That(Get(reach, "Depths"), Is.EqualTo(new[] { 0, 1, 2, 1 }));
        Assert.That(Get(reach, "ExitMasks"), Is.EqualTo(new[] { 0, 2, 3, 1 }));
        Assert.That(Get(reach, "ExitRoomUnits"), Is.EqualTo(new[] { 18, 18, 0, 0 }));
        var node = Activator.CreateInstance(Assembler.GetNestedType("PlannedNode", BindingFlags.NonPublic), true);
        Set(node, "GridPosition", new Vector2Int(2, 0)); ((IList)Get(topology, "Nodes")).Add(node);
        ((IList)Get(topology, "Edges")).Add(Edge(1, 4));
        reach = Reach(topology);
        Assert.That(((int[])Get(reach, "ExitMasks"))[4], Is.EqualTo(2));
        Assert.That(Get(reach, "ExitRoomUnits"), Is.EqualTo(new[] { 18, 30, 0, 0 }));
        Assert.That(((int[])Get(reach, "ExitRoomUnits")).Sum(), Is.EqualTo(4 * 12));
    }

    [Test]
    public void StartBalance_FrontierOrderPrefersShallowThenSmallerRegion()
    {
        var topology = BranchedTopology(
            new[] { new Vector2Int(0, 0), new(1, 0), new(2, 0), new(3, 0), new(1, 1), new(-1, 0), new(-2, 0), new(0, -1) },
            (0, 1), (1, 2), (2, 3), (1, 4), (0, 5), (5, 6), (0, 7));
        for (int seed = 0; seed < 16; seed++)
        {
            var candidates = new List<int> { 3, 4, 6, 7 };
            OrderGrowth(topology, candidates, seed);
            Assert.That(candidates, Is.EqualTo(new[] { 7, 6, 4, 3 }));
        }
    }

    [Test]
    public void StartBalance_EqualGrowthPrioritiesRemainSeededAndVaried()
    {
        var topology = BranchedTopology(new[] { new Vector2Int(0, 0), new(1, 0), new(-1, 0) }, (0, 1), (0, 2));
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 16; seed++)
        {
            var first = new List<int> { 1, 2 }; var replay = new List<int> { 1, 2 };
            OrderGrowth(topology, first, seed); OrderGrowth(topology, replay, seed);
            Assert.That(first, Is.EqualTo(replay)); seen.Add(first[0]);
        }
        Assert.That(seen, Is.EquivalentTo(new[] { 1, 2 }));
    }

    [Test]
    public void StartBalance_BlockedShallowFrontierFallsBackWithinBudget()
    {
        var topology = BranchedTopology(new[] { new Vector2Int(0, 0), new(0, 1), new(1, 0), new(2, 0),
            new(2, 1), new(2, 2), new(2, 3), new(1, 3), new(0, 3), new(-1, 0), new(-2, 0), new(-2, 1) },
            (0, 1), (0, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 7), (7, 8), (0, 9), (9, 10), (10, 11));
        var nodes = (IList)Get(topology, "Nodes");
        Set(nodes[1], "BranchGroup", 0); Set(nodes[8], "BranchGroup", 1); Set(nodes[11], "BranchGroup", 2);
        object[] args = { topology, 3, 1, new System.Random(7) };
        Assert.That((bool)Assembler.GetMethod("TryAddMeaningfulBranches", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True);
        Assert.That(nodes.Count, Is.EqualTo(13));
        var lastEdge = ((IList)Get(topology, "Edges"))[^1];
        Assert.That(lastEdge.GetType().GetProperty("FirstNodeIndex").GetValue(lastEdge), Is.EqualTo(11));
        Assert.That(Get(nodes[12], "BranchGroup"), Is.EqualTo(2));
    }

    [Test]
    public void StartBalance_GrowthBudgetPreferenceRetainsAllHardValidBranchCounts()
    {
        var policy = Policy();
        Set(policy, "minimumBossGraphDistance", 3); Set(policy, "maximumBossGraphDistance", 3);
        Set(policy, "minimumCycleConnections", 0); Set(policy, "maximumCycleConnections", 0);
        var counts = new HashSet<int>();
        for (int seed = 0; seed < 64; seed++)
        {
            object[] args = { policy, 10, 2, false, new System.Random(seed), null, null, null, null };
            Assert.That((bool)Assembler.GetMethod("TryResolveTopologyCounts", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True);
            Assert.That((int)args[5], Is.EqualTo(3)); Assert.That((int)args[7], Is.Zero);
            counts.Add((int)args[6]);
        }
        Assert.That(counts, Is.EquivalentTo(new[] { 2, 3, 4 }));
    }

    [Test]
    public void StartBalance_NewBranchSitesRetainDistantOptionsForRequiredRooms()
    {
        var seen = new HashSet<int>();
        for (int seed = 0; seed < 32; seed++)
        {
            var topology = Line(5);
            object[] args = { topology, 1, 1, new System.Random(seed) };
            Assert.That((bool)Assembler.GetMethod("TryAddMeaningfulBranches", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args), Is.True);
            var edge = ((IList)Get(topology, "Edges"))[^1];
            seen.Add((int)edge.GetType().GetProperty("FirstNodeIndex").GetValue(edge));
        }
        Assert.That(seen, Is.EquivalentTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void StartBalance_ScorePrefersEvenDepthThenEvenRoomLoad()
    {
        Type type = Assembler.GetNestedType("StartDepthBalance", BindingFlags.NonPublic);
        object Score(int[] depths, int[] sizes) => Activator.CreateInstance(type, Fields, null, new object[] { depths, sizes }, null);
        var uneven = Score(new[] { 6, 3, 1, 1 }, new[] { 60, 24, 12, 12 });
        var even = Score(new[] { 6, 2, 2, 1 }, new[] { 60, 24, 12, 12 });
        Assert.That((int)type.GetMethod("CompareTo").Invoke(even, new[] { uneven }), Is.LessThan(0));
        var loaded = Score(new[] { 6, 2, 2, 1 }, new[] { 72, 12, 12, 12 });
        Assert.That((int)type.GetMethod("CompareTo").Invoke(even, new[] { loaded }), Is.LessThan(0));
    }

    private static object Reach(object topology) => Activator.CreateInstance(
        Assembler.GetNestedType("StartBranchReach", BindingFlags.NonPublic), Fields, null, new[] { topology }, null);

    private static void OrderGrowth(object topology, List<int> candidates, int seed) => Assembler
        .GetMethod("OrderExpansionNodes", BindingFlags.Static | BindingFlags.NonPublic)
        .Invoke(null, new object[] { topology, candidates, new System.Random(seed) });

    private static object BranchedTopology(Vector2Int[] points, params (int first, int second)[] links)
    {
        var topology = Topology(points, allCombat: true);
        var edges = (IList)Get(topology, "Edges"); edges.Clear();
        foreach (var link in links) edges.Add(Edge(link.first, link.second));
        return topology;
    }

    [Test]
    public void GuaranteedRoles_ReserveScarceDistantDeadEndBeforeFlexibleShop()
    {
        Template("Start", RoomType.Start); Template("Boss", RoomType.Boss); Template("Combat");
        var shop = Template("FlexibleShop", RoomType.Shop);
        var parcel = Template("DistantEvent", RoomType.Event);
        var layout = parcel.LayoutData;
        layout.topologyPlacement = new RoomTopologyPlacementData
            { minimumGraphDistanceFromStart = 3, requireDeadEnd = true, mode = RoomTopologyPlacementMode.FarthestFromStart };
        parcel.EditorSetData(layout, parcel.BuildData);
        var topology = Line(5);
        var nodes = (IList)Get(topology, "Nodes");
        var edges = (IList)Get(topology, "Edges");
        Vector2Int[] branches = { new(2, 1), new(0, 1) };
        for (int i = 0; i < branches.Length; i++)
        {
            var node = Activator.CreateInstance(Assembler.GetNestedType("PlannedNode", BindingFlags.NonPublic), true);
            Set(node, "GridPosition", branches[i]);
            nodes.Add(node); edges.Add(Edge(i == 0 ? 2 : 0, 5 + i));
        }
        var policy = Policy(); Set(policy, "treasureRoomCount", 0); Set(policy, "minimumCombatRoomCount", 0);
        object[] args = { TestLibrary(), policy, new[] { shop, parcel }, null, topology, new System.Random(0), null };
        bool success = (bool)Assembler.GetMethod("TryAssignRoomRoles", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        Assert.That(success, Is.True, args[6] as string);
        Assert.That(Get(nodes[5], "Template"), Is.SameAs(parcel));
        Assert.That(Get(nodes[6], "Template"), Is.SameAs(shop));
    }

    [Test]
    public void ScarceSocketNode_IsAssignedBeforeHighWeightFlexibleNeighbor()
    {
        var start = Template("Start", RoomType.Start);
        var boss = Template("Boss", RoomType.Boss);
        var a = Template("A", weight: 1000000f);
        var b = Template("B", mask: 9);
        var points = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) };
        for (int seed = 0; seed < 16; seed++)
        {
            var search = Search(Policy(), Topology(points), seed);
            Assert.That(Solve(search, out var result, out var failure), Is.True, failure);
            Assert.That(result.Rooms[1].Template, Is.SameAs(b));
            Assert.That(result.Rooms[2].Template, Is.SameAs(a));
            Assert.That(result.TemplateSelection.RelaxationPhase, Is.Zero);
        }
    }

    [Test]
    public void ForwardChecking_ActuallyBacktracksBeforeRelaxation()
    {
        var a = Template("A", weight: 1000000f); var b = Template("B");
        var c = Template("C"); var d = Template("D");
        var points = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1) };
        var topology = Topology(points, allCombat: true, closeCycle: true);
        var search = Search(Policy(distance: 1), topology, 7);
        // A at #0 forces C -> D around the square, leaving #3 with neither A nor D.
        // B at #0 has a solution. Equal MRV domains force a genuine rollback test.
        var domains = (Array)Get(search, "domains");
        RoomTemplateSO[][] allowed = { new[] { a, b }, new[] { a, c }, new[] { c, d }, new[] { a, d } };
        for (int i = 0; i < 4; i++)
        {
            var list = (IList)domains.GetValue(i);
            for (int j = list.Count - 1; j >= 0; j--)
                if (!allowed[i].Contains((RoomTemplateSO)Get(list[j], "Template"))) list.RemoveAt(j);
        }
        var ties = (double[])Get(search, "tieBreaks");
        for (int i = 0; i < ties.Length; i++) ties[i] = i;
        Assert.That(Solve(search, out var result, out var failure), Is.True, failure);
        Assert.That(result.Rooms[0].Template, Is.SameAs(b));
        Assert.That(result.TemplateSelection.RelaxationPhase, Is.Zero);
        Assert.That(result.TemplateSelection.SearchSteps, Is.GreaterThan(points.Length));
        Assert.That(result.TemplateSelection.Metrics.IsRepeatFree, Is.True);
    }

    [Test]
    public void RequiredCombatSlot_DoesNotPinItsProvisionalTemplate()
    {
        Template("Start", RoomType.Start); Template("Boss", RoomType.Boss);
        var a = Template("A", reward: true, weight: 1000000f);
        var b = Template("B", mask: 9, reward: true);
        var points = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(2, 1) };
        object topology = Topology(points);
        var rule = new RequiredCombatRoomRule(RoomCombatSizeTag.Normal, RoomKillLockRewardTag.Present, 2);
        var node = ((IList)Get(topology, "Nodes"))[1];
        Set(node, "Template", a);
        var search = Search(Policy(), topology, 4, rules: new[] { rule });
        Assert.That(Solve(search, out var result, out var failure), Is.True, failure);
        Assert.That(result.Rooms[1].Template, Is.SameAs(b));
        Assert.That(result.Rooms.Count(r => rule.Matches(r.Template)), Is.EqualTo(2));
    }

    [Test]
    public void Quota_IsRedistributedAcrossCombatNodes_NotPinnedToProvisionalSlots()
    {
        Template("Start", RoomType.Start); Template("Boss", RoomType.Boss);
        var plain = Template("Plain"); var reward = Template("Reward", reward: true);
        var topology = Line(5);
        var nodes = (IList)Get(topology, "Nodes");
        Set(nodes[1], "Template", reward); Set(nodes[2], "Template", reward);
        var rule = new RequiredCombatRoomRule(RoomCombatSizeTag.Normal, RoomKillLockRewardTag.Present, 2);
        Assert.That(Solve(Search(Policy(distance: 1), topology, 2, rules: new[] { rule }), out var result, out var failure), Is.True, failure);
        Assert.That(result.Rooms[1].Template, Is.SameAs(reward));
        Assert.That(result.Rooms[2].Template, Is.SameAs(plain));
        Assert.That(result.Rooms[3].Template, Is.SameAs(reward));
        Assert.That(result.TemplateSelection.RelaxationPhase, Is.Zero);
    }

    [Test]
    public void GuaranteedTemplate_IsNeverReplacedOrDuplicated()
    {
        Template("Start", RoomType.Start); Template("Boss", RoomType.Boss);
        var a = Template("A"); var b = Template("B"); var c = Template("C");
        var topology = Line(5);
        Set(((IList)Get(topology, "Nodes"))[2], "Template", a);
        var search = Search(Policy(), topology, 3, guaranteed: new[] { a });
        Assert.That(Solve(search, out var result, out var failure), Is.True, failure);
        Assert.That(result.Rooms[2].Template, Is.SameAs(a));
        Assert.That(result.Rooms.Count(r => r.Template == a), Is.EqualTo(1));
    }

    [Test]
    public void DistanceTwoRepeats_RelaxBeforeAdjacentRepeats()
    {
        Template("Start", RoomType.Start); Template("Boss", RoomType.Boss);
        Template("A"); Template("B");
        Assert.That(Solve(Search(Policy(), Line(5), 9), out var result, out var failure), Is.True, failure);
        Assert.That(result.TemplateSelection.RelaxationPhase, Is.EqualTo(1));
        Assert.That(result.TemplateSelection.Metrics.AdjacentTemplates, Is.Zero);
        Assert.That(result.TemplateSelection.Metrics.NearbyTemplates, Is.EqualTo(1));
    }

    [Test]
    public void SameShapeRepeats_RelaxBeforeAdjacentIdenticalRoom()
    {
        Template("Start", RoomType.Start); Template("Boss", RoomType.Boss);
        var tag = Own(ScriptableObject.CreateInstance<RoomShapeTagSO>());
        Template("A", shape: tag); Template("B", shape: tag); Template("C", shape: tag);
        Assert.That(Solve(Search(Policy(), Line(5), 12), out var result, out var failure), Is.True, failure);
        Assert.That(result.TemplateSelection.RelaxationPhase, Is.EqualTo(2));
        Assert.That(result.TemplateSelection.Metrics.AdjacentTemplates, Is.Zero);
        Assert.That(result.TemplateSelection.Metrics.AdjacentShapes, Is.EqualTo(2));
    }

    [Test]
    public void UnavoidableRepeatedRoom_IsAllowedLast_AndExplained()
    {
        Template("Start", RoomType.Start); Template("Boss", RoomType.Boss); Template("A");
        Assert.That(Solve(Search(Policy(), Line(5), 5), out var result, out var failure), Is.True, failure);
        Assert.That(result.TemplateSelection.RelaxationPhase, Is.EqualTo(3));
        Assert.That(result.TemplateSelection.Metrics.AdjacentTemplates, Is.EqualTo(2));
        Assert.That(result.TemplateSelection.Description, Does.Contain("no non-repeating pair"));
        Assert.That(result.TemplateSelection.SearchSteps, Is.LessThanOrEqualTo(4096));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void HardCounts_NeverRelaxEvenWhenOnlyOneTemplateRemains(bool large)
    {
        Template("Start", RoomType.Start); Template("Boss", RoomType.Boss);
        Template("Only", large: large, reward: true);
        var rules = large ? null : new[] { new RequiredCombatRoomRule(RoomCombatSizeTag.Normal, RoomKillLockRewardTag.Present, 1) };
        Assert.That(Solve(Search(Policy(), Line(5), 1, rules: rules), out _, out var failure), Is.False);
        Assert.That(failure, Does.Contain("without relaxing hard constraints"));
    }

    [Test]
    public void Quality_PrioritizesAdjacencyBeforeDistanceTwo()
    {
        Assert.That(new DungeonTemplateRepetitionMetrics(0, 5, 9, 9).CompareTo(new DungeonTemplateRepetitionMetrics(1, 0, 0, 0)), Is.LessThan(0));
        Assert.That(new DungeonTemplateRepetitionMetrics(0, 0, 9, 9).CompareTo(new DungeonTemplateRepetitionMetrics(0, 1, 0, 0)), Is.LessThan(0));
    }

    [TestCase("Shadow")]
    [TestCase("Dragon")]
    [TestCase("Slime")]
    public void ProductionPolicy_RequiresOnlyOneLargeRoom_WithoutRewardFilter(string theme)
    {
        var profile = AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
            $"Assets/_Project/Data/Dungeon/GenerationProfiles/Procedural{theme}GenerationProfile.asset");
        var policy = AssetDatabase.LoadAssetAtPath<DungeonLayoutPolicySO>(
            "Assets/_Project/Data/Dungeon/Libraries/ExplorationCorridorPrototypePolicy.asset");
        Assert.That(policy, Is.Not.Null);
        Assert.That(profile.LayoutPolicy, Is.SameAs(policy));
        Assert.That(policy.MaximumLargeCombatRoomCount, Is.EqualTo(1));
        Assert.That(policy.RequiredCombatRoomRules.Count, Is.EqualTo(1));
        var rule = policy.RequiredCombatRoomRules[0];
        Assert.That(rule.SizeTag, Is.EqualTo(RoomCombatSizeTag.Large));
        Assert.That(rule.KillLockRewardTag, Is.EqualTo(RoomKillLockRewardTag.Auto));
        Assert.That(rule.Count, Is.EqualTo(1));
        Assert.That(rule.Matches(Template("LargeNone", large: true)), Is.True);
        Assert.That(rule.Matches(Template("LargePresent", large: true, reward: true)), Is.True);
        Assert.That(rule.Matches(Template("NormalPresent", reward: true)), Is.False);
    }

    [TestCase("Shadow")]
    [TestCase("Dragon")]
    [TestCase("Slime")]
    public void ProductionSeedSweep_AllCombatRewardsNone_StillPlacesExactlyOneLarge(string theme)
    {
        var profile = AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
            $"Assets/_Project/Data/Dungeon/GenerationProfiles/Procedural{theme}GenerationProfile.asset");
        var library = Own(ScriptableObject.CreateInstance<RoomThemeLibrarySO>());
        var copies = new Dictionary<RoomTemplateSO, RoomTemplateSO>();
        foreach (var source in profile.RoomLibrary.Rooms)
        {
            if (source == null || copies.ContainsKey(source)) continue;
            // Clone before changing metadata so production assets and other tests remain untouched.
            var copy = Own(Object.Instantiate(source));
            var layout = copy.LayoutData;
            if (layout.roomType == RoomType.Combat)
                layout.combatMetadata.killLockRewardTag = RoomKillLockRewardTag.None;
            copy.EditorSetData(layout, copy.BuildData);
            copies.Add(source, copy);
            library.EditorAddRoom(copy);
        }
        var guaranteed = profile.GuaranteedRoomTemplates.Select(t => copies[t]).ToArray();
        for (int n = 0; n < 3; n++)
        {
            int seed = unchecked(profile.Seed + n * 997);
            var result = new DungeonGraphLayoutAssembler().Assemble(library, profile.LayoutPolicy,
                seed, profile.RoomCount, profile.MaxPlacementAttemptsPerRoom, profile.MinimumCorridorLength,
                profile.CorridorLengthPerRoomCell, profile.CorridorLengthVariation, guaranteed);
            Assert.That(result.IsComplete, Is.True, $"{theme} seed={seed}: {result.FailureReason}");
            Assert.That(result.Rooms.Count, Is.EqualTo(profile.RoomCount));
            var combat = result.Rooms.Where(r => r.Template.LayoutData.roomType == RoomType.Combat).ToArray();
            Assert.That(combat.Count(r => RoomTemplateCombatMetadataUtility.ResolveSizeTag(r.Template) ==
                RoomCombatSizeTag.Large), Is.EqualTo(1));
            Assert.That(combat.All(r => RoomTemplateCombatMetadataUtility.ResolveKillLockRewardTag(r.Template) ==
                RoomKillLockRewardTag.None), Is.True);
            foreach (var template in guaranteed)
                Assert.That(result.Rooms.Count(r => r.Template == template), Is.EqualTo(1));
            TestContext.WriteLine($"{theme} seed={seed}: all Combat reward tags None, exactly one Large.");
        }
    }

    [TestCase("Shadow")]
    [TestCase("Dragon")]
    [TestCase("Slime")]
    public void ProductionSeedSweep_PreservesHardRules_AndReproducesSeed(string theme)
    {
        var profile = AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
            $"Assets/_Project/Data/Dungeon/GenerationProfiles/Procedural{theme}GenerationProfile.asset");
        var clock = Stopwatch.StartNew();
        for (int n = 0; n < 32; n++)
        {
            int seed = unchecked(profile.Seed + n * 997);
            DungeonLayoutResult Build() => new DungeonGraphLayoutAssembler().Assemble(profile.RoomLibrary, profile.LayoutPolicy,
                seed, profile.RoomCount, profile.MaxPlacementAttemptsPerRoom, profile.MinimumCorridorLength,
                profile.CorridorLengthPerRoomCell, profile.CorridorLengthVariation, profile.GuaranteedRoomTemplates);
            var perSeed = Stopwatch.StartNew();
            var result = Build();
            perSeed.Stop();
            Assert.That(result.IsComplete, Is.True, $"{theme} seed={seed}: {result.FailureReason}");
            Assert.That(result.Rooms.Count, Is.EqualTo(profile.RoomCount));
            Assert.That(result.Rooms.Count(r => r.Template.LayoutData.roomType == RoomType.Combat &&
                RoomTemplateCombatMetadataUtility.ResolveSizeTag(r.Template) == RoomCombatSizeTag.Large), Is.LessThanOrEqualTo(profile.LayoutPolicy.MaximumLargeCombatRoomCount));
            foreach (var rule in profile.LayoutPolicy.RequiredCombatRoomRules)
                if (rule != null && rule.Count > 0) Assert.That(result.Rooms.Count(r => rule.Matches(r.Template)), Is.EqualTo(rule.Count));
            foreach (var guaranteed in profile.GuaranteedRoomTemplates)
                Assert.That(result.Rooms.Count(r => r.Template == guaranteed), Is.EqualTo(1));
            CheckStartConnections(result);
            CheckPhysicalConnections(result);
            Assert.That(result.BossGraphDistance, Is.InRange(profile.LayoutPolicy.MinimumBossGraphDistance, profile.LayoutPolicy.MaximumBossGraphDistance));
            Assert.That(result.MeaningfulBranchCount, Is.InRange(profile.LayoutPolicy.MinimumMeaningfulBranches, profile.LayoutPolicy.MaximumMeaningfulBranches));
            Assert.That(result.CycleConnectionCount, Is.InRange(profile.LayoutPolicy.MinimumCycleConnections, profile.LayoutPolicy.MaximumCycleConnections));
            Assert.That(result.TemplateSelection.SearchSteps, Is.LessThanOrEqualTo(4096));
            if (n == 0)
            {
                var repeat = Build();
                Assert.That(Signature(repeat), Is.EqualTo(Signature(result)));
                Assert.That(repeat.TemplateSelection.Description, Is.EqualTo(result.TemplateSelection.Description));
            }
            TestContext.WriteLine($"{theme} seed={seed}: {result.TemplateSelection.Metrics}, phase={result.TemplateSelection.RelaxationPhase}, steps={result.TemplateSelection.SearchSteps}, elapsed={perSeed.ElapsedMilliseconds}ms");
            var balance = MeasureStartDepthBalance(result);
            TestContext.WriteLine($"StartDepth theme={theme} seed={seed} spread={balance.spread} depthEnergy={balance.depthEnergy} sizeEnergy={balance.sizeEnergy} depths={balance.depths}");
            if (result.TemplateSelection.Metrics.AdjacentTemplates > 0)
                TestContext.WriteLine(result.TemplateSelection.Description);
        }
        TestContext.WriteLine($"{theme}: 32 seeds + 1 replay in {clock.ElapsedMilliseconds} ms (Editor/headless, not player-frame profiling).");
    }

    private static string Signature(DungeonLayoutResult result) => string.Join("|", result.Rooms.Select(r => $"{r.PlacementId}:{r.Template.LayoutData.roomId}:{r.Origin}"));

    // Independently assigns equal credit to exits on equally short paths, using one BFS per exit.
    private static (int spread, long depthEnergy, long sizeEnergy, string depths) MeasureStartDepthBalance(DungeonLayoutResult result)
    {
        int n = result.Rooms.Count;
        var neighbors = Enumerable.Range(0, n).Select(_ => new List<int>()).ToArray();
        foreach (var c in result.Connections)
        {
            neighbors[c.FirstRoomPlacementId].Add(c.SecondRoomPlacementId);
            neighbors[c.SecondRoomPlacementId].Add(c.FirstRoomPlacementId);
        }
        int[] Bfs(int root, bool skipStart)
        {
            var distance = Enumerable.Repeat(-1, n).ToArray();
            var queue = new Queue<int>(); queue.Enqueue(root); distance[root] = 0;
            while (queue.Count > 0)
            {
                int node = queue.Dequeue();
                foreach (int next in neighbors[node])
                    if ((!skipStart || next != 0) && distance[next] < 0)
                    { distance[next] = distance[node] + 1; queue.Enqueue(next); }
            }
            return distance;
        }
        int[] depth = Bfs(0, false);
        var roots = neighbors[0];
        var fromRoots = roots.Select(root => Bfs(root, true)).ToArray();
        var maxima = new int[roots.Count]; var units = new int[roots.Count];
        var terminals = Enumerable.Range(0, roots.Count).Select(_ => new List<(int node, int depth)>()).ToArray();
        for (int node = 1; node < n; node++)
        {
            Assert.That(result.Rooms[node].GraphDistanceFromStart, Is.EqualTo(depth[node]));
            var owners = Enumerable.Range(0, roots.Count).Where(r => fromRoots[r][node] >= 0 && fromRoots[r][node] + 1 == depth[node]).ToArray();
            Assert.That(owners, Is.Not.Empty);
            foreach (int owner in owners) { maxima[owner] = Math.Max(maxima[owner], depth[node]); units[owner] += 12 / owners.Length; }
            if (neighbors[node].Count == 1)
            {
                Assert.That(DungeonReturnPortalPlacement.TryGetOnlyConnection(result, node, out _), Is.True);
                foreach (int owner in owners) terminals[owner].Add((node, depth[node]));
            }
        }
        // Independent backtracking oracle: each exit needs a different farthest leaf, even on shared cycles.
        var used = new HashSet<int>();
        bool AssignTerminal(int exit)
        {
            if (exit == roots.Count) return true;
            foreach (var terminal in terminals[exit])
            {
                if (terminal.depth != maxima[exit] || terminal.depth < Math.Min(2, result.BossGraphDistance) || !used.Add(terminal.node)) continue;
                if (AssignTerminal(exit + 1)) return true;
                used.Remove(terminal.node);
            }
            return false;
        }
        Assert.That(AssignTerminal(0), Is.True, "Each Start exit needs a distinct degree-one terminal at its greatest graph depth.");
        long depthEnergy = 0, sizeEnergy = 0;
        for (int i = 0; i < roots.Count; i++)
            for (int j = i + 1; j < roots.Count; j++)
            { long d = maxima[i] - maxima[j], s = units[i] - units[j]; depthEnergy += d * d; sizeEnergy += s * s; }
        return (maxima.Max() - maxima.Min(), depthEnergy, sizeEnergy, string.Join(",", maxima.OrderBy(x => x)));
    }

    [TestCase(RoomTopologyPlacementMode.Default, true, true)]
    [TestCase(RoomTopologyPlacementMode.FarthestFromStart, true, true)]
    [TestCase(RoomTopologyPlacementMode.CycleDetour, false, true)]
    [TestCase(RoomTopologyPlacementMode.CycleDetour, true, false)]
    public void TopologyPlacement_RejectsOnlyContradictoryCycleDeadEnd(
        RoomTopologyPlacementMode mode, bool deadEnd, bool expected)
    {
        var placement = new RoomTopologyPlacementData { mode = mode, requireDeadEnd = deadEnd };
        Assert.That(placement.TryValidate(out _), Is.EqualTo(expected));
    }

    [Test]
    public void GuaranteedCycleDeadEnd_FailsBeforeTopologyRetryLoop()
    {
        Template("Start", RoomType.Start);
        Template("Boss", RoomType.Boss);
        var invalid = Template("ContradictoryEvent", RoomType.Event);
        var layout = invalid.LayoutData;
        layout.topologyPlacement = new RoomTopologyPlacementData
        {
            mode = RoomTopologyPlacementMode.CycleDetour,
            minimumGraphDistanceFromStart = 2,
            requireDeadEnd = true
        };
        invalid.EditorSetData(layout, invalid.BuildData);
        var library = Own(ScriptableObject.CreateInstance<RoomThemeLibrarySO>());
        foreach (var template in templates) library.EditorAddRoom(template);
        var result = new DungeonGraphLayoutAssembler().Assemble(
            library, Policy(), 17, 12, 512, 2, 0f, 0, new[] { invalid });
        Assert.That(result.Rooms, Is.Empty);
        StringAssert.Contains("ContradictoryEvent", result.FailureReason);
        StringAssert.Contains("CycleDetour cannot require a dead end", result.FailureReason);
        StringAssert.DoesNotContain("after 512 attempts", result.FailureReason);
    }

    [TestCase("Shadow")]
    [TestCase("Dragon")]
    [TestCase("Slime")]
    public void ProductionRuntimeEventPlans_PlaceEveryStartEventAndFollowUp(string theme)
    {
        var profile = AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
            $"Assets/_Project/Data/Dungeon/GenerationProfiles/Procedural{theme}GenerationProfile.asset");
        Assert.That(profile.RunMapEventProfile, Is.Not.Null);
        var route = AssetDatabase.FindAssets("t:CorridorBossRouteSetSO")
            .Select(g => AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(AssetDatabase.GUIDToAssetPath(g)))
            .First(r => r.MatchesCorridorScene($"Procedural{theme}Corridor"));
        var manager = GamePlayDataManager.EnsureInstance();
        var routes = PortalRouteManager.EnsureInstance();
        var eventProfile = Own(Object.Instantiate(profile.RunMapEventProfile));
        eventProfile.EditorConfigure(profile.RunMapEventProfile.EventDefinitions, 1,
            profile.RunMapEventProfile.PlannedBossRouteVisitCount, false);
        int checkedLayouts = 0;
        try
        {
            foreach (var definition in eventProfile.EventDefinitions)
            {
                manager.ResetForDevelopmentStart();
                manager.StartRun();
                routes.ClearPlan();
                Assert.That(routes.ActivateSceneConnectionRouteContext(route, route.CorridorSceneName), Is.True);
                for (int visit = 1; visit < definition.MinimumBossRouteVisitOrder; visit++)
                    manager.Data.visitedRunMapEventRouteThemeIds.Add($"test_previous_route_{visit}");
                eventProfile.EditorSetGuaranteedStartEvents(new[] { definition });
                for (int n = 0; n < 3; n++)
                {
                    int seed = unchecked(profile.Seed + n * 997);
                    var plan = RunMapEventGenerationResolver.CreatePlan(eventProfile, profile.GuaranteedRoomTemplates, seed);
                    Assert.That(plan.GuaranteedRoomTemplates, Does.Contain(definition.EventRoomTemplate));
                    CheckEventLayout(profile, seed, plan.GuaranteedRoomTemplates);
                    checkedLayouts++;
                }

                foreach (var followUp in definition.FollowUps)
                {
                    if (followUp == null || !followUp.IsConfigured) continue;
                    var guaranteed = new List<RoomTemplateSO>(profile.GuaranteedRoomTemplates);
                    if (!guaranteed.Contains(followUp.RoomTemplate)) guaranteed.Add(followUp.RoomTemplate);
                    CheckEventLayout(profile, profile.Seed, guaranteed);
                    checkedLayouts++;
                    if (ParcelDeliveryPointInteractable.IsDeliveryRoom(followUp.RoomTemplate))
                    {
                        guaranteed.Add(definition.EventRoomTemplate);
                        CheckEventLayout(profile, profile.Seed, guaranteed);
                        checkedLayouts++;
                    }
                }
            }
            Assert.That(checkedLayouts, Is.GreaterThan(0));
            TestContext.WriteLine($"{theme}: {checkedLayouts} runtime start-event / follow-up layouts passed.");
        }
        finally
        {
            routes.ClearPlan();
            manager.ResetForDevelopmentStart();
        }
    }

    private static void CheckEventLayout(DungeonGenerationProfileSO profile, int seed, IReadOnlyList<RoomTemplateSO> guaranteed)
    {
        var result = new DungeonGraphLayoutAssembler().Assemble(profile.RoomLibrary, profile.LayoutPolicy,
            seed, profile.RoomCount, profile.MaxPlacementAttemptsPerRoom, profile.MinimumCorridorLength,
            profile.CorridorLengthPerRoomCell, profile.CorridorLengthVariation, guaranteed);
        Assert.That(result.IsComplete, Is.True, $"{profile.name}, seed={seed}: {result.FailureReason}");
        Assert.That(result.Rooms.Count, Is.EqualTo(profile.RoomCount));
        CheckStartConnections(result);
        CheckPhysicalConnections(result);
        Assert.That(result.Rooms.Count(r => r.Template.LayoutData.roomType == RoomType.Event &&
            !ParcelDeliveryPointInteractable.IsDeliveryRoom(r.Template)),
            Is.GreaterThanOrEqualTo(profile.LayoutPolicy.EventRoomCount),
            "Parcel destinations must not consume the regular event-room quota.");
        foreach (var rule in profile.LayoutPolicy.RequiredCombatRoomRules)
            if (rule != null && rule.Count > 0) Assert.That(result.Rooms.Count(r => rule.Matches(r.Template)), Is.EqualTo(rule.Count));
        Assert.That(result.Rooms.Count(r => r.Template.LayoutData.roomType == RoomType.Combat &&
            RoomTemplateCombatMetadataUtility.ResolveSizeTag(r.Template) == RoomCombatSizeTag.Large),
            Is.LessThanOrEqualTo(profile.LayoutPolicy.MaximumLargeCombatRoomCount));
        foreach (var template in guaranteed)
        {
            var rooms = result.Rooms.Where(r => r.Template == template).ToArray();
            Assert.That(rooms.Length, Is.EqualTo(1), template.name);
            if (template.LayoutData.topologyPlacement.requireDeadEnd)
                Assert.That(result.Connections.Count(c => c.FirstRoomPlacementId == rooms[0].PlacementId ||
                    c.SecondRoomPlacementId == rooms[0].PlacementId), Is.EqualTo(1), template.name);
        }
    }

    [TestCase("Shadow", 1)] [TestCase("Shadow", 2)] [TestCase("Shadow", 3)]
    [TestCase("Dragon", 1)] [TestCase("Dragon", 2)] [TestCase("Dragon", 3)]
    [TestCase("Slime", 1)] [TestCase("Slime", 2)] [TestCase("Slime", 3)]
    public void Recovery_DeliveryAndAlarmPreserveAllRequiredRooms(string theme, int stage)
    {
        var profile = AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
            $"Assets/_Project/Data/Dungeon/GenerationProfiles/Procedural{theme}GenerationProfile.asset");
        var guaranteed = new List<RoomTemplateSO>(profile.GuaranteedRoomTemplates);
        foreach (string suffix in new[] { "ParcelDeliveryPoint", "AlarmBell" })
        {
            var room = AssetDatabase.LoadAssetAtPath<RoomTemplateSO>(
                $"Assets/_Project/Data/Dungeon/Rooms/BossThemes/{theme}/{theme}_Event_{suffix}.asset");
            Assert.That(room, Is.Not.Null, suffix);
            guaranteed.Add(room);
        }
        string policyBefore = EditorJsonUtility.ToJson(profile.LayoutPolicy);
        var roomBefore = guaranteed.Select(room => EditorJsonUtility.ToJson(room)).ToArray();
        for (int n = 0; n < 3; n++)
        {
            int seed = unchecked(profile.Seed + n * 997);
            var result = new DungeonGraphLayoutAssembler().Assemble(profile.RoomLibrary, profile.LayoutPolicy,
                seed, profile.RoomCount, profile.MaxPlacementAttemptsPerRoom, profile.MinimumCorridorLength,
                profile.CorridorLengthPerRoomCell, profile.CorridorLengthVariation, guaranteed, generationStage: stage);
            Assert.That(result.IsComplete, Is.True, $"{theme}, stage={stage}, seed={seed}: {result.FailureReason}");
            Assert.That(result.Rooms.Count, Is.EqualTo(profile.RoomCount));
            foreach (var template in guaranteed)
            {
                var rooms = result.Rooms.Where(r => r.Template == template).ToArray();
                Assert.That(rooms.Length, Is.EqualTo(1), template.name);
                if (template.LayoutData.topologyPlacement.requireDeadEnd)
                    Assert.That(rooms[0].IsDeadEnd, Is.True, template.name);
            }
            CheckStartConnections(result);
            CheckPhysicalConnections(result);
            var large = result.Rooms.Where(r => r.Template.LayoutData.roomType == RoomType.Combat &&
                RoomTemplateCombatMetadataUtility.ResolveSizeTag(r.Template) == RoomCombatSizeTag.Large).ToArray();
            Assert.That(large.Length, Is.EqualTo(1));
            Assert.That(DungeonStageComposition.Tier(large[0].Template), Is.EqualTo(stage));
            TestContext.WriteLine($"{theme}, stage={stage}, seed={seed}, recovery={result.RecoveryLevel}");
        }
        Assert.That(EditorJsonUtility.ToJson(profile.LayoutPolicy), Is.EqualTo(policyBefore));
        CollectionAssert.AreEqual(roomBefore, guaranteed.Select(room => EditorJsonUtility.ToJson(room)).ToArray());
    }

    [Test]
    public void Recovery_TreeFallbackIsDeterministicAndKeepsRequiredDeadEnd()
    {
        Template("Start", RoomType.Start); Template("Boss", RoomType.Boss);
        Template("Treasure", RoomType.Treasure); Template("A"); Template("B");
        var required = Template("Delivery", RoomType.Event);
        var data = required.LayoutData;
        data.topologyPlacement = new RoomTopologyPlacementData
        {
            mode = RoomTopologyPlacementMode.FarthestFromStart,
            minimumGraphDistanceFromStart = 100,
            requireDeadEnd = true
        };
        required.EditorSetData(data, required.BuildData);
        var cycleEvent = Template("Cycle event", RoomType.Event);
        var cycleData = cycleEvent.LayoutData;
        cycleData.topologyPlacement = new RoomTopologyPlacementData
        {
            mode = RoomTopologyPlacementMode.CycleDetour,
            minimumGraphDistanceFromStart = 100
        };
        cycleEvent.EditorSetData(cycleData, cycleEvent.BuildData);
        var policy = Policy();
        Set(policy, "minimumCycleConnections", 0); Set(policy, "maximumCycleConnections", 0);
        var library = TestLibrary();
        DungeonLayoutResult Build(bool recover) => new DungeonGraphLayoutAssembler().Assemble(
            library, policy, 17, 12, 32, 2, 0f, 0, new[] { required, cycleEvent }, allowRecovery: recover);
        Assert.That(Build(false).IsComplete, Is.False);
        var first = Build(true);
        var second = Build(true);
        Assert.That(first.IsComplete, Is.True, first.FailureReason);
        Assert.That(first.RecoveryLevel, Is.EqualTo(2));
        Assert.That(first.CycleConnectionCount, Is.Zero);
        Assert.That(first.Rooms.Single(r => r.Template == required).IsDeadEnd, Is.True);
        Assert.That(first.Rooms.Count(r => r.Template == cycleEvent), Is.EqualTo(1));
        CollectionAssert.AreEqual(first.Rooms.Select(r => (r.Template, r.Origin)).ToArray(),
            second.Rooms.Select(r => (r.Template, r.Origin)).ToArray());
        CheckStartConnections(first);
        CheckPhysicalConnections(first);
        Assert.That(required.LayoutData.topologyPlacement.minimumGraphDistanceFromStart, Is.EqualTo(100));
    }

    [Test]
    public void Recovery_MissingLibraryIsNotAReadyScene()
    {
        var host = Own(new GameObject("Failed generator"));
        var generator = host.AddComponent<DungeonGenerator>();
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
            "DungeonGenerator requires a RoomThemeLibrarySO, directly or through its generation profile.");
        Assert.That(generator.Generate(), Is.False);
        Assert.That(generator.HasCompletedInitialGeneration, Is.True);
        Assert.That(generator.IsSceneEntryReady, Is.False);
        Assert.That(generator.SceneEntryFailure, Is.Not.Empty);
    }

    [Test]
    public void Recovery_SceneTransitionWaitsUntilRetryIsReady()
    {
        var host = Own(new GameObject("Entry readiness test"));
        var generator = host.AddComponent<DungeonGenerator>();
        const string expectedError = "DungeonGenerator requires a RoomThemeLibrarySO, directly or through its generation profile.";
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error, expectedError);
        generator.Generate();
        var coordinator = SceneTransitionCoordinator.EnsureInstance();
        var wait = (IEnumerator)typeof(SceneTransitionCoordinator)
            .GetMethod("WaitForSceneContentReady", Fields)
            .Invoke(coordinator, new object[] { host.scene });
        try
        {
            Assert.That(wait.MoveNext(), Is.True, "Allow Start callbacks first.");
            Assert.That(wait.MoveNext(), Is.True, "Failure must keep the transition covered.");
            Set(coordinator, "retrySceneEntryRequested", true);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, expectedError);
            Assert.That(wait.MoveNext(), Is.True, "A failed retry must not unlock the player.");
            Set(generator, "<LastGenerationSucceeded>k__BackingField", true);
            Assert.That(wait.MoveNext(), Is.False, "Ready content releases the gate.");
        }
        finally { (wait as IDisposable)?.Dispose(); }
    }

    private RoomThemeLibrarySO TestLibrary()
    {
        var library = Own(ScriptableObject.CreateInstance<RoomThemeLibrarySO>());
        foreach (var template in templates) library.EditorAddRoom(template);
        return library;
    }

    private static void CheckStartConnections(DungeonLayoutResult result)
    {
        var start = result.Rooms.Single(r => r.Template.LayoutData.roomType == RoomType.Start);
        var layout = start.Template.LayoutData;
        var expected = layout.sockets.Where(s => RoomSocketGeometry.IsValid(s, layout.localBounds))
            .Select(s => s.direction).Distinct().ToArray();
        var actual = result.Connections.Where(c => c.FirstRoomPlacementId == start.PlacementId || c.SecondRoomPlacementId == start.PlacementId)
            .Select(c => layout.sockets[c.FirstRoomPlacementId == start.PlacementId ? c.FirstSocketIndex : c.SecondSocketIndex].direction).ToArray();
        Assert.That(actual, Is.EquivalentTo(expected), $"{start.Template.name}: every unique usable Start direction must connect exactly once.");
        _ = MeasureStartDepthBalance(result);
    }

    private static void CheckPhysicalConnections(DungeonLayoutResult result)
    {
        for (int i = 0; i < result.Rooms.Count; i++)
            for (int j = i + 1; j < result.Rooms.Count; j++)
                Assert.That(result.Rooms[i].WorldBounds.Overlaps(result.Rooms[j].WorldBounds), Is.False);
        for (int i = 0; i < result.Connections.Count; i++)
        {
            var c = result.Connections[i];
            var first = result.Rooms.Single(r => r.PlacementId == c.FirstRoomPlacementId);
            var second = result.Rooms.Single(r => r.PlacementId == c.SecondRoomPlacementId);
            var a = first.Template.LayoutData.sockets[c.FirstSocketIndex];
            var b = second.Template.LayoutData.sockets[c.SecondSocketIndex];
            Assert.That(((int)a.direction + 2) % 4, Is.EqualTo((int)b.direction));
            Assert.That(RoomSocketGeometry.ResolveWidth(a), Is.EqualTo(RoomSocketGeometry.ResolveWidth(b)));
            var aCell = first.Origin + a.localCell;
            var bCell = second.Origin + b.localCell;
            bool horizontal = a.direction == RoomSocketDirection.Left || a.direction == RoomSocketDirection.Right;
            Assert.That(horizontal ? aCell.y : aCell.x, Is.EqualTo(horizontal ? bCell.y : bCell.x));
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
            Assert.That(bCell - aCell, Is.EqualTo(directions[(int)a.direction] * (c.CorridorLength + 1)));
            if (c.CorridorLength <= 0) continue;
            foreach (var room in result.Rooms) Assert.That(c.CorridorBounds.Overlaps(room.WorldBounds), Is.False);
            for (int j = i + 1; j < result.Connections.Count; j++)
                if (result.Connections[j].CorridorLength > 0)
                    Assert.That(c.CorridorBounds.Overlaps(result.Connections[j].CorridorBounds), Is.False);
        }
    }

    private static void OffsetSockets(RoomTemplateSO template, bool translatedBounds)
    {
        var layout = template.LayoutData;
        var down = layout.sockets[2]; down.localCell = new Vector2Int(3, 0); layout.sockets[2] = down;
        var left = layout.sockets[3]; left.localCell = new Vector2Int(0, 4); layout.sockets[3] = left;
        if (translatedBounds)
        {
            var shift = new Vector2Int(-4, -7);
            layout.localBounds.position += shift;
            for (int i = 0; i < layout.sockets.Count; i++)
            {
                var socket = layout.sockets[i]; socket.localCell += shift; layout.sockets[i] = socket;
            }
        }
        template.EditorSetData(layout, template.BuildData);
    }

    private RoomTemplateSO Template(string id, RoomType role = RoomType.Combat, int mask = 15,
        bool large = false, bool reward = false, float weight = 1f, RoomShapeTagSO shape = null)
    {
        var template = Own(ScriptableObject.CreateInstance<RoomTemplateSO>()); template.name = id;
        var sockets = new List<RoomSocketData>();
        Vector2Int[] cells = { new(2, 6), new(6, 2), new(2, 0), new(0, 2) };
        for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0)
            sockets.Add(new RoomSocketData { direction = (RoomSocketDirection)i, localCell = cells[i], width = 2 });
        template.EditorSetData(new RoomLayoutData { roomId = id, roomType = role, size = new Vector2Int(7, 7),
            localBounds = new RectInt(0, 0, 7, 7), sockets = sockets, selectionWeight = weight,
            shapeTag = shape != null ? shape : Own(ScriptableObject.CreateInstance<RoomShapeTagSO>()),
            combatMetadata = new RoomCombatMetadata { sizeTag = large ? RoomCombatSizeTag.Large : RoomCombatSizeTag.Normal,
                killLockRewardTag = reward ? RoomKillLockRewardTag.Present : RoomKillLockRewardTag.None } }, new RoomBuildData());
        templates.Add(template); return template;
    }

    private DungeonLayoutPolicySO Policy(int distance = 2)
    {
        var policy = Own(ScriptableObject.CreateInstance<DungeonLayoutPolicySO>());
        Set(policy, "templateRepeatAvoidanceGraphDistance", distance);
        return policy;
    }

    private object Line(int count) => Topology(Enumerable.Range(0, count).Select(i => new Vector2Int(i, 0)).ToArray());
    private static object Topology(Vector2Int[] points, bool allCombat = false, bool closeCycle = false)
    {
        Type type = Assembler.GetNestedType("TopologyDraft", BindingFlags.NonPublic);
        object topology = Activator.CreateInstance(type, true);
        var nodes = (IList)Get(topology, "Nodes"); var edges = (IList)Get(topology, "Edges");
        var main = (IList)Get(topology, "MainPathNodeIndices");
        for (int i = 0; i < points.Length; i++)
        {
            object node = Activator.CreateInstance(Assembler.GetNestedType("PlannedNode", BindingFlags.NonPublic), true);
            Set(node, "GridPosition", points[i]); Set(node, "IsMainPath", true);
            Set(node, "Role", allCombat ? RoomType.Combat : i == 0 ? RoomType.Start : i == points.Length - 1 ? RoomType.Boss : RoomType.Combat);
            nodes.Add(node); main.Add(i);
            if (i > 0) edges.Add(Edge(i - 1, i));
        }
        if (closeCycle) edges.Add(Edge(points.Length - 1, 0));
        Set(topology, "BossNodeIndex", points.Length - 1);
        return topology;
    }
    private static object Edge(int a, int b) => Activator.CreateInstance(Assembler.GetNestedType("PlannedEdge", BindingFlags.NonPublic), Fields, null, new object[] { a, b, false }, null);
    private object Search(DungeonLayoutPolicySO policy, object topology, int seed,
        IReadOnlyList<RoomTemplateSO> guaranteed = null, IReadOnlyList<RequiredCombatRoomRule> rules = null)
    {
        var library = Own(ScriptableObject.CreateInstance<RoomThemeLibrarySO>());
        foreach (var template in templates) library.EditorAddRoom(template);
        return Activator.CreateInstance(Assembler.GetNestedType("TemplateSearch", BindingFlags.NonPublic), Fields, null,
            new object[] { library, policy, guaranteed, rules, topology, new System.Random(seed), seed,
                ((IList)Get(topology, "Nodes")).Count, 2, 0f, 0, 0 }, null);
    }
    private static bool Solve(object search, out DungeonLayoutResult result, out string failure)
    {
        object[] args = { null, null };
        bool success = (bool)search.GetType().GetMethod("TrySolve", Fields).Invoke(search, args);
        result = (DungeonLayoutResult)args[0]; failure = (string)args[1]; return success;
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
}
#endif

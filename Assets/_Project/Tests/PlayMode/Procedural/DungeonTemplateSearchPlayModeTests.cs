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

/// <summary>Exercises constrained template assignment, rollback, relaxation, hard quotas and production-seed geometry without changing authored assets.</summary>
public sealed class DungeonTemplateSearchPlayModeTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Type Assembler = typeof(DungeonGraphLayoutAssembler);
    private readonly List<Object> owned = new();
    private readonly List<RoomTemplateSO> templates = new();
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    [TearDown] public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear(); templates.Clear();
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
    public void ProductionSeedSweep_PreservesHardRules_AndReproducesSeed(string theme)
    {
        var profile = AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
            $"Assets/_Project/Data/Dungeon/GenerationProfiles/Procedural{theme}GenerationProfile.asset");
        var clock = Stopwatch.StartNew();
        for (int n = 0; n < 8; n++)
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
            Assert.That(result.TemplateSelection.SearchSteps, Is.LessThanOrEqualTo(4096));
            if (n == 0)
            {
                var repeat = Build();
                Assert.That(Signature(repeat), Is.EqualTo(Signature(result)));
                Assert.That(repeat.TemplateSelection.Description, Is.EqualTo(result.TemplateSelection.Description));
            }
            TestContext.WriteLine($"{theme} seed={seed}: {result.TemplateSelection.Metrics}, phase={result.TemplateSelection.RelaxationPhase}, steps={result.TemplateSelection.SearchSteps}, elapsed={perSeed.ElapsedMilliseconds}ms");
            if (result.TemplateSelection.Metrics.AdjacentTemplates > 0)
                TestContext.WriteLine(result.TemplateSelection.Description);
        }
        TestContext.WriteLine($"{theme}: 8 seeds + 1 replay in {clock.ElapsedMilliseconds} ms (Editor/headless, not player-frame profiling).");
    }

    private static string Signature(DungeonLayoutResult result) => string.Join("|", result.Rooms.Select(r => $"{r.PlacementId}:{r.Template.LayoutData.roomId}:{r.Origin}"));

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
                ((IList)Get(topology, "Nodes")).Count, 2, 0f, 0 }, null);
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

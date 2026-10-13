#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CapstoneAudio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityGAS;
using Object = UnityEngine.Object;

/// <summary>Verifies placement ownership and the plan-copy boundary used by delayed mark spawning.</summary>
public sealed class LightningSpearPlacementStructurePlayModeTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<Object> owned = new List<Object>();
    private UnityEngine.Random.State randomState;
    private GameObject root;
    private Tilemap floor;
    private Tile tile;
    private MonsterRoomArea2D room;
    private LightningSpearLoadout loadout;
    private LightningSpearSkill2Data data;
    private Vector2 Origin => root.transform.position;

    [SetUp]
    public void SetUp()
    {
        randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(20261010);
        root = Own(new GameObject("LightningStructureFixture"));
        root.transform.position = new Vector3(10000f, 10000f, 0f);
        var grid = Child("Grid").AddComponent<Grid>();
        floor = Child("Floor", grid.transform).AddComponent<Tilemap>();
        floor.gameObject.layer = LayerMask.NameToLayer("Ground");
        tile = Own(ScriptableObject.CreateInstance<Tile>());
        FillFloor();
        var area = Child("RoomArea").AddComponent<BoxCollider2D>();
        area.size = Vector2.one * 30f;
        area.isTrigger = true;
        room = area.gameObject.AddComponent<MonsterRoomArea2D>();
        room.Configure(area);
        var target = Child("Target");
        target.transform.position = Origin + new Vector2(3.1f, 0.2f);
        target.AddComponent<BoxCollider2D>().size = Vector2.one * 0.2f;
        target.AddComponent<TrainingDummy2D>();
        loadout = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<LightningSpearLoadout>(
            "Assets/_Project/Data/Items/Weapons/Loadouts/WAL_LightningSpear.asset")));
        data = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<LightningSpearSkill2Data>(
            "Assets/_Project/Data/Items/Weapons/LogicData/ALData_LightningSpearSkill2.asset")));
        Physics2D.SyncTransforms();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var mark in Object.FindObjectsByType<LightningSpearMarkActor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (mark.name == "LightningStructureMarkPrefab(Clone)") Object.DestroyImmediate(mark.gameObject);
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        UnityEngine.Random.state = randomState;
    }

    [Test]
    public void Planner_ProducesRainAndThirdStrikeWithoutCreatingOrOwningActors()
    {
        var liveMarks = new List<LightningSpearMarkActor>();
        object planner = CreatePlanner(liveMarks);
        int actorsBefore = Object.FindObjectsByType<LightningSpearMarkActor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        IList requests = GenerateRain(planner);
        Assert.That(requests.Count, Is.EqualTo(6));
        foreach (object request in requests) Assert.That(Room(request), Is.SameAs(room));
        AssertRequestEnded(planner);
        object[] thirdArgs = { loadout, data, Origin, Origin + Vector2.left * 3f, null };
        Assert.That((bool)Invoke(planner, "TryPlanThirdStrike", thirdArgs), Is.True);
        Assert.That(Room(thirdArgs[4]), Is.SameAs(room));
        AssertRequestEnded(planner);
        Assert.That(liveMarks, Is.Empty, "Planning must not register live actors.");
        Assert.That(Object.FindObjectsByType<LightningSpearMarkActor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            Is.EqualTo(actorsBefore), "Only the runtime owner may instantiate mark actors.");
    }

    [Test]
    public void IndependentPlanners_DoNotShareReservationsOrOverwriteBorrowedResults()
    {
        object first = CreatePlanner(new List<LightningSpearMarkActor>());
        object second = CreatePlanner(new List<LightningSpearMarkActor>());
        IList firstRequests = GenerateRain(first);
        Vector2[] firstPositions = Positions(firstRequests);
        IList secondRequests = GenerateRain(second);
        Assert.That(firstRequests, Is.Not.SameAs(secondRequests));
        CollectionAssert.AreEqual(firstPositions, Positions(secondRequests));
        object[] thirdArgs = { loadout, data, Origin, Origin + Vector2.left * 3f, null };
        Assert.That((bool)Invoke(second, "TryPlanThirdStrike", thirdArgs), Is.True);
        CollectionAssert.AreEqual(firstPositions, Positions(firstRequests), "Another owner cannot reuse this planner's scratch list.");
        IList repeated = GenerateRain(first);
        CollectionAssert.AreEqual(firstPositions, Positions(repeated), "A completed request cannot leave reservations in the next request.");
        AssertRequestEnded(first);
        AssertRequestEnded(second);
    }

    [Test]
    public void FailedThirdStrike_ReleasesRequestAndCanRetryAfterGroundReturns()
    {
        object planner = CreatePlanner(new List<LightningSpearMarkActor>());
        floor.ClearAllTiles();
        object[] args = { loadout, data, Origin, Origin + new Vector2(3.1f, 0.2f), null };
        Assert.That((bool)Invoke(planner, "TryPlanThirdStrike", args), Is.False);
        AssertRequestEnded(planner);
        FillFloor();
        Assert.That((bool)Invoke(planner, "TryPlanThirdStrike", args), Is.True,
            "Exhausted candidates and invalid geometry belong to the previous request only.");
        Assert.That(Position(args[4]), Is.EqualTo((Vector2)args[3]));
        Assert.That(Room(args[4]), Is.SameAs(room));
        AssertRequestEnded(planner);
    }

    [UnityTest]
    public IEnumerator DelayedRain_KeepsItsCopiedPlan_WhenThirdStrikeReusesPlannerScratch()
    {
        var runtime = CreateRainRuntime(out AbilitySystem system);
        object planner = typeof(LightningSpearRuntimeState).GetProperty("PlacementPlanner", Members).GetValue(runtime);
        Vector2[] plannedPositions = Positions(GenerateRain(planner));
        var definition = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        var spec = new AbilitySpec(definition);
        var liveMarks = (List<LightningSpearMarkActor>)typeof(LightningSpearRuntimeState).GetField("activeMarks", Members).GetValue(runtime);
        IEnumerator execution = runtime.ExecuteSkill2(system, spec, loadout, data);
        Coroutine running = system.StartCoroutine(execution);
        try
        {
            float deadline = Time.time + 2f;
            while (liveMarks.Count == 0 && Time.time < deadline) yield return null;
            Assert.That(liveMarks.Count, Is.EqualTo(1), "Targeted rain yields after its first spawn.");
            object[] thirdArgs = { loadout, data, Origin, Origin + Vector2.left * 3f, null };
            Assert.That((bool)Invoke(planner, "TryPlanThirdStrike", thirdArgs), Is.True);
            Assert.That(((IList)planner.GetType().GetProperty("Pending", Members).GetValue(planner)).Count, Is.Zero,
                "Third-strike planning has reused and cleared the borrowed rain list.");
            while (liveMarks.Count < plannedPositions.Length && Time.time < deadline) yield return null;
            Assert.That(liveMarks.Count, Is.EqualTo(plannedPositions.Length), "The delayed coroutine must retain all copied rain requests.");
            for (int i = 0; i < plannedPositions.Length; i++)
            {
                Assert.That((Vector2)liveMarks[i].transform.position, Is.EqualTo(plannedPositions[i]));
                Assert.That(liveMarks[i].RoomArea, Is.SameAs(room));
            }
            AssertRequestEnded(planner);
        }
        finally
        {
            system.StopCoroutine(running);
            ((IDisposable)execution).Dispose();
        }
    }

    [UnityTest]
    public IEnumerator DelayedRain_CancellationStopsFurtherSpawns_AndDisableClearsExistingMark()
    {
        var runtime = CreateRainRuntime(out AbilitySystem system);
        var definition = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        var spec = new AbilitySpec(definition);
        var token = new AbilityCancellationToken();
        typeof(AbilitySpec).GetProperty("Token", Members).SetValue(spec, token);
        var liveMarks = (List<LightningSpearMarkActor>)typeof(LightningSpearRuntimeState).GetField("activeMarks", Members).GetValue(runtime);
        IEnumerator execution = runtime.ExecuteSkill2(system, spec, loadout, data);
        Coroutine running = system.StartCoroutine(execution);
        try
        {
            float deadline = Time.time + 2f;
            while (liveMarks.Count == 0 && Time.time < deadline) yield return null;
            Assert.That(liveMarks.Count, Is.EqualTo(1));
            LightningSpearMarkActor firstMark = liveMarks[0];
            token.Cancel();
            float observeUntil = Time.time + 0.25f;
            while (Time.time < observeUntil) yield return null;
            Assert.That(liveMarks.Count, Is.EqualTo(1), "Cancelling during the inter-mark delay must stop the remaining five spawns.");
            Assert.That(liveMarks[0], Is.SameAs(firstMark), "The already spawned actor remains with the live runtime owner.");
            object planner = typeof(LightningSpearRuntimeState).GetProperty("PlacementPlanner", Members).GetValue(runtime);
            AssertRequestEnded(planner);

            // Exercise the real lifecycle callback without enabling unrelated per-frame input/feedback.
            runtime.enabled = true;
            runtime.enabled = false;
            Assert.That(liveMarks, Is.Empty, "Disable must unregister the existing live mark.");
            yield return null;
            Assert.That(firstMark == null, Is.True, "Disable must destroy the actor as well as clearing the owner list.");
            yield return null;
            Assert.That(liveMarks, Is.Empty, "The cancelled coroutine cannot recreate actors after cleanup.");
        }
        finally
        {
            system.StopCoroutine(running);
            ((IDisposable)execution).Dispose();
        }
    }

    private LightningSpearRuntimeState CreateRainRuntime(out AbilitySystem system)
    {
        var actor = Child("RainOwner");
        system = actor.AddComponent<AbilitySystem>();
        var inventory = actor.AddComponent<RelicInventory>();
        var targetedRelic = Own(ScriptableObject.CreateInstance<RelicDefinition>());
        targetedRelic.relicId = WeaponExclusiveRelics.SpearTargetedRain;
        inventory.RestoreShellState(new RelicInventoryState
        {
            slots = new[] { new RelicSlotState { relicId = targetedRelic.relicId, level = 1 } }
        }, id => id == targetedRelic.relicId ? targetedRelic : null);
        Assert.That(WeaponExclusiveRelics.Has(actor, WeaponExclusiveRelics.SpearTargetedRain), Is.True);
        var runtime = Child("Runtime", actor.transform).AddComponent<LightningSpearRuntimeState>();
        runtime.enabled = false;
        var source = Child("LightningStructureMarkPrefab");
        source.SetActive(false);
        var prefab = source.AddComponent<LightningSpearMarkActor>();
        Set(data, "markPrefab", prefab);
        Set(data, "markRainSpawnEventTag", null);
        Set(data, "markRainAnimationTrigger", null);
        Set(data, "markRainFallbackSpawnDelay", 0f);
        Set(data, "markRainSpawnStartSound", default(SoundRef));
        Set(data, "markRainMarkSpawnSound", default(SoundRef));
        return runtime;
    }

    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private GameObject Child(string name, Transform parent = null)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent != null ? parent : root.transform, false);
        return child;
    }
    private void FillFloor()
    {
        for (int x = -15; x < 15; x++)
            for (int y = -15; y < 15; y++) floor.SetTile(new Vector3Int(x, y, 0), tile);
    }
    private static void Set(Object target, string name, object value) => target.GetType().GetField(name, Members).SetValue(target, value);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Members).Invoke(target, args);
    private static object CreatePlanner(List<LightningSpearMarkActor> liveMarks)
    {
        var assembly = typeof(LightningSpearRuntimeState).Assembly;
        object world = Activator.CreateInstance(assembly.GetType("LightningSpearPlacementWorldQuery", true), true);
        return Activator.CreateInstance(assembly.GetType("LightningSpearMarkPlacementPlanner", true), Members,
            null, new object[] { world, liveMarks }, null);
    }
    private IList GenerateRain(object planner) => (IList)Invoke(planner, "GenerateRain", loadout, data, Origin, true);
    private static Vector2 Position(object request) => (Vector2)request.GetType().GetField("position", Members).GetValue(request);
    private static MonsterRoomArea2D Room(object request) => (MonsterRoomArea2D)request.GetType().GetField("room", Members).GetValue(request);
    private static Vector2[] Positions(IList requests)
    {
        var positions = new Vector2[requests.Count];
        for (int i = 0; i < positions.Length; i++) positions[i] = Position(requests[i]);
        return positions;
    }
    private static void AssertRequestEnded(object planner)
    {
        object search = planner.GetType().GetProperty("Search", Members).GetValue(planner);
        Assert.That((bool)search.GetType().GetProperty("IsActive", Members).GetValue(search), Is.False);
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

/// <summary>Exercises the shared tools using coordinates and numeric settings without weapon data or actors.</summary>
public sealed class PlacementToolCompositionPlayModeTests
{
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<Object> owned = new List<Object>();
    private readonly List<Vector2> existingPositions = new List<Vector2>();
    private UnityEngine.Random.State savedRandom;
    private GameObject root;
    private Tilemap floor;
    private Tile tile;
    private MonsterRoomArea2D room;
    private Type readerType;
    private Type sourcesType;
    private Type flowType;
    private object maps;
    private object physics;
    private object sources;
    private object search;
    private object session;
    private Vector2 Origin => root.transform.position;

    [SetUp]
    public void SetUp()
    {
        savedRandom = UnityEngine.Random.state;
        UnityEngine.Random.InitState(20261010);
        existingPositions.Clear();
        root = Own(new GameObject("PlacementToolFixture"));
        root.transform.position = new Vector3(10000f, 10000f, 0f);
        var grid = Child("Grid").AddComponent<Grid>();
        floor = Child("Floor", grid.transform).AddComponent<Tilemap>();
        floor.gameObject.layer = LayerMask.NameToLayer("Ground");
        tile = Own(ScriptableObject.CreateInstance<Tile>());
        FillFloor();
        var boundary = Child("Room").AddComponent<BoxCollider2D>();
        boundary.size = Vector2.one * 30f;
        boundary.isTrigger = true;
        room = boundary.gameObject.AddComponent<MonsterRoomArea2D>();
        room.Configure(boundary);
        Physics2D.SyncTransforms();

        // The public runtime type is only an assembly anchor; the fixture uses no weapon state or data.
        var assembly = typeof(LightningSpearRuntimeState).Assembly;
        readerType = assembly.GetType("PositionListReader", true);
        sourcesType = assembly.GetType("CandidateSources", true);
        flowType = assembly.GetType("PlacementFlow", true);
        maps = Create(assembly.GetType("MapQuery2D", true),
            LayerMask.NameToLayer("Ground"), LayerMask.NameToLayer("Wall"), "Wall");
        physics = Create(assembly.GetType("PhysicsQuery2D", true));
        sources = Create(sourcesType);
        search = Create(assembly.GetType("NearestPositionSearch", true), sources);
        object reader = Create(readerType, existingPositions);
        session = Create(assembly.GetType("PlacementSession`1", true).MakeGenericType(readerType), maps, physics, reader);
    }

    [TearDown]
    public void TearDown()
    {
        End();
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        UnityEngine.Random.state = savedRandom;
    }

    [Test]
    public void SameTools_ComposeBoundedFillRoundRobinAndFirstSuccess_WithoutCreatingActors()
    {
        Vector2 first = Origin + new Vector2(3.1f, 0.2f);
        Vector2 second = Origin + new Vector2(-3.1f, 0.2f);
        var targets = new List<Transform> { Target(first), Target(second) };
        int objectsBefore = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        Begin(0f);
        var beforeFixed = UnityEngine.Random.state;
        object fixedSource = FixedThenRandom(first, second);
        Fill(fixedSource, 2, 2);
        CollectionAssert.AreEqual(new[] { first, second }, Positions());
        Assert.That(UnityEngine.Random.state.Equals(beforeFixed), Is.True, "Fixed candidates do not draw random samples.");
        var randomStart = UnityEngine.Random.state;
        Vector2 randomFirst = Origin + UnityEngine.Random.insideUnitCircle * 2f;
        Vector2 randomSecond = Origin + UnityEngine.Random.insideUnitCircle * 2f;
        var randomEnd = UnityEngine.Random.state;
        UnityEngine.Random.state = randomStart;
        Fill(RandomDisk(), 2, 4);
        CollectionAssert.AreEqual(new[] { first, second, randomFirst, randomSecond }, Positions());
        Assert.That(UnityEngine.Random.state.Equals(randomEnd), Is.True);
        AssertRooms();
        End();

        Begin(1f);
        Flow("RoundRobinFill", targets, 2, search, maps, session);
        CollectionAssert.AreEqual(new[] { first, second }, Positions());
        AssertRooms();
        End();

        Begin(1f);
        var beforeFirstSuccess = UnityEngine.Random.state;
        object[] firstArgs = { first, search, maps, FixedThenRandom(second, second), 3, session, null };
        Assert.That((bool)Flow("FirstSuccess", firstArgs), Is.True);
        CollectionAssert.AreEqual(new[] { first }, Positions());
        Assert.That(PointPosition(firstArgs[6]), Is.EqualTo(first));
        Assert.That(UnityEngine.Random.state.Equals(beforeFirstSuccess), Is.True,
            "An exact nearest success cannot request fallback samples.");
        AssertRooms();
        End();
        Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
            Is.EqualTo(objectsBefore), "The same tools produce plans without creating scene objects.");
    }

    [TestCase(1, 0)]
    [TestCase(2, 0)]
    [TestCase(3, 1)]
    public void FirstSuccess_AllInvalid_ChargesFixedPrefixAgainstBudget_AndDrawsRandomLazily(int budget, int randomSamples)
    {
        floor.ClearAllTiles();
        Begin(0f);
        Vector2 first = Origin + Vector2.right * 2f;
        Vector2 second = Origin + Vector2.up * 2f;
        var before = UnityEngine.Random.state;
        for (int i = 0; i < randomSamples; i++) _ = UnityEngine.Random.insideUnitCircle;
        var expected = UnityEngine.Random.state;
        UnityEngine.Random.state = before;
        object[] args = { first, search, maps, FixedThenRandom(first, second), budget, session, null };
        Assert.That((bool)Flow("FirstSuccess", args), Is.False);
        Assert.That(Pending, Is.Empty);
        Assert.That(UnityEngine.Random.state.Equals(expected), Is.True,
            "Budgets 1 and 2 consume fixed points only; budget 3 permits exactly one random candidate.");
        End();
        AssertEnded();
    }

    [Test]
    public void ZeroSpacing_AllowsSixExactPlacementsUnderOneCoordinateTarget()
    {
        Vector2 preferred = Origin + new Vector2(2.2f, 1.1f);
        var targets = new List<Transform> { Target(preferred) };
        Begin(0f);
        Flow("RoundRobinFill", targets, 6, search, maps, session);
        CollectionAssert.AreEqual(new[] { preferred, preferred, preferred, preferred, preferred, preferred }, Positions());
        AssertRooms();
        End();
        AssertEnded();
    }

    [Test]
    public void GrowingReservations_RejectCachedSuccess_AndKeepNearestTieOrder()
    {
        Vector2 preferred = Origin + Vector2.right * 2f;
        existingPositions.Add(preferred);
        Begin(0.2f);
        Vector2 first = FindNearest(preferred);
        Assert.That(first, Is.EqualTo(Origin + new Vector2(1.5f, -0.5f)));
        Invoke(session, "Commit", first);
        Vector2 second = FindNearest(preferred);
        Assert.That(second, Is.EqualTo(Origin + new Vector2(1.5f, 0.5f)),
            "A cached-valid point is rejected after reservation, then equal-distance candidates retain X/Y order.");
        Invoke(session, "Commit", second);
        CollectionAssert.AreEqual(new[] { first, second }, Positions());
        End();
    }

    [Test]
    public void RequestReuse_ResetsGeometryAndReservations_WhileReaderObservesLiveCoordinateChanges()
    {
        Vector2 preferred = Origin + new Vector2(3.1f, 0.2f);
        floor.ClearAllTiles();
        Begin(1f);
        Assert.That((bool)Invoke(session, "CanPlace", preferred), Is.False);
        object[] failedArgs = { preferred, maps, session, null };
        Assert.That((bool)Generic(search, "TryFind", failedArgs), Is.False);
        End();
        AssertEnded();

        FillFloor();
        Begin(1f);
        Assert.That(FindNearest(preferred), Is.EqualTo(preferred), "A prior failed geometry/exhaustion cache cannot survive Begin.");
        existingPositions.Add(preferred);
        Assert.That((bool)Invoke(session, "CanPlace", preferred), Is.False,
            "A newly added live coordinate must override the remembered valid geometry.");
        existingPositions[0] = Origin + Vector2.left * 4f;
        Assert.That((bool)Invoke(session, "CanPlace", preferred), Is.True,
            "The reader follows the maintained list instead of an earlier position snapshot.");
        Invoke(session, "Commit", preferred);
        End();

        Begin(1f);
        Assert.That(Pending, Is.Empty);
        Assert.That((bool)Invoke(session, "CanPlace", preferred), Is.True, "Previous pending reservations cannot leak into the next request.");
        End();
        AssertEnded();
    }

    private IList Pending => (IList)session.GetType().GetProperty("Pending", InstanceMembers).GetValue(session);
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private GameObject Child(string name, Transform parent = null)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent != null ? parent : root.transform, false);
        return child;
    }
    private Transform Target(Vector2 position)
    {
        Transform target = Child("CoordinateTarget").transform;
        target.position = position;
        return target;
    }
    private void FillFloor()
    {
        for (int x = -15; x < 15; x++)
            for (int y = -15; y < 15; y++) floor.SetTile(new Vector3Int(x, y, 0), tile);
    }
    private void Begin(float spacing)
    {
        var settingsType = session.GetType().Assembly.GetType("PlacementSettings", true);
        object settings = Create(settingsType, Origin, 7f, room, 0f, spacing, 0f, 0, 0.1f, 0, 0, true, true, true, true);
        Invoke(session, "Begin", settings, true);
        Invoke(search, "Begin", Origin, 7f);
    }
    private void End()
    {
        if (session != null) Invoke(session, "End");
        if (search != null) Invoke(search, "End");
        if (physics != null) Invoke(physics, "ClearPlacementResults");
    }
    private void AssertEnded()
    {
        Assert.That((bool)session.GetType().GetProperty("IsActive", InstanceMembers).GetValue(session), Is.False);
        Assert.That((bool)search.GetType().GetProperty("IsActive", InstanceMembers).GetValue(search), Is.False);
    }
    private static object Create(Type type, params object[] args) =>
        Activator.CreateInstance(type, InstanceMembers, null, args, null);
    private static object Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, InstanceMembers).Invoke(target, args);
    private object Generic(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, InstanceMembers).MakeGenericMethod(readerType).Invoke(target, args);
    private object Flow(string name, params object[] args) =>
        flowType.GetMethod(name, StaticMembers).MakeGenericMethod(readerType).Invoke(null, args);
    private object FixedThenRandom(Vector2 first, Vector2 second) =>
        sourcesType.GetMethod("FixedThenRandom", StaticMembers).Invoke(null, new object[] { first, second, Origin, 2f });
    private object RandomDisk() => sourcesType.GetMethod("RandomDisk", StaticMembers).Invoke(null, new object[] { Origin, 2f });
    private void Fill(object source, int budget, int desiredTotal) => Flow("FillWithinBudget", source, budget, desiredTotal, session);
    private Vector2 FindNearest(Vector2 preferred)
    {
        object[] args = { preferred, maps, session, null };
        Assert.That((bool)Generic(search, "TryFind", args), Is.True);
        return (Vector2)args[3];
    }
    private static Vector2 PointPosition(object point) => (Vector2)point.GetType().GetField("position", InstanceMembers).GetValue(point);
    private Vector2[] Positions()
    {
        var result = new Vector2[Pending.Count];
        for (int i = 0; i < result.Length; i++) result[i] = PointPosition(Pending[i]);
        return result;
    }
    private void AssertRooms()
    {
        foreach (object point in Pending)
            Assert.That(point.GetType().GetField("room", InstanceMembers).GetValue(point), Is.SameAs(room));
    }
}
#endif

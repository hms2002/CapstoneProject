using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityGAS;

/// <summary>Responsibility: verify pure content aggregation and concave-room assignment.</summary>
public sealed class DungeonMapContentModelTests
{
    [Test]
    public void Sources_AggregateByKindWithoutDuplicates()
    {
        var model = new DungeonMapContentModel();
        int changed = 0;
        model.Changed += _ => changed++;
        model.Set(1, 10, DungeonMapContentKind.ClosedChest);
        model.Set(1, 10, DungeonMapContentKind.ClosedChest);
        model.Set(2, 10, DungeonMapContentKind.ClosedChest);
        model.Set(3, 10, DungeonMapContentKind.Heart);
        Assert.That(changed, Is.EqualTo(3));
        Assert.That(model.GetContents(10).ClosedChestCount, Is.EqualTo(2));
        Assert.That(model.GetContents(10).HeartCount, Is.EqualTo(1));
    }

    [Test]
    public void Opening_ReplacesClosedCountAtomically()
    {
        var model = new DungeonMapContentModel();
        model.Set(1, 10, DungeonMapContentKind.ClosedChest);
        int changed = 0;
        model.Changed += room =>
        {
            changed++;
            Assert.That(model.GetContents(room).ClosedChestCount, Is.Zero);
            Assert.That(model.GetContents(room).OpenedChestCount, Is.EqualTo(1));
        };
        model.Set(1, 10, DungeonMapContentKind.OpenedChest);
        Assert.That(changed, Is.EqualTo(1));
    }

    [Test]
    public void MovingAndRemoval_ClearPreviousRoomAndNeverGoNegative()
    {
        var model = new DungeonMapContentModel();
        model.Set(1, 10, DungeonMapContentKind.Heart);
        var changed = new List<int>();
        model.Changed += changed.Add;
        model.Set(1, 11, DungeonMapContentKind.Heart);
        Assert.That(changed, Is.EqualTo(new[] { 10, 11 }));
        Assert.That(model.GetContents(10).HeartCount, Is.Zero);
        Assert.That(model.GetContents(11).HeartCount, Is.EqualTo(1));
        model.Remove(1);
        model.Remove(1);
        Assert.That(model.GetContents(11).HeartCount, Is.Zero);
        Assert.That(changed.Count, Is.EqualTo(3));
    }

    [Test]
    public void Resolver_UsesActualShapesAndStableNearestRoomForCorridors()
    {
        var graph = new DungeonMapGraphSnapshot(new[]
        {
            new DungeonMapRoomNode(7, RoomType.Combat, new Rect(0, 0, 10, 10), new Vector2Int(10, 10),
                new[] { new RectInt(0, 0, 2, 10), new RectInt(2, 0, 8, 2) }),
            new DungeonMapRoomNode(2, RoomType.Combat, new Rect(4, 4, 2, 2))
        }, null);
        Assert.That(DungeonMapContentRoomResolver.Resolve(graph, new Vector2(5, 5)), Is.EqualTo(2));
        Assert.That(DungeonMapContentRoomResolver.Resolve(graph, new Vector2(1, 8)), Is.EqualTo(7));
        Assert.That(DungeonMapContentRoomResolver.Resolve(graph, new Vector2(3, 5)), Is.EqualTo(2));
        Assert.That(DungeonMapContentRoomResolver.Resolve(null, Vector2.zero), Is.EqualTo(-1));
    }

    [Test]
    public void IconGroupSafeArea_DoesNotExtendIntoConcaveEmptyCells()
    {
        var shapes = new[] { new RectInt(0, 0, 3, 9), new RectInt(3, 3, 6, 3) };
        Vector2 anchor = DungeonMapRoomShapeBuilder.ResolveInteriorAnchor(shapes, new Vector2Int(9, 9),
            out Vector2 safeSize);
        Rect safeRect = new((anchor - safeSize * 0.5f) * 9f, safeSize * 9f);
        for (int y = 0; y < 9; y++)
        for (int x = 0; x < 9; x++)
        {
            var cell = new Vector2Int(x, y);
            if (!safeRect.Contains((Vector2)cell + Vector2.one * 0.5f))
                continue;
            Assert.That(Array.Exists(shapes, shape => shape.Contains(cell)), Is.True);
        }
    }
}

/// <summary>
/// Responsibility: verify source lifecycle, discovery gating and disposal in an isolated scene.
/// Cover the translated/scaled tile grid used by real procedural scenes, not only identity coordinates.
/// Restore shared runtime ownership by destroying the owned map controller before scene teardown.
/// </summary>
public sealed class DungeonMapContentLifecyclePlayModeTests
{
    private Scene scene;
    private readonly List<UnityEngine.Object> owned = new();
    private DungeonMapContentTracker tracker;
    private DungeonMapGraphSnapshot graph;
    private DungeonMapRuntimeController previousRuntime;

    [SetUp]
    public void SetUp()
    {
        previousRuntime = DungeonMapRuntimeController.Active;
        scene = SceneManager.CreateScene("MapContentTest_" + Guid.NewGuid().ToString("N"));
        graph = new DungeonMapGraphSnapshot(new[]
        {
            new DungeonMapRoomNode(1, RoomType.Start, new Rect(0, 0, 10, 10)),
            new DungeonMapRoomNode(2, RoomType.Combat, new Rect(20, 0, 10, 10))
        }, new[] { new DungeonMapConnection(1, 2) });
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        tracker?.Dispose();
        tracker = null;
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null)
                UnityEngine.Object.DestroyImmediate(owned[i]);
        owned.Clear();
        if (previousRuntime != null && previousRuntime.isActiveAndEnabled)
            typeof(DungeonMapRuntimeController).GetMethod("SetActiveRuntime",
                BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { previousRuntime });
        if (scene.IsValid() && scene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(scene);
    }

    [Test]
    public void Chest_BootstrapRestoreDisableReenableAndDestroyReflectActualState()
    {
        TreasureChest chest = NewObject("Chest", new Vector2(5, 5)).AddComponent<TreasureChest>();
        tracker = new DungeonMapContentTracker(scene, graph, _ => { });
        Assert.That(tracker.Model.GetContents(1).ClosedChestCount, Is.EqualTo(1));
        chest.RestoreOpenedStateForDungeon();
        Assert.That(tracker.Model.GetContents(1).ClosedChestCount, Is.Zero);
        Assert.That(tracker.Model.GetContents(1).OpenedChestCount, Is.EqualTo(1));
        chest.gameObject.SetActive(false);
        Assert.That(tracker.Model.GetContents(1).OpenedChestCount, Is.Zero);
        chest.gameObject.SetActive(true);
        Assert.That(tracker.Model.GetContents(1).OpenedChestCount, Is.EqualTo(1));
        UnityEngine.Object.DestroyImmediate(chest.gameObject);
        Assert.That(tracker.Model.GetContents(1).OpenedChestCount, Is.Zero);
    }

    [Test]
    public void RestoredChestBeforeMapConfiguration_IsAlreadyOpened()
    {
        TreasureChest chest = NewObject("RestoredChest", new Vector2(5, 5)).AddComponent<TreasureChest>();
        chest.RestoreOpenedStateForDungeon();
        tracker = new DungeonMapContentTracker(scene, graph, _ => { });
        Assert.That(tracker.Model.GetContents(1).ClosedChestCount, Is.Zero);
        Assert.That(tracker.Model.GetContents(1).OpenedChestCount, Is.EqualTo(1));
    }

    [Test]
    public void Heart_UsesLandingPositionAndRemovesOnDisable()
    {
        tracker = new DungeonMapContentTracker(scene, graph, _ => { });
        FieldHealPickup2D heart = CreateHeart(new Vector2(5, 5));
        Assert.That(tracker.Model.GetContents(1).HeartCount, Is.EqualTo(1));
        heart.PlayDrop(new Vector2(5, 5), new Vector2(25, 5));
        Assert.That(tracker.Model.GetContents(1).HeartCount, Is.Zero);
        Assert.That(tracker.Model.GetContents(2).HeartCount, Is.EqualTo(1));
        heart.gameObject.SetActive(false);
        Assert.That(tracker.Model.GetContents(2).HeartCount, Is.Zero);
    }

    [Test]
    public void FullHealth_LeavesHeartVisible_ActualHealingRemovesItImmediately()
    {
        var health = Own(ScriptableObject.CreateInstance<AttributeDefinition>());
        health.attributeName = "Health";
        health.defaultBaseValue = health.maxValue = 100f;
        var profile = Own(ScriptableObject.CreateInstance<AttributeInitProfileSO>());
        SetField(profile, "entries", new[]
        {
            new AttributeInitProfileSO.Entry { attribute = health, baseValue = 100f }
        });
        GameObject player = NewObject("Player", new Vector2(5, 5));
        player.SetActive(false);
        var attributes = player.AddComponent<AttributeSet>();
        SetField(attributes, "baseInitProfile", profile);
        var body = player.AddComponent<CircleCollider2D>();
        player.AddComponent<PickupCollector2D>();
        player.SetActive(true);
        tracker = new DungeonMapContentTracker(scene, graph, _ => { });
        FieldHealPickup2D heart = CreateHeart(new Vector2(5, 5));
        heart.Configure(health, 1, null);
        var collect = typeof(FieldHealPickup2D).GetMethod("TryCollect", BindingFlags.Instance | BindingFlags.NonPublic);
        collect.Invoke(heart, new object[] { body });
        Assert.That(heart.IsCollected, Is.False);
        Assert.That(tracker.Model.GetContents(1).HeartCount, Is.EqualTo(1));
        attributes.TrySetBaseValue(health, 99f, heart);
        collect.Invoke(heart, new object[] { body });
        Assert.That(attributes.GetCurrentValue(health), Is.EqualTo(100f));
        Assert.That(heart.IsCollected, Is.True);
        Assert.That(tracker.Model.GetContents(1).HeartCount, Is.Zero);
    }

    [Test]
    public void OtherScenesAndDisposedTrackers_DoNotPublishChanges()
    {
        int changes = 0;
        tracker = new DungeonMapContentTracker(scene, graph, _ => changes++);
        GameObject foreign = Own(new GameObject("OtherSceneChest"));
        foreign.AddComponent<TreasureChest>();
        Assert.That(changes, Is.Zero);
        tracker.Dispose();
        NewObject("AfterDispose", new Vector2(5, 5)).AddComponent<TreasureChest>();
        Assert.That(changes, Is.Zero);
    }

    [TestCase(-49.35f, -2.04f, 0f, 1f, 1f)]
    [TestCase(17f, -8f, 32f, 2f, 0.5f)]
    public void TransformedGrid_AssignsChestsAndDroppedHeartsToTheirActualRooms(
        float x, float y, float rotation, float scaleX, float scaleY)
    {
        Tilemap tilemap = CreateTilemap(x, y, rotation, scaleX, scaleY);
        Vector3 firstPosition = tilemap.GetCellCenterWorld(new Vector3Int(5, 5, 0));
        Vector3 secondPosition = tilemap.GetCellCenterWorld(new Vector3Int(25, 5, 0));
        TreasureChest chest = NewObject("ActualSecondRoomChest", secondPosition).AddComponent<TreasureChest>();
        tracker = new DungeonMapContentTracker(scene, graph, _ => { }, tilemap);
        Assert.That(tracker.Model.GetContents(1).ClosedChestCount, Is.Zero);
        Assert.That(tracker.Model.GetContents(2).ClosedChestCount, Is.EqualTo(1));

        FieldHealPickup2D heart = CreateHeart(firstPosition);
        heart.PlayDrop(firstPosition, secondPosition);
        Assert.That(tracker.Model.GetContents(1).HeartCount, Is.Zero);
        Assert.That(tracker.Model.GetContents(2).HeartCount, Is.EqualTo(1));
        chest.RestoreOpenedStateForDungeon();
        Assert.That(tracker.Model.GetContents(2).OpenedChestCount, Is.EqualTo(1));
        heart.gameObject.SetActive(false);
        Assert.That(tracker.Model.GetContents(2).HeartCount, Is.Zero);
    }

    [Test]
    public void RuntimeReenable_RetainsItsBuilderCoordinateSpace()
    {
        Tilemap tilemap = CreateTilemap(-49.35f, -2.04f, 0f, 1f, 1f);
        NewObject("ActualSecondRoomChest", tilemap.GetCellCenterWorld(new Vector3Int(25, 5, 0)))
            .AddComponent<TreasureChest>();
        GameObject root = NewObject("MapRuntime", Vector2.zero);
        root.SetActive(false);
        var runtime = root.AddComponent<DungeonMapRuntimeController>();
        var discovery = new DungeonMapDiscoveryModel(graph);
        discovery.RevealInitialStartRoom();
        SetField(runtime, "graph", graph);
        SetField(runtime, "discovery", discovery);
        SetField(runtime, "layoutGrid", tilemap);
        SetField(runtime, "configured", true);
        root.SetActive(true);
        runtime.NotifyPlayerEnteredRoom(2);
        Assert.That(runtime.GetRoomContents(1).ClosedChestCount, Is.Zero);
        Assert.That(runtime.GetRoomContents(2).ClosedChestCount, Is.EqualTo(1));
        root.SetActive(false);
        root.SetActive(true);
        Assert.That(runtime.GetRoomContents(2).ClosedChestCount, Is.EqualTo(1));
    }

    private Tilemap CreateTilemap(float x, float y, float rotation, float scaleX, float scaleY)
    {
        GameObject gridObject = NewObject("TranslatedDungeonGrid", new Vector2(x, y));
        Grid grid = gridObject.AddComponent<Grid>();
        grid.cellSize = new Vector3(1.25f, 0.75f, 0f);
        gridObject.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        gridObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        GameObject tiles = NewObject("FloorTilemap", Vector2.zero);
        tiles.transform.SetParent(gridObject.transform, false);
        tiles.transform.localPosition = new Vector3(0.3f, -0.7f, 0f);
        return tiles.AddComponent<Tilemap>();
    }

    private FieldHealPickup2D CreateHeart(Vector2 position)
    {
        GameObject root = NewObject("Heart", position);
        root.AddComponent<CircleCollider2D>();
        return root.AddComponent<FieldHealPickup2D>();
    }

    [Test]
    public void Runtime_HidesUnvisitedContentsAndRebuildsAfterDisable()
    {
        NewObject("UnvisitedChest", new Vector2(25, 5)).AddComponent<TreasureChest>();
        GameObject root = NewObject("MapRuntime", Vector2.zero);
        root.SetActive(false);
        var runtime = root.AddComponent<DungeonMapRuntimeController>();
        var discovery = new DungeonMapDiscoveryModel(graph);
        discovery.RevealInitialStartRoom();
        SetField(runtime, "graph", graph);
        SetField(runtime, "discovery", discovery);
        SetField(runtime, "configured", true);
        root.SetActive(true);
        Assert.That(runtime.GetRoomContents(2).ClosedChestCount, Is.Zero);
        runtime.NotifyPlayerEnteredRoom(2);
        Assert.That(runtime.GetRoomContents(2).ClosedChestCount, Is.EqualTo(1));
        root.SetActive(false);
        NewObject("CreatedWhileMapDisabled", new Vector2(25, 5)).AddComponent<TreasureChest>();
        root.SetActive(true);
        Assert.That(runtime.GetRoomContents(2).ClosedChestCount, Is.EqualTo(2));
        runtime.ClearConfiguration();
        Assert.That(runtime.GetRoomContents(2).ClosedChestCount, Is.Zero);
    }

    private GameObject NewObject(string name, Vector2 position)
    {
        GameObject result = Own(new GameObject(name));
        SceneManager.MoveGameObjectToScene(result, scene);
        result.transform.position = position;
        return result;
    }

    private T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}

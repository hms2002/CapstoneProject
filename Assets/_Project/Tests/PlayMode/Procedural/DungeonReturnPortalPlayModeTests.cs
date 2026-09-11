#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

/// <summary>Verifies return selection, room gating, art-free authoring, persistence and cancellation without scene travel.</summary>
public sealed class DungeonReturnPortalPlayModeTests
{
    private const string Folder = "Assets/_Project/Prefabs/Map/Procedural/ReturnPortals/";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> owned = new();
    private float oldTimeScale;
    [SetUp] public void Setup() { oldTimeScale = Time.timeScale; Time.timeScale = 1f; }
    [TearDown] public void TearDown()
    {
        foreach (var root in owned.OfType<GameObject>().Where(o => o != null).ToArray())
            foreach (var travel in root.GetComponentsInChildren<DungeonReturnTravel>(true)) travel.Cancel();
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear(); Time.timeScale = oldTimeScale;
    }
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    [TestCase(RoomSocketDirection.Left, 5, 3)]
    [TestCase(RoomSocketDirection.Right, 1, 3)]
    [TestCase(RoomSocketDirection.Down, 3, 5)]
    [TestCase(RoomSocketDirection.Up, 3, 1)]
    public void OppositeWall_FollowsEntranceDirection(RoomSocketDirection direction, int x, int y)
    {
        RectInt bounds = new(1, 1, 5, 5);
        Vector2Int entrance = new Vector2Int(3, 3) + DungeonReturnPortalPlacement.Direction(direction) * 2;
        var cells = DungeonReturnPortalPlacement.Reachable(bounds, entrance, c => true);
        Assert.That(DungeonReturnPortalPlacement.TryChooseOppositeWall(cells, entrance, direction,
            c => !bounds.Contains(c), c => true, out Vector2Int chosen), Is.True);
        Assert.That(chosen, Is.EqualTo(new Vector2Int(x, y)));
    }

    [Test]
    public void Reachability_DoesNotSelectDisconnectedIsland_OrBlockedCandidate()
    {
        var bounds = new RectInt(0, 0, 9, 7);
        var cells = DungeonReturnPortalPlacement.Reachable(bounds, new Vector2Int(1, 3), c => c.x != 4);
        Assert.That(cells.All(c => c.x < 4), Is.True);
        Assert.That(DungeonReturnPortalPlacement.TryChooseOppositeWall(cells, new Vector2Int(1, 3),
            RoomSocketDirection.Left, c => c.x == 4, c => c.y != 3, out var chosen), Is.True);
        Assert.That(chosen.x, Is.EqualTo(3));
        Assert.That(chosen.y, Is.Not.EqualTo(3));
        Assert.That(DungeonReturnPortalPlacement.TryChooseOppositeWall(cells, Vector2Int.zero,
            RoomSocketDirection.Left, c => true, c => false, out _), Is.False);
    }

    [Test]
    public void Builder_UsesActualConnections_PersistsReveal_AndSkipsVisualPreview()
    {
        DungeonLayoutResult layout = MakeLayout();
        var builder = MakeBuilder();
        Assert.That(builder.TryBuild(layout), Is.True);
        Assert.That(builder.GeneratedReturnPortals.Count, Is.EqualTo(1));
        var portal = builder.GeneratedReturnPortals[0];
        Assert.That(portal.RoomPlacementId, Is.EqualTo(1));
        Assert.That(portal.IsRevealed, Is.False);
        portal.NotifyRoomEntered(1);
        var states = builder.CaptureGeneratedObjectStates();
        Assert.That(states.Single(s => s.stateId == "return-portal:1").isActive, Is.True);
        Assert.That(builder.TryBuild(layout), Is.True);
        builder.RestoreGeneratedObjectStates(states);
        Assert.That(builder.GeneratedReturnPortals[0].IsRevealed, Is.True);
        Assert.That(builder.TryBuild(layout, DungeonBuildOptions.VisualOnly), Is.True);
        Assert.That(builder.GeneratedReturnPortals, Is.Empty);
        Assert.That(builder.ReturnTravel == null, Is.True);
    }

    [UnityTest]
    public IEnumerator CombatPortal_WaitsForFinalWavePendingHoldsAndSplitFamily()
    {
        var group = Own(new GameObject("Group")).AddComponent<MonsterSpawnRoomGroup>();
        var portal = Own(Object.Instantiate(Load("DungeonReturnPortal"))).GetComponent<DungeonReturnPortal>();
        portal.Configure(null, 1, group, false, RoomSocketDirection.Up);
        portal.NotifyRoomEntered(1);
        Set(group, "roomEntrySpawnStarted", true);
        Set(group, "roomWavesCompleted", true);
        group.PushEncounterHold();
        Set(group, "pendingRoomEntrySpawnCount", 1);
        var member = Own(new GameObject("SplitParent"));
        group.NotifyMonsterSpawned(member);
        var units = (IList)typeof(MonsterSpawnRoomGroup).GetField("runtimeSpawnedMonsterUnits", Private).GetValue(group);
        var child = Own(new GameObject("SplitSurvivor"));
        units[0].GetType().GetMethod("AddMember").Invoke(units[0], new object[] { child });
        Object.DestroyImmediate(member);
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.That(portal.IsRevealed, Is.False);
        group.PopEncounterHold(); Set(group, "pendingRoomEntrySpawnCount", 0);
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.That(portal.IsRevealed, Is.False);
        Object.DestroyImmediate(child);
        yield return new WaitForSecondsRealtime(0.6f);
        Assert.That(portal.IsRevealed, Is.True);
    }

    [Test]
    public void EventPortal_RevealsOnEntry_ButBusyEncounterIsSeparate()
    {
        var group = Own(new GameObject("EventGroup")).AddComponent<MonsterSpawnRoomGroup>();
        var portal = Own(Object.Instantiate(Load("DungeonReturnPortal"))).GetComponent<DungeonReturnPortal>();
        portal.Configure(null, 3, group, true, RoomSocketDirection.Down);
        portal.NotifyRoomEntered(2);
        Assert.That(portal.IsRevealed, Is.False);
        portal.NotifyRoomEntered(3);
        Assert.That(portal.IsRevealed, Is.True);
        Assert.That(portal.EncounterBusy, Is.False);
        group.PushEncounterHold();
        Assert.That(portal.EncounterBusy, Is.True);
        group.PopEncounterHold();
        Assert.That(portal.EncounterBusy, Is.False);
    }

    [UnityTest]
    public IEnumerator Travel_RejectsDuplicate_AndCancellingRestoresPlayerPhysicsAndVisuals()
    {
        var builder = MakeBuilder(); Assert.That(builder.TryBuild(MakeLayout()), Is.True);
        var player = MakePlayer();
        var portal = builder.GeneratedReturnPortals[0]; portal.RestoreRevealed(true);
        Assert.That(portal.CanInteract(player), Is.True, TravelDiagnostic(builder.ReturnTravel, player));
        Assert.That(builder.ReturnTravel.TryTravel(portal, player), Is.True, TravelDiagnostic(builder.ReturnTravel, player));
        Assert.That(builder.ReturnTravel.TryTravel(portal, player), Is.False);
        yield return new WaitForSecondsRealtime(0.1f);
        builder.ReturnTravel.Cancel();
        Assert.That(builder.ReturnTravel.IsTravelling, Is.False);
        Assert.That(player.Transform.Find("Render").localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(player.Transform.Find("Render").GetComponent<Renderer>().forceRenderingOff, Is.False);
        Assert.That(player.Transform.GetComponent<Collider2D>().enabled, Is.True);
        Assert.That(player.Transform.GetComponent<PlayerTargetabilityBlocker>().IsTargetable, Is.True);
    }

    [UnityTest]
    public IEnumerator Travel_FinishesAtStartWithoutReload_AndCanBeUsedAgain()
    {
        var builder = MakeBuilder(); Assert.That(builder.TryBuild(MakeLayout()), Is.True);
        var player = MakePlayer();
        var portal = builder.GeneratedReturnPortals[0]; portal.RestoreRevealed(true);
        var scene = player.Transform.gameObject.scene;
        Assert.That(builder.ReturnTravel.TryTravel(portal, player), Is.True);
        float until = Time.realtimeSinceStartup + 4f;
        while (builder.ReturnTravel.IsTravelling && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(builder.ReturnTravel.IsTravelling, Is.False);
        Assert.That(player.Transform.gameObject.scene, Is.EqualTo(scene));
        Assert.That(Vector2.Distance(player.Transform.position, builder.ReturnTravel.LandingPoint.position), Is.LessThan(0.01f));
        Assert.That(player.Transform.Find("Render").localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(portal.CanInteract(player), Is.True);
    }

    [Test]
    public void UnsafeLanding_AndNonIdlePlayer_BlockTravelBeforeLocking()
    {
        var builder = MakeBuilder(); Assert.That(builder.TryBuild(MakeLayout()), Is.True);
        var player = MakePlayer(); var portal = builder.GeneratedReturnPortals[0]; portal.RestoreRevealed(true);
        player.SetInteractState(InteractState.Talking);
        Assert.That(portal.CanInteract(player), Is.False);
        player.SetInteractState(InteractState.Idle);
        var obstacle = Own(new GameObject("LandingBlocker", typeof(BoxCollider2D)));
        obstacle.transform.position = builder.ReturnTravel.LandingPoint.position;
        Physics2D.SyncTransforms();
        Assert.That(portal.CanInteract(player), Is.False);
        Assert.That(builder.ReturnTravel.IsTravelling, Is.False);
    }

    [Test]
    public void AuthoredAssets_ContainNoArt_AndPlayerHasExplicitVisualBinding()
    {
        foreach (string name in new[] { "DungeonReturnPortal", "DungeonReturnTravelRig" })
        {
            GameObject prefab = Load(name);
            Assert.That(prefab.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(4));
            foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true)) Assert.That(renderer.sprite, Is.Null);
            foreach (var animator in prefab.GetComponentsInChildren<Animator>(true)) Assert.That(animator.runtimeAnimatorController, Is.Null);
            Assert.That(prefab.GetComponent<ScenePortal>(), Is.Null);
        }
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PF Player.prefab");
        Assert.That(player.GetComponent<PlayerPortalArrivalVisual2D>().IsConfigured, Is.True);
    }

    [TestCase("Shadow")]
    [TestCase("Dragon")]
    [TestCase("Slime")]
    public void ProductionThemeGeometry_FindsPortalsForEachActualDeadEnd(string theme)
    {
        var p = AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(
            $"Assets/_Project/Data/Dungeon/GenerationProfiles/Procedural{theme}GenerationProfile.asset");
        var layout = new DungeonGraphLayoutAssembler().Assemble(p.RoomLibrary, p.LayoutPolicy, p.Seed,
            p.RoomCount, p.MaxPlacementAttemptsPerRoom, p.MinimumCorridorLength, p.CorridorLengthPerRoomCell,
            p.CorridorLengthVariation, p.GuaranteedRoomTemplates);
        Assert.That(layout.IsComplete, Is.True, layout.FailureReason);
        var builder = MakeBuilder();
        foreach (var room in layout.Rooms)
        {
            foreach (var tile in room.Template.BuildData.floorTiles) builder.FloorTilemap.SetTile((Vector3Int)(room.Origin + tile.localCell), tile.tile);
            foreach (var tile in room.Template.BuildData.wallTiles) builder.WallTilemap.SetTile((Vector3Int)(room.Origin + tile.localCell), tile.tile);
        }
        Assert.That((bool)typeof(DungeonRoomBuilder).GetMethod("TryBuildReturnPortals", Private).Invoke(builder, new object[] { layout }), Is.True);
        int expected = layout.Rooms.Count(r => r.Template.LayoutData.roomType != RoomType.Start &&
            DungeonReturnPortalPlacement.TryGetOnlyConnection(layout, r.PlacementId, out _));
        Assert.That(builder.GeneratedReturnPortals.Count, Is.EqualTo(expected));
    }

    private TestPlayerInteractor MakePlayer()
    {
        var root = Own(new GameObject("ReturnTestPlayer", typeof(Rigidbody2D)));
        root.transform.position = new Vector3(12f, 3f);
        root.GetComponent<Rigidbody2D>().gravityScale = 0f;
        root.AddComponent<CircleCollider2D>().radius = 0.2f;
        root.AddComponent<UnityGAS.MovementMotor2D>();
        var render = new GameObject("Render", typeof(SpriteRenderer)); render.transform.SetParent(root.transform, false);
        root.AddComponent<PlayerPortalArrivalVisual2D>().EditorConfigure(new[] { render.transform }, null);
        Physics2D.SyncTransforms();
        return new TestPlayerInteractor(root.transform);
    }

    private static string TravelDiagnostic(DungeonReturnTravel travel, IPlayerInteractor player)
    {
        var validator = (Func<Vector3, float, Transform, bool>)typeof(DungeonReturnTravel).GetField("landingValidator", Private).GetValue(travel);
        float radius = (float)typeof(DungeonReturnTravel).GetMethod("ResolveClearance", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { player.Transform });
        return $"enabled={travel.isActiveAndEnabled}, busy={travel.IsTravelling}, state={player.CurrentState}, radius={radius}, " +
            $"landing={travel.LandingPoint.position}, safe={validator(travel.LandingPoint.position, radius, player.Transform)}, " +
            $"fade={SceneFadeTransitionPlayback.Instance?.IsTransitionActive}, can={travel.CanTravel(player)}";
    }

    private DungeonRoomBuilder MakeBuilder()
    {
        var root = Own(new GameObject("ReturnTestDungeon", typeof(Grid)));
        var builder = root.AddComponent<DungeonRoomBuilder>();
        var floor = new GameObject("Floor", typeof(Tilemap)); floor.transform.SetParent(root.transform, false);
        var wall = new GameObject("Wall", typeof(Tilemap)); wall.transform.SetParent(root.transform, false);
        builder.EditorAssignTilemaps(floor.GetComponent<Tilemap>(), wall.GetComponent<Tilemap>());
        var tile = Own(ScriptableObject.CreateInstance<Tile>()); tile.colliderType = Tile.ColliderType.Grid;
        builder.EditorAssignCorridorTiles(tile, tile);
        builder.EditorAssignConnectedDoorSetup(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Map/ShortCut/Door.prefab").GetComponent<DoorObject>(), null, true);
        builder.EditorConfigureReturnPortals(Load("DungeonReturnPortal").GetComponent<DungeonReturnPortal>(), Load("DungeonReturnTravelRig").GetComponent<DungeonReturnTravel>());
        return builder;
    }

    private DungeonLayoutResult MakeLayout()
    {
        var layout = Construct<DungeonLayoutResult>(7, 2);
        var tile = Own(ScriptableObject.CreateInstance<Tile>()); tile.colliderType = Tile.ColliderType.Grid;
        for (int i = 0; i < 2; i++)
        {
            var template = Own(ScriptableObject.CreateInstance<RoomTemplateSO>());
            var floor = new List<RoomTileData>(); var wall = new List<RoomTileData>();
            for (int x = 0; x < 7; x++) for (int y = 0; y < 7; y++)
            {
                floor.Add(new RoomTileData { localCell = new Vector2Int(x, y), tile = tile });
                if (x == 0 || x == 6 || y == 0 || y == 6) wall.Add(new RoomTileData { localCell = new Vector2Int(x, y), tile = tile });
            }
            var sockets = new List<RoomSocketData> { new() { localCell = new Vector2Int(i == 0 ? 6 : 0, 2), direction = i == 0 ? RoomSocketDirection.Right : RoomSocketDirection.Left, width = 2 } };
            // A template can have unused sockets and still be a runtime dead end.
            if (i == 1) sockets.Add(new RoomSocketData { localCell = new Vector2Int(2, 6), direction = RoomSocketDirection.Up, width = 2 });
            template.EditorSetData(new RoomLayoutData { roomId = "test" + i, roomType = i == 0 ? RoomType.Start : RoomType.Event,
                size = new Vector2Int(7, 7), localBounds = new RectInt(0, 0, 7, 7), sockets = sockets },
                new RoomBuildData { floorTiles = floor, wallTiles = wall, objectPlacements = new List<RoomObjectPlacementData>() });
            var room = Construct<DungeonRoomPlacement>(i, template, new Vector2Int(i * 10, 0), new RectInt(i * 10, 0, 7, 7), 0, false, false);
            typeof(DungeonLayoutResult).GetMethod("AddRoom", Private).Invoke(layout, new object[] { room });
        }
        var connection = Construct<DungeonSocketConnection>(0, 0, 1, 0, 3, new RectInt(7, 2, 3, 2));
        typeof(DungeonLayoutResult).GetMethod("AddConnection", Private).Invoke(layout, new object[] { connection });
        return layout;
    }
    private static T Construct<T>(params object[] args) => (T)Activator.CreateInstance(typeof(T), Private, null, args, null);
    private static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

/// <summary>
/// Verifies candidate limits, direct-placement compatibility, room-only kill locks and
/// exact re-entry persistence using the production builder and authored chest prefabs.
/// </summary>
public sealed class ChestPossiblePlayModeTests
{
    private readonly List<Object> assets = new();
    private HashSet<GameObject> existingRoots;
    private const string ChestPath = "Assets/_Project/Prefabs/Items/Chests/";

    [SetUp]
    public void SetUp() => existingRoots = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (!existingRoots.Contains(root)) Object.DestroyImmediate(root);
        foreach (Object asset in assets) Object.DestroyImmediate(asset);
        assets.Clear();
    }

    [TestCase(0, 5, 0)]
    [TestCase(3, 0, 0)]
    [TestCase(3, 2, 2)]
    [TestCase(3, 7, 3)]
    [TestCase(-1, 4, 0)]
    public void Selection_UsesMaximumNotQuota_AndLeavesGlobalRandomAlone(int maximum, int available, int expected)
    {
        var ids = Enumerable.Range(0, available).Select(i => "room:" + i).ToArray();
        UnityEngine.Random.State before = UnityEngine.Random.state;
        var first = ChestPossibleSelection.Select(ids, maximum, 8721);
        var second = ChestPossibleSelection.Select(ids.Reverse().ToArray(), maximum, 8721);
        Assert.That(first.Count, Is.EqualTo(expected));
        CollectionAssert.AreEquivalent(first, second);
        Assert.That(UnityEngine.Random.state, Is.EqualTo(before));
    }

    [Test]
    public void SavedPresence_WinsOverChangedSeedBudgetAndNewCandidates()
    {
        var states = new[]
        {
            new DungeonObjectRuntimeStateData { stateId = "A", isPresent = true },
            new DungeonObjectRuntimeStateData { stateId = "B", isPresent = false },
            new DungeonObjectRuntimeStateData { stateId = "direct", isPresent = true }
        };
        CollectionAssert.AreEquivalent(new[] { "A" }, ChestPossibleSelection.Select(new[] { "A", "B", "new" }, 0, -99, states));
    }

    [TestCase(0, 1)]
    [TestCase(3, 4)]
    [TestCase(20, 7)]
    public void Builder_ProducesOnlySelectedChests_AndKeepsDirectChest(int maximum, int expectedChests)
    {
        var setup = CreateLayout(false);
        DungeonRoomBuilder builder = CreateBuilder();
        builder.ConfigurePossibleChests(maximum);
        Assert.That(builder.TryBuild(setup), Is.True);
        var chests = builder.GeneratedRoomObjects.Where(o => o != null && o.GetComponent<TreasureChest>() != null).ToArray();
        Assert.That(chests.Length, Is.EqualTo(expectedChests));
        Assert.That(chests.Any(o => o.name.EndsWith("_Direct")), Is.True);
        Assert.That(builder.GeneratedRoomObjects.Any(o => o != null && o.GetComponent<ChestPossible>() != null), Is.False);
        Assert.That(builder.CaptureGeneratedObjectStates().Count(s => s.isPresent), Is.EqualTo(expectedChests));
        Assert.That(builder.CaptureGeneratedObjectStates().Count, Is.EqualTo(7));
    }

    [Test]
    public void Builder_RestoresSelectedOpenedAndAbsentChests_WithoutReroll()
    {
        DungeonLayoutResult layout = CreateLayout(false);
        DungeonRoomBuilder first = CreateBuilder();
        first.ConfigurePossibleChests(3);
        Assert.That(first.TryBuild(layout), Is.True);
        GameObject opened = first.GeneratedRoomObjects.First(o => o != null && o.name.Contains("_Possible"));
        opened.GetComponent<TreasureChest>().RestoreOpenedStateForDungeon();
        var saved = first.CaptureGeneratedObjectStates();
        string openedId = saved.Single(s => s.isChestOpened).stateId;
        var absent = saved.First(s => s.isPresent && !s.isChestOpened && !s.stateId.EndsWith(":Direct"));
        absent.isPresent = false;

        DungeonRoomBuilder restored = CreateBuilder();
        restored.ConfigurePossibleChests(6);
        Assert.That(restored.TryBuild(layout, DungeonBuildOptions.Full, saved), Is.True);
        restored.RestoreGeneratedObjectStates(saved);
        var result = restored.CaptureGeneratedObjectStates();
        CollectionAssert.AreEquivalent(saved.Where(s => s.isPresent).Select(s => s.stateId), result.Where(s => s.isPresent).Select(s => s.stateId));
        Assert.That(result.Single(s => s.stateId == openedId).isChestOpened, Is.True);
        Assert.That(result.Single(s => s.stateId == absent.stateId).isPresent, Is.False);
    }

    [Test]
    public void CandidatePose_UsesTransformedGridAndMarkerOffset()
    {
        DungeonLayoutResult layout = CreateLayout(false);
        DungeonRoomBuilder builder = CreateBuilder();
        builder.transform.SetPositionAndRotation(new Vector3(-49.35f, -2.04f), Quaternion.Euler(0f, 0f, 30f));
        builder.transform.localScale = new Vector3(1.5f, 1.2f, 1f);
        builder.ConfigurePossibleChests(20);
        Assert.That(builder.TryBuild(layout), Is.True);
        GameObject chest = builder.GeneratedRoomObjects.Single(o => o.name == "RoomObject_1_Possible0");
        Vector3 expected = builder.FloorTilemap.GetCellCenterWorld(new Vector3Int(11, 1, 0)) + builder.transform.TransformVector(new Vector3(0.2f, -0.1f));
        Assert.That(Vector3.Distance(chest.transform.position, expected), Is.LessThan(0.001f));
        Assert.That(Quaternion.Angle(chest.transform.rotation, builder.transform.rotation * Quaternion.Euler(0f, 0f, 15f)), Is.LessThan(0.001f));
    }

    [Test]
    public void SelectedKillLock_BindsOnlyItsOwnRoom_AndDirectLinksAreUntouched()
    {
        DungeonLayoutResult layout = CreateLayout(true);
        DungeonRoomBuilder builder = CreateBuilder();
        builder.ConfigurePossibleChests(20);
        Assert.That(builder.TryBuild(layout), Is.True);
        var candidateLock = builder.GeneratedRoomObjects.Single(o => o.name == "RoomObject_0_Possible0").GetComponent<ChestMonsterKillLock>();
        var directLock = builder.GeneratedRoomObjects.Single(o => o.name == "RoomObject_0_Direct").GetComponent<ChestMonsterKillLock>();
        var point = builder.GeneratedRoomObjects.Single(o => o.name == "RoomObject_0_Monster").GetComponent<MonsterSpawnContainer>();
        Assert.That(point.LinkedChestKillLock, Is.SameAs(directLock));
        Assert.That(candidateLock.IsUnlocked, Is.False, "Deferred room spawns must lock the selected chest before room entry.");
        Assert.That(builder.TryGetGeneratedRoomEncounter(0, out MonsterSpawnRoomGroup firstGroup, out _), Is.True);
        Assert.That(builder.TryGetGeneratedRoomEncounter(1, out MonsterSpawnRoomGroup otherGroup, out _), Is.True);

        // End the deferred phase without launching the production spawn/audio services.
        SetField(firstGroup, "roomEntrySpawnStarted", true);
        firstGroup.PushEncounterHold();
        SetField(firstGroup, "pendingRoomEntrySpawnCount", 1);
        Refresh(candidateLock);
        Assert.That(candidateLock.IsUnlocked, Is.False);
        var ownMonster = new GameObject("OwnRoomMonster");
        firstGroup.NotifyMonsterSpawned(ownMonster);
        otherGroup.NotifyMonsterSpawned(new GameObject("OtherRoomMonster"));
        SetField(firstGroup, "pendingRoomEntrySpawnCount", 0);
        firstGroup.PopEncounterHold();
        Refresh(candidateLock);
        Assert.That(candidateLock.RemainingAliveCount, Is.EqualTo(1));
        var tracked = new List<GameObject>();
        candidateLock.GetAliveMonstersNonAlloc(tracked);
        CollectionAssert.AreEqual(new[] { ownMonster }, tracked);
        Object.DestroyImmediate(ownMonster);
        Refresh(candidateLock);
        Assert.That(candidateLock.IsUnlocked, Is.True, "The other room's live monster must not hold this chest.");
        Assert.That(directLock.IsUnlocked, Is.True, "Candidate binding must not inject new links into direct chests.");
    }

    [Test]
    public void KillLock_RestoredConsumedSpawnPointsAndEmptyRoomsDoNotStayLocked()
    {
        var group = new GameObject("Group").AddComponent<MonsterSpawnRoomGroup>();
        var point = new GameObject("ConsumedPoint").AddComponent<MonsterSpawnContainer>();
        point.ConfigureRuntime(new GameObject("Source"), null, group, null);
        var chestLock = new GameObject("Lock").AddComponent<ChestMonsterKillLock>();
        chestLock.BindRoomEncounter(group, new[] { point });
        Assert.That(chestLock.IsUnlocked, Is.False);
        point.gameObject.SetActive(false);
        Refresh(chestLock);
        Assert.That(chestLock.IsUnlocked, Is.True);
        chestLock.BindRoomEncounter(null, Array.Empty<MonsterSpawnContainer>());
        Assert.That(chestLock.IsUnlocked, Is.True);
    }

    [Test]
    public void Minimap_CountsOnlyRealSelectedAndDirectChests()
    {
        DungeonLayoutResult layout = CreateLayout(false);
        DungeonRoomBuilder builder = CreateBuilder();
        builder.transform.position = new Vector3(-49.35f, -2.04f);
        builder.ConfigurePossibleChests(2);
        Assert.That(builder.TryBuild(layout), Is.True);
        using var tracker = new DungeonMapContentTracker(builder.gameObject.scene,
            DungeonMapGraphSnapshot.Create(layout), null, builder.FloorTilemap);
        Assert.That(tracker.Model.GetContents(0).ClosedChestCount + tracker.Model.GetContents(1).ClosedChestCount, Is.EqualTo(3));
        GameObject opened = builder.GeneratedRoomObjects.First(o => o.name.Contains("_Possible"));
        opened.GetComponent<TreasureChest>().RestoreOpenedStateForDungeon();
        Assert.That(tracker.Model.GetContents(0).OpenedChestCount + tracker.Model.GetContents(1).OpenedChestCount, Is.EqualTo(1));
    }

    [Test]
    public void VisualPreview_DoesNotInstantiateCandidatesOrDirectGameplayChests()
    {
        DungeonRoomBuilder builder = CreateBuilder();
        builder.ConfigurePossibleChests(20);
        Assert.That(builder.TryBuild(CreateLayout(false), DungeonBuildOptions.VisualOnly), Is.True);
        Assert.That(builder.GeneratedRoomObjects, Is.Empty);
        Assert.That(builder.CaptureGeneratedObjectStates(), Is.Empty);
    }

    [Test]
    public void AuthoredMarkers_HaveNoInteractableChest_AndKeepRootSourceReferences()
    {
        foreach (string name in new[] { "ChestPossible", "ChestPossible_KillLock" })
        {
            var marker = Load(name).GetComponent<ChestPossible>();
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.ChestPrefab, Is.Not.Null);
            Assert.That(marker.ChestPrefab.transform.parent, Is.Null);
            Assert.That(marker.GetComponentInChildren<TreasureChest>(true), Is.Null);
            Assert.That(marker.GetComponentInChildren<Collider2D>(true), Is.Null);
        }
    }

    private DungeonLayoutResult CreateLayout(bool monsters)
    {
        var layout = (DungeonLayoutResult)Activator.CreateInstance(typeof(DungeonLayoutResult), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { 7391, 2 }, null);
        var tile = ScriptableObject.CreateInstance<Tile>();
        assets.Add(tile);
        for (int roomIndex = 0; roomIndex < 2; roomIndex++)
        {
            var template = ScriptableObject.CreateInstance<RoomTemplateSO>();
            template.name = "TestRoom" + roomIndex;
            assets.Add(template);
            var floor = new List<RoomTileData>();
            for (int x = 0; x < 7; x++)
                for (int y = 0; y < 7; y++)
                    floor.Add(new RoomTileData { localCell = new Vector2Int(x, y), tile = tile });
            var placements = new List<RoomObjectPlacementData>();
            for (int i = 0; i < 3; i++)
                placements.Add(new RoomObjectPlacementData
                {
                    placementId = "Possible" + i, kind = RoomObjectKind.Prop,
                    prefab = Load(monsters ? "ChestPossible_KillLock" : "ChestPossible"),
                    localCell = new Vector2Int(i + 1, 1), localScale = Vector3.one,
                    localOffset = new Vector2(0.2f, -0.1f), localRotationDegrees = 15f
                });
            if (roomIndex == 0)
                placements.Add(new RoomObjectPlacementData { placementId = "Direct", kind = RoomObjectKind.Chest,
                    prefab = Load(monsters ? "KillLockTresureChest" : "TreasureChest"), localCell = new Vector2Int(2, 4), localScale = Vector3.one });
            if (monsters)
                placements.Add(new RoomObjectPlacementData { placementId = "Monster", kind = RoomObjectKind.Monster,
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/CommonCorridor/GoblinGunner.prefab"),
                    localCell = new Vector2Int(3, 3), localScale = Vector3.one,
                    linkedChestLockPlacementId = roomIndex == 0 ? "Direct" : string.Empty });
            var bounds = new RectInt(0, 0, 7, 7);
            template.EditorSetData(new RoomLayoutData { roomId = "TestRoom" + roomIndex, roomType = RoomType.Combat,
                size = new Vector2Int(7, 7), localBounds = bounds, sockets = new List<RoomSocketData>() },
                new RoomBuildData { floorTiles = floor, wallTiles = new List<RoomTileData>(), objectPlacements = placements });
            var room = (DungeonRoomPlacement)Activator.CreateInstance(typeof(DungeonRoomPlacement), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { roomIndex, template, new Vector2Int(roomIndex * 10, 0), new RectInt(roomIndex * 10, 0, 7, 7), 0, false, false }, null);
            typeof(DungeonLayoutResult).GetMethod("AddRoom", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(layout, new object[] { room });
        }
        return layout;
    }

    private static DungeonRoomBuilder CreateBuilder()
    {
        var root = new GameObject("CandidateDungeon", typeof(Grid));
        var builder = root.AddComponent<DungeonRoomBuilder>();
        var floor = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
        var wall = new GameObject("Wall", typeof(Tilemap), typeof(TilemapRenderer));
        floor.transform.SetParent(root.transform, false);
        wall.transform.SetParent(root.transform, false);
        builder.EditorAssignTilemaps(floor.GetComponent<Tilemap>(), wall.GetComponent<Tilemap>());
        return builder;
    }

    private static GameObject Load(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPath + name + ".prefab");
        Assert.That(prefab, Is.Not.Null, name);
        return prefab;
    }

    private static void SetField(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static void Refresh(ChestMonsterKillLock chestLock) => typeof(ChestMonsterKillLock).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(chestLock, null);
}
#endif

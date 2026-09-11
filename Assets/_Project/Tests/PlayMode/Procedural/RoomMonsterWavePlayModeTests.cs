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

/// <summary>Verifies room waves, split-aware clear, pending locks, cancellation and persisted authoring/runtime state.</summary>
public sealed class RoomMonsterWavePlayModeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> owned = new();
    private readonly List<GameObject> spawned = new();
    private GameObject source;
    private MonsterSpawner spawner;
    private float previousTimeScale;

    [SetUp]
    public void SetUp()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        Assert.That(MonsterSpawner.Instance == null, Is.True, "Tests require an isolated scene.");
        spawner = Own(new GameObject("WaveTestSpawner")).AddComponent<MonsterSpawner>();
        source = Own(new GameObject("WaveTestMonster"));
        source.SetActive(false);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var group in owned.OfType<GameObject>().Where(o => o != null)
                     .SelectMany(o => o.GetComponentsInChildren<MonsterSpawnRoomGroup>(true)).ToArray())
            group.enabled = false;
        foreach (GameObject monster in spawned) if (monster != null) Object.DestroyImmediate(monster);
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        spawned.Clear();
        owned.Clear();
        Time.timeScale = previousTimeScale;
    }

    [UnityTest]
    public IEnumerator LegacyRoom_SpawnsOnce_AndDoesNotRestartOnReentry()
    {
        var group = CreateGroup();
        AddPoint(group, null);
        AddPoint(group, null);
        group.NotifyPlayerEnteredEncounter();
        Assert.That(spawned.Count, Is.EqualTo(2));
        Assert.That(group.WaveCount, Is.EqualTo(1));
        group.NotifyPlayerExitedEncounter();
        group.NotifyPlayerEnteredEncounter();
        Assert.That(spawned.Count, Is.EqualTo(2));
        DestroySpawned();
        yield return Until(() => group.RoomWavesCompleted);
        Assert.That(group.RemainingRegisteredOrPendingCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator Waves_KeepFutureChestAndRoomLocks_AndWaitForDelay()
    {
        var group = CreateGroup(TwoWaves(0.3f));
        var direct = Own(new GameObject("DirectLinkedChest")).AddComponent<ChestMonsterKillLock>();
        var wholeRoom = Own(new GameObject("WholeRoomChest")).AddComponent<ChestMonsterKillLock>();
        var first = AddPoint(group, RoomMonsterWaveDefinition.DefaultId, direct);
        var second = AddPoint(group, "second", direct);
        wholeRoom.BindRoomEncounter(group, new[] { first, second });
        group.NotifyPlayerEnteredEncounter();
        Assert.That(spawned.Count, Is.EqualTo(1));
        Assert.That(group.PendingRoomEntrySpawnCount, Is.EqualTo(1));
        Object.DestroyImmediate(spawned[0]);
        yield return Until(() => group.CurrentWaveNumber == 2);
        Refresh(direct);
        Refresh(wholeRoom);
        Assert.That(direct.IsUnlocked, Is.False, "A specifically linked future spawn must still lock the chest.");
        Assert.That(wholeRoom.IsUnlocked, Is.False);
        Assert.That(group.EncounterHoldCount, Is.EqualTo(1));
        Assert.That(spawned.Count, Is.EqualTo(1), "The next delay must precede actual spawn.");
        yield return Until(() => spawned.Count == 2);
        Object.DestroyImmediate(spawned[1]);
        yield return Until(() => group.RoomWavesCompleted);
        Refresh(direct);
        Refresh(wholeRoom);
        Assert.That(direct.IsUnlocked && wholeRoom.IsUnlocked, Is.True);
        Assert.That(group.RemainingRegisteredOrPendingCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator ExplicitChest_OnlyWaitsForLinkedWaves_NotEntireRoom()
    {
        var group = CreateGroup(TwoWaves(0.2f));
        var direct = Own(new GameObject("OnlyFirstWaveChest")).AddComponent<ChestMonsterKillLock>();
        AddPoint(group, RoomMonsterWaveDefinition.DefaultId, direct);
        AddPoint(group, "second");
        group.NotifyPlayerEnteredEncounter();
        Object.DestroyImmediate(spawned[0]);
        yield return Until(() => group.CurrentWaveNumber == 2);
        Refresh(direct);
        Assert.That(direct.IsUnlocked, Is.True);
        Assert.That(group.RoomWavesCompleted, Is.False);
    }

    [UnityTest]
    public IEnumerator SplitMember_BlocksAdvance_UntilLastChildDies()
    {
        var group = CreateGroup(TwoWaves(0f));
        AddPoint(group, RoomMonsterWaveDefinition.DefaultId);
        AddPoint(group, "second");
        group.NotifyPlayerEnteredEncounter();
        var tickets = (IList)Get(group, "waveTickets");
        object unit = tickets[0].GetType().GetField("Unit").GetValue(tickets[0]);
        GameObject child = Own(new GameObject("SplitChild"));
        unit.GetType().GetMethod("AddMember").Invoke(unit, new object[] { child });
        Object.DestroyImmediate(spawned[0]);
        yield return new WaitForSeconds(0.25f);
        Assert.That(group.CurrentWaveNumber, Is.EqualTo(1));
        Assert.That(spawned.Count, Is.EqualTo(1));
        Object.DestroyImmediate(child);
        yield return Until(() => spawned.Count == 2);
    }

    [UnityTest]
    public IEnumerator ExternalEncounterHoldAndMonster_DoNotBlockWaveAdvance()
    {
        var group = CreateGroup(TwoWaves(0f));
        AddPoint(group, RoomMonsterWaveDefinition.DefaultId);
        AddPoint(group, "second");
        group.PushEncounterHold();
        group.NotifyMonsterSpawned(Own(new GameObject("IndependentAlarmMonster")));
        group.NotifyPlayerEnteredEncounter();
        Object.DestroyImmediate(spawned[0]);
        yield return Until(() => spawned.Count == 2);
        Object.DestroyImmediate(spawned[1]);
        yield return Until(() => group.RoomWavesCompleted);
        Assert.That(group.EncounterHoldCount, Is.EqualTo(1));
        Assert.That(group.RemainingRegisteredOrPendingCount, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator PendingVfx_DisableReenable_ReleasesAndReacquiresReservations()
    {
        var group = CreateGroup(TwoWaves(0f));
        var chest = Own(new GameObject("VfxChest")).AddComponent<ChestMonsterKillLock>();
        Set(group, "spawnVfxPrefab", Own(new GameObject("TestSpawnVfx")));
        Set(group, "spawnVfxDelaySeconds", 0.15f);
        AddPoint(group, RoomMonsterWaveDefinition.DefaultId, chest);
        AddPoint(group, "second", chest);
        group.NotifyPlayerEnteredEncounter();
        Assert.That(spawned.Count, Is.Zero);
        Assert.That(group.PendingRoomEntrySpawnCount, Is.EqualTo(2));
        group.enabled = false;
        Assert.That(group.PendingRoomEntrySpawnCount, Is.Zero);
        Assert.That(group.EncounterHoldCount, Is.Zero);
        Refresh(chest);
        Assert.That(chest.IsUnlocked, Is.True);
        group.enabled = true;
        Assert.That(group.PendingRoomEntrySpawnCount, Is.EqualTo(2));
        yield return Until(() => spawned.Count == 1);
        yield return new WaitForSeconds(0.2f);
        Assert.That(spawned.Count, Is.EqualTo(1));
        Assert.That(group.CurrentWaveNumber, Is.EqualTo(1));
        Object.DestroyImmediate(spawned[0]);
        yield return Until(() => spawned.Count == 2);
    }

    [UnityTest]
    public IEnumerator MissingSpawner_CompletesWithoutPermanentLocks()
    {
        Object.DestroyImmediate(spawner.gameObject);
        var group = CreateGroup(TwoWaves(0f));
        var chest = Own(new GameObject("FailureChest")).AddComponent<ChestMonsterKillLock>();
        AddPoint(group, RoomMonsterWaveDefinition.DefaultId, chest);
        AddPoint(group, "second", chest);
        group.NotifyPlayerEnteredEncounter();
        yield return Until(() => group.RoomWavesCompleted);
        Assert.That(group.RemainingRegisteredOrPendingCount, Is.Zero);
        Refresh(chest);
        Assert.That(chest.IsUnlocked, Is.True);
    }

    [UnityTest]
    public IEnumerator RestoreDelay_SkipsEarlierWaves_AndPreservesRemainingTime()
    {
        var group = CreateGroup(TwoWaves(10f));
        AddPoint(group, RoomMonsterWaveDefinition.DefaultId);
        AddPoint(group, "second");
        group.RestoreWaveState(new DungeonRoomWaveRuntimeStateData
        {
            hasStarted = true, currentWaveId = "second", remainingDelaySeconds = 0.3f
        });
        group.NotifyPlayerEnteredEncounter();
        Assert.That(group.CurrentWaveNumber, Is.EqualTo(2));
        Assert.That(spawned.Count, Is.Zero);
        yield return Until(() => spawned.Count == 1);
        Assert.That(group.CurrentWaveNumber, Is.EqualTo(2));
        Object.DestroyImmediate(spawned[0]);
        yield return Until(() => group.RoomWavesCompleted);
        var restored = CreateGroup(TwoWaves(0f));
        AddPoint(restored, RoomMonsterWaveDefinition.DefaultId);
        restored.RestoreWaveState(group.CaptureWaveState());
        restored.NotifyPlayerEnteredEncounter();
        Assert.That(restored.RoomWavesCompleted, Is.True);
        Assert.That(spawned.Count, Is.EqualTo(1), "Completed encounters never spawn again.");
    }

    [UnityTest]
    public IEnumerator ReorderedWaves_KeepStableMonsterMembership()
    {
        var waves = TwoWaves(0f);
        Array.Reverse(waves);
        var group = CreateGroup(waves);
        var legacy = AddPoint(group, null);
        var second = AddPoint(group, "second");
        second.transform.position = Vector3.right * 8f;
        group.NotifyPlayerEnteredEncounter();
        Assert.That(spawned.Single().transform.position.x, Is.EqualTo(8f));
        Object.DestroyImmediate(spawned[0]);
        yield return Until(() => spawned.Count == 2);
        Assert.That(spawned[1].transform.position, Is.EqualTo(legacy.SpawnPosition));
    }

    [Test]
    public void Authoring_RoundTripsStableWaveIds_AndLegacyDefaults()
    {
        var root = Own(new GameObject("Authoring", typeof(Grid), typeof(RoomPieceAuthoring)));
        var authoring = root.GetComponent<RoomPieceAuthoring>();
        authoring.EditorAssignTilemaps(root.GetComponent<Grid>(), null, null);
        var waves = TwoWaves(0.7f);
        authoring.EditorSetMonsterWaves(waves);
        var marker = Own(new GameObject("MonsterMarker")).AddComponent<RoomObjectAuthoring>();
        marker.transform.SetParent(root.transform);
        marker.EditorConfigure("monster", RoomObjectKind.Monster, source, RoomMonsterSpawnRole.Warrior, null);
        marker.EditorSetPlacement(new RoomObjectPlacementData
        {
            kind = RoomObjectKind.Monster, monsterWaveId = "second", localCell = Vector2Int.one, localScale = Vector3.one
        });
        Assert.That(marker.TryGetPlacementData(out var placement), Is.True);
        Assert.That(placement.monsterWaveId, Is.EqualTo("second"));
        var template = Own(ScriptableObject.CreateInstance<RoomTemplateSO>());
        template.EditorSetData(default, new RoomBuildData
        {
            monsterWaves = RoomMonsterWaveDefinition.CopyOrDefault(authoring.MonsterWaves),
            objectPlacements = new List<RoomObjectPlacementData> { placement }
        });
        string json = JsonUtility.ToJson(template);
        var copy = Own(ScriptableObject.CreateInstance<RoomTemplateSO>());
        JsonUtility.FromJsonOverwrite(json, copy);
        Assert.That(copy.BuildData.monsterWaves[1].startDelaySeconds, Is.EqualTo(0.7f));
        Assert.That(copy.BuildData.objectPlacements[0].monsterWaveId, Is.EqualTo("second"));
        Assert.That(RoomMonsterWaveDefinition.ResolveId(null), Is.EqualTo(RoomMonsterWaveDefinition.DefaultId));
    }

    [Test]
    public void AuthoringValidator_RejectsOrphansDuplicateIdsAndInvalidStageSources()
    {
        var root = Own(new GameObject("InvalidWaveAuthoring", typeof(RoomPieceAuthoring)));
        var authoring = root.GetComponent<RoomPieceAuthoring>();
        authoring.EditorSetMonsterWaves(new[]
        {
            new RoomMonsterWaveDefinition { id = "duplicate", startDelaySeconds = float.NaN },
            new RoomMonsterWaveDefinition { id = "duplicate" }
        });
        var marker = Own(new GameObject("Orphan")).AddComponent<RoomObjectAuthoring>();
        marker.transform.SetParent(root.transform);
        var stageSet = Own(ScriptableObject.CreateInstance<StageMonsterSetSO>());
        stageSet.EditorSetStagePrefabs(new GameObject[] { null });
        marker.EditorConfigure("orphan", RoomObjectKind.Monster, null, RoomMonsterSpawnRole.Warrior, stageSet);
        marker.EditorSetMonsterWaveId("missing");
        var errors = new List<string>();
        Type editorType = Type.GetType("RoomPieceEditorWindow, Editor", throwOnError: true);
        editorType.GetMethod("ValidateMonsterWaves", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { authoring, errors });
        Assert.That(errors.Count, Is.EqualTo(4));
    }

    [UnityTest]
    public IEnumerator Door_StaysClosedBetweenWaves_AndOpensAfterFinalClear()
    {
        var group = CreateGroup(TwoWaves(0.2f));
        var doorRoot = Own(new GameObject("WaveDoor"));
        doorRoot.SetActive(false);
        var door = doorRoot.AddComponent<DoorObject>();
        door.doorID = "wave_test_door";
        door.doorType = DoorObject.DoorType.Normal;
        door.isPermanent = false;
        var lockRoot = Own(new GameObject("WaveDoorLock"));
        lockRoot.SetActive(false);
        var doorLock = lockRoot.AddComponent<RoomDoorMonsterKillLock>();
        doorLock.Configure(door, group);
        Set(doorLock, "requireTrackedMonstersInsideBeforeClose", false);
        Set(doorLock, "openAfterAllClearedDelaySeconds", 0f);
        doorRoot.SetActive(true);
        lockRoot.SetActive(true);
        yield return null;
        AddPoint(group, RoomMonsterWaveDefinition.DefaultId);
        AddPoint(group, "second");
        Assert.That(door.IsOpen, Is.True);
        group.NotifyPlayerEnteredEncounter();
        Assert.That(door.IsOpen, Is.False);
        Object.DestroyImmediate(spawned[0]);
        yield return Until(() => group.CurrentWaveNumber == 2);
        Assert.That(door.IsOpen, Is.False);
        yield return Until(() => spawned.Count == 2);
        Object.DestroyImmediate(spawned[1]);
        yield return Until(() => group.RoomWavesCompleted && door.IsOpen);
        Assert.That(doorLock.RemainingMonsterCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator LegacySpawnProfile_KeepsItsConfiguredCount()
    {
        var profile = Own(ScriptableObject.CreateInstance<MonsterRoomSpawnProfileSO>());
        var table = new MonsterRoomSpawnProfileSO.SpawnTable();
        Set(table, "spawnCount", 2);
        Set(table, "entries", new List<MonsterRoomSpawnProfileSO.WeightedMonsterEntry>
        {
            new() { monsterPrefab = source, weight = 1f }
        });
        Set(profile, "spawnTables", new List<MonsterRoomSpawnProfileSO.SpawnTable> { table });
        var group = CreateGroup();
        group.ConfigureSpawnProfile(profile);
        for (int i = 0; i < 4; i++) AddPoint(group, null);
        group.NotifyPlayerEnteredEncounter();
        Assert.That(spawned.Count, Is.EqualTo(2));
        DestroySpawned();
        yield return Until(() => group.RoomWavesCompleted);
        Assert.That(group.RemainingRegisteredOrPendingCount, Is.Zero);
    }

    [Test]
    public void Builder_ReentryKeepsFutureAnchorsAndDeadStates_AndRestoresWaveCursor()
    {
        var template = Own(ScriptableObject.CreateInstance<RoomTemplateSO>());
        var tile = Own(ScriptableObject.CreateInstance<Tile>());
        var floor = new List<RoomTileData>();
        for (int x = 0; x < 8; x++) for (int y = 0; y < 8; y++)
            floor.Add(new RoomTileData { localCell = new Vector2Int(x, y), tile = tile });
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/CommonCorridor/GoblinGunner.prefab");
        template.EditorSetData(new RoomLayoutData
        {
            roomId = "waves", roomType = RoomType.Combat, size = new Vector2Int(8, 8),
            localBounds = new RectInt(0, 0, 8, 8), sockets = new List<RoomSocketData>()
        }, new RoomBuildData
        {
            floorTiles = floor, monsterWaves = new List<RoomMonsterWaveDefinition>(TwoWaves(1f)),
            objectPlacements = new List<RoomObjectPlacementData>
            {
                new() { placementId = "dead", kind = RoomObjectKind.Monster, prefab = prefab,
                    localCell = new Vector2Int(2, 2), localScale = Vector3.one },
                new() { placementId = "future", kind = RoomObjectKind.Monster, prefab = prefab, monsterWaveId = "second",
                    localCell = new Vector2Int(4, 4), localScale = Vector3.one }
            }
        });
        var layout = (DungeonLayoutResult)Activator.CreateInstance(typeof(DungeonLayoutResult), PrivateInstance,
            null, new object[] { 42, 1 }, null);
        var room = (DungeonRoomPlacement)Activator.CreateInstance(typeof(DungeonRoomPlacement), PrivateInstance,
            null, new object[] { 0, template, Vector2Int.zero, new RectInt(0, 0, 8, 8), 0, false, false }, null);
        typeof(DungeonLayoutResult).GetMethod("AddRoom", PrivateInstance).Invoke(layout, new object[] { room });
        var builder = CreateBuilder();
        Assert.That(builder.TryBuild(layout), Is.True);
        var saved = builder.CaptureGeneratedObjectStates();
        saved.Single(s => s.stateId == "0:dead").isPresent = false;
        saved.Single(s => s.roomWaves != null).roomWaves = new DungeonRoomWaveRuntimeStateData
        {
            hasStarted = true, currentWaveId = "second", remainingDelaySeconds = 0.7f
        };
        var restored = CreateBuilder();
        Assert.That(restored.TryBuild(layout, DungeonBuildOptions.Full, saved), Is.True);
        restored.RestoreGeneratedObjectStates(saved);
        var snapshot = restored.CaptureGeneratedObjectStates();
        Assert.That(snapshot.Single(s => s.stateId == "0:dead").isPresent, Is.False);
        Assert.That(snapshot.Single(s => s.stateId == "0:future").isPresent, Is.True);
        Assert.That(snapshot.Single(s => s.roomWaves != null).roomWaves.currentWaveId, Is.EqualTo("second"));
        Assert.That(snapshot.Single(s => s.roomWaves != null).roomWaves.remainingDelaySeconds, Is.EqualTo(0.7f));
    }

    [Test]
    public void RunStore_DeepCopiesWaveState_AndResetsItOnNewRun()
    {
        var manager = GamePlayDataManager.EnsureInstance();
        manager.ResetForDevelopmentStart();
        manager.StartRun();
        var state = new DungeonRoomWaveRuntimeStateData { hasStarted = true, currentWaveId = "second", remainingDelaySeconds = 0.4f };
        try
        {
            manager.ResolveDungeonSeed("wave_test", DungeonReentryPolicy.PreserveDuringRun, 42);
            manager.SaveDungeonObjectStates("wave_test", new[] { new DungeonObjectRuntimeStateData { stateId = "room-wave:0", roomWaves = state } });
            state.currentWaveId = "mutated";
            var restored = new List<DungeonObjectRuntimeStateData>();
            Assert.That(manager.TryGetDungeonObjectStates("wave_test", restored), Is.True);
            Assert.That(restored.Single().roomWaves.currentWaveId, Is.EqualTo("second"));
            restored[0].roomWaves.currentWaveId = "mutated-again";
            manager.TryGetDungeonObjectStates("wave_test", restored);
            Assert.That(restored.Single().roomWaves.currentWaveId, Is.EqualTo("second"));
            manager.StartRun();
            Assert.That(manager.TryGetDungeonObjectStates("wave_test", restored), Is.False);
        }
        finally { manager.ResetForDevelopmentStart(); }
    }

    private MonsterSpawnRoomGroup CreateGroup(RoomMonsterWaveDefinition[] waves = null)
    {
        var group = Own(new GameObject("WaveTestGroup")).AddComponent<MonsterSpawnRoomGroup>();
        if (waves != null) group.ConfigureWaves(waves);
        var settings = Own(ScriptableObject.CreateInstance<MonsterRoomEntrySpawnSettingsSO>());
        Set(group, "cachedDefaultSpawnSettings", settings);
        Set(group, "postSpawnIdleSeconds", 0f);
        return group;
    }

    private MonsterSpawnContainer AddPoint(MonsterSpawnRoomGroup group, string waveId, ChestMonsterKillLock chest = null)
    {
        var point = Own(new GameObject("WavePoint")).AddComponent<MonsterSpawnContainer>();
        point.transform.SetParent(group.transform, false);
        point.ConfigureRuntime(source, null, group, chest, monster => { if (monster != null) spawned.Add(monster); });
        point.ConfigureWave(waveId);
        return point;
    }

    private DungeonRoomBuilder CreateBuilder()
    {
        var root = Own(new GameObject("WaveDungeon", typeof(Grid)));
        var builder = root.AddComponent<DungeonRoomBuilder>();
        var floor = new GameObject("Floor", typeof(Tilemap), typeof(TilemapRenderer));
        var wall = new GameObject("Wall", typeof(Tilemap), typeof(TilemapRenderer));
        floor.transform.SetParent(root.transform, false);
        wall.transform.SetParent(root.transform, false);
        builder.EditorAssignTilemaps(floor.GetComponent<Tilemap>(), wall.GetComponent<Tilemap>());
        return builder;
    }

    private static RoomMonsterWaveDefinition[] TwoWaves(float delay) => new[]
    {
        new RoomMonsterWaveDefinition { id = RoomMonsterWaveDefinition.DefaultId, displayName = "First" },
        new RoomMonsterWaveDefinition { id = "second", displayName = "Second", startDelaySeconds = delay }
    };
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private void DestroySpawned() { foreach (GameObject monster in spawned) if (monster != null) Object.DestroyImmediate(monster); }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
    private static object Get(object target, string field) => target.GetType().GetField(field, PrivateInstance).GetValue(target);
    private static void Refresh(ChestMonsterKillLock chest) => typeof(ChestMonsterKillLock).GetMethod("Update", PrivateInstance).Invoke(chest, null);
    private static IEnumerator Until(Func<bool> predicate)
    {
        float timeout = Time.realtimeSinceStartup + 3f;
        while (!predicate() && Time.realtimeSinceStartup < timeout) yield return null;
        Assert.That(predicate(), Is.True, "Timed out waiting for wave progression.");
    }
}
#endif

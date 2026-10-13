using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Editor-only, synchronous A/B position queries. No gameplay objects are spawned by the queries.</summary>
public static class LightningSpearPlacementBenchmark
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string ReportFolder = "Library/LightningSpearPlacementBenchmark";
    private static string FixtureBenchmarkMethod => Array.IndexOf(Environment.GetCommandLineArgs(), "-lightningCompareStructure") >= 0
        ? "RunStructureBenchmarkWithLoadout" : "RunPlacementBenchmarkWithLoadout";

    [Serializable]
    private sealed class ReportStatus
    {
        public bool allEquivalent;
        public bool allOptimizedValid;
        public bool allRngEquivalent;
        public bool allWorkEquivalent;
        public SampleCount[] samples;
    }

    [Serializable]
    private sealed class SampleCount
    {
        public int baselineCount;
        public int optimizedCount;
    }

    [MenuItem("Tools/Capstone/Validation/Lightning Spear/Benchmark Current Scene")]
    [MenuItem("Tools/Testing/Lightning Spear/Benchmark Equipped Weapon")]
    public static void Run()
    {
        RequirePlayMode();
        LightningSpearRuntimeState state = UnityEngine.Object.FindFirstObjectByType<LightningSpearRuntimeState>();
        if (state == null)
            throw new InvalidOperationException("Equip Lightning Spear in Play Mode first.");
        MethodInfo benchmark = typeof(LightningSpearRuntimeState).GetMethod("RunPlacementBenchmark", PrivateInstance);
        foreach (bool targeted in new[] { false, true })
        {
            string report = (string)benchmark.Invoke(state, new object[] { 20261007, 64, targeted });
            SaveReport(targeted ? "current-targeted" : "current-random", report);
        }
        Debug.Log("Lightning Spear placement benchmark passed. Reports: " + Path.GetFullPath(ReportFolder));
    }

    [MenuItem("Tools/Capstone/Validation/Lightning Spear/Run Placement Fixture Suite")]
    [MenuItem("Tools/Testing/Lightning Spear/Run Placement Fixture Suite")]
    public static void RunFixtureSuite()
    {
        RequirePlayMode();
        // This directory contains only this probe's generated reports. Do not let
        // an aborted earlier fixture run look like the current completed suite.
        if (Directory.Exists(ReportFolder))
            foreach (string previousReport in Directory.GetFiles(ReportFolder, "*.json"))
                File.Delete(previousReport);
        var loadout = AssetDatabase.LoadAssetAtPath<LightningSpearLoadout>(
            "Assets/_Project/Data/Items/Weapons/Loadouts/WAL_LightningSpear.asset");
        var data = AssetDatabase.LoadAssetAtPath<LightningSpearSkill2Data>(
            "Assets/_Project/Data/Items/Weapons/LogicData/ALData_LightningSpearSkill2.asset");
        if (loadout == null || data == null)
            throw new InvalidOperationException("Authored Lightning Spear loadout/skill data is missing.");
        string[] scenarios = { "open", "walls-and-holes", "overlapping-floor", "transformed-grid", "crowded-marks", "no-ground" };
        foreach (string scenario in scenarios)
        {
            using (var fixture = new PlacementFixture(scenario))
            {
                MethodInfo benchmark = typeof(LightningSpearRuntimeState).GetMethod(
                    FixtureBenchmarkMethod, PrivateInstance);
                foreach (bool targeted in new[] { false, true })
                {
                    string report = (string)benchmark.Invoke(fixture.State,
                        new object[] { loadout, data, fixture.Origin, 20261007, 40, targeted });
                    int expectedCount = scenario == "no-ground" ? 0 :
                        scenario == "open" || scenario == "overlapping-floor" || scenario == "transformed-grid" ? 6 : -1;
                    SaveReport(scenario + (targeted ? "-targeted" : "-random"), report, expectedCount);
                }
                fixture.VerifyNearestBoundaries(loadout, data);
                if (FixtureBenchmarkMethod == "RunStructureBenchmarkWithLoadout")
                {
                    MethodInfo third = typeof(LightningSpearRuntimeState).GetMethod(
                        "RunThirdStrikeBenchmarkWithLoadout", PrivateInstance);
                    SaveReport(scenario + "-third-strike", (string)third.Invoke(fixture.State,
                        new object[] { loadout, data, fixture.Origin, 20261007, 40 }));
                }
            }
        }
        Debug.Log("LIGHTNING_PLACEMENT_SUITE_PASS: 480 seeded A/B requests, exact output order/count/room, " +
            "legacy-rule validity, nearest tie order, zero spacing and growing physics buffers. Reports: " +
            Path.GetFullPath(ReportFolder));
    }

    private static void RequirePlayMode()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
            throw new InvalidOperationException("Run the placement benchmark in Play Mode after compilation completes.");
    }

    private static void SaveReport(string name, string report, int expectedCount = -1)
    {
        Directory.CreateDirectory(ReportFolder);
        string path = Path.Combine(ReportFolder, name + ".json");
        File.WriteAllText(path, report);
        ReportStatus status = JsonUtility.FromJson<ReportStatus>(report);
        if (!status.allEquivalent || !status.allOptimizedValid || !status.allRngEquivalent || !status.allWorkEquivalent)
            throw new InvalidOperationException("Placement A/B output or validity mismatch: " + Path.GetFullPath(path));
        if (expectedCount >= 0)
            foreach (SampleCount sample in status.samples)
                if (sample.baselineCount != expectedCount || sample.optimizedCount != expectedCount)
                    throw new InvalidOperationException("Fixture expected " + expectedCount + " positions: " + Path.GetFullPath(path));
    }

    private sealed class PlacementFixture : IDisposable
    {
        private readonly GameObject root;
        private readonly Tile tile;
        private readonly Tilemap floor;
        private readonly Tilemap walls;
        private readonly string scenario;
        internal LightningSpearRuntimeState State { get; }
        internal Vector2 Origin => root.transform.position;

        internal PlacementFixture(string scenario)
        {
            this.scenario = scenario;
            // Runtime FindObjectsByType intentionally excludes DontSave objects. These
            // PlayMode-only objects must be discoverable and are disposed before any save.
            root = new GameObject("LightningPlacementBenchmarkFixture");
            root.transform.position = new Vector3(1000f, 1000f, 0f);
            var grid = Child("Grid").AddComponent<Grid>();
            if (scenario == "transformed-grid")
            {
                grid.transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
                grid.transform.localScale = new Vector3(.8f, 1.2f, 1f);
            }
            floor = Child("Floor", grid.transform).AddComponent<Tilemap>();
            floor.gameObject.layer = LayerMask.NameToLayer("Ground");
            walls = Child("Wall", grid.transform).AddComponent<Tilemap>();
            walls.gameObject.layer = LayerMask.NameToLayer("Wall");
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.hideFlags = HideFlags.DontSave;
            if (scenario != "no-ground")
            {
                for (int x = -15; x < 15; x++)
                    for (int y = -15; y < 15; y++)
                    {
                        bool hole = scenario == "walls-and-holes" && x >= 2 && (x + y) % 4 == 0;
                        if (!hole) floor.SetTile(new Vector3Int(x, y, 0), tile);
                    }
            }
            var roomCollider = Child("RoomArea").AddComponent<BoxCollider2D>();
            roomCollider.size = new Vector2(30f, 30f);
            roomCollider.isTrigger = true;
            roomCollider.gameObject.AddComponent<MonsterRoomArea2D>().Configure(roomCollider);
            State = Child("PlacementRuntime").AddComponent<LightningSpearRuntimeState>();
            State.enabled = false;

            // A single target forces repeated nearest queries after the direct placement is reserved.
            GameObject target = Child("Target");
            target.transform.position = Origin + new Vector2(3.1f, .2f);
            target.AddComponent<BoxCollider2D>().size = new Vector2(.2f, .2f);
            target.AddComponent<TrainingDummy2D>();
            // Exercise result-list growth and multi-collider root deduplication (> a typical 32/64 buffer).
            for (int i = 0; i < 80; i++) target.AddComponent<CircleCollider2D>().radius = .08f;

            if (scenario == "walls-and-holes")
            {
                for (int y = -5; y <= 5; y++)
                {
                    walls.SetTile(new Vector3Int(3, y, 0), tile);
                    var blocker = Child("WallCollider" + y).AddComponent<BoxCollider2D>();
                    blocker.gameObject.layer = walls.gameObject.layer;
                    blocker.transform.position = Origin + new Vector2(3.5f, y + .5f);
                    blocker.size = Vector2.one;
                }
            }
            if (scenario == "overlapping-floor")
            {
                var duplicate = Child("FloorDuplicate", grid.transform).AddComponent<Tilemap>();
                duplicate.gameObject.layer = floor.gameObject.layer;
                foreach (Vector3Int cell in floor.cellBounds.allPositionsWithin)
                    if (floor.HasTile(cell)) duplicate.SetTile(cell, tile);
                var secondTarget = Child("SecondTarget");
                secondTarget.transform.position = Origin - new Vector2(3.1f, .2f);
                secondTarget.AddComponent<BoxCollider2D>().size = new Vector2(.2f, .2f);
                secondTarget.AddComponent<TrainingDummy2D>();
            }
            if (scenario == "crowded-marks")
            {
                for (int x = -5; x <= 5; x += 2)
                    for (int y = -5; y <= 5; y += 2)
                    {
                        LightningSpearMarkActor mark = Child("ExistingMark").AddComponent<LightningSpearMarkActor>();
                        mark.enabled = false;
                        mark.transform.position = Origin + new Vector2(x + .25f, y + .25f);
                        State.RegisterMark(mark);
                    }
            }
            Physics2D.SyncTransforms();
            if (!((IList)State.GetType().GetMethod("ResolveGroundTilemaps", PrivateInstance).Invoke(State, null)).Contains(floor))
                throw new InvalidOperationException("Fixture floor was not discovered by the runtime placement query.");
        }

        private GameObject Child(string name, Transform parent = null)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent != null ? parent : root.transform, false);
            return child;
        }

        internal void VerifyNearestBoundaries(LightningSpearLoadout loadout, LightningSpearSkill2Data data)
        {
            if (scenario != "open") return;
            // Clone data only: existing assets and their serialized schemas are never changed.
            LightningSpearSkill2Data zeroSpacing = UnityEngine.Object.Instantiate(data);
            try
            {
                typeof(LightningSpearSkill2Data).GetField("minMarkSpacing", PrivateInstance).SetValue(zeroSpacing, 0f);
                MethodInfo benchmark = typeof(LightningSpearRuntimeState).GetMethod(
                    FixtureBenchmarkMethod, PrivateInstance);
                SaveReport("zero-spacing-targeted", (string)benchmark.Invoke(State,
                    new object[] { loadout, zeroSpacing, Origin, 20261007, 16, true }), 6);

                // An equidistant fallback must keep the original x/y tie break.
                Vector2 lowerLeft = floor.GetCellCenterWorld(new Vector3Int(2, -1, 0));
                Vector2 upperLeft = floor.GetCellCenterWorld(new Vector3Int(2, 0, 0));
                Vector2 preferred = (lowerLeft + upperLeft) * .5f + Vector2.right * .5f;
                var centerBlocker = Child("TiePointBlocker").AddComponent<BoxCollider2D>();
                centerBlocker.gameObject.layer = walls.gameObject.layer;
                centerBlocker.transform.position = preferred;
                centerBlocker.size = Vector2.one * .1f;
                Physics2D.SyncTransforms();
                object world = State.GetType().GetField("placementWorld", PrivateInstance).GetValue(State);
                world.GetType().GetMethod("InvalidateTilemapCache", PrivateInstance).Invoke(world, null);
                var args = new object[] { loadout, data, Origin, null, preferred, null };
                object planner = State.GetType().GetProperty("PlacementPlanner", PrivateInstance).GetValue(State);
                ((IList)planner.GetType().GetProperty("Pending", PrivateInstance).GetValue(planner)).Clear();
                bool found = (bool)State.GetType().GetMethod("TryFindNearestMarkPosition", PrivateInstance).Invoke(State, args);
                var baselineArgs = new object[] { loadout, data, Origin, null, preferred, null };
                bool baselineFound = (bool)State.GetType().GetMethod("TryFindNearestMarkPositionBaseline", PrivateInstance)
                    .Invoke(State, baselineArgs);
                if (!found || !baselineFound || !((Vector2)args[5]).Equals(lowerLeft) ||
                    !((Vector2)args[5]).Equals((Vector2)baselineArgs[5]))
                    throw new InvalidOperationException("Nearest tie mismatch. Preferred=" + preferred +
                        ", expected=" + lowerLeft + ", optimized=" + args[5] + ", baseline=" + baselineArgs[5]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(zeroSpacing);
            }
        }

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(tile);
        }
    }
}

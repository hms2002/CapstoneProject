#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// Responsibility: verify corridor void-fill authoring, bounded background painting and rebuild cleanup.
public sealed class DungeonVoidFillPlayModeTests
{
    private const string TilePath = "Assets/_Project/Art/Tilemaps/TilePallet_Tile/Tiles_LevelSet_new/TileMap_B_126.asset";
    private const BindingFlags InternalInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> assets = new();
    private GameObject root;
    private DungeonRoomBuilder builder;
    private Tile fill;

    // Batch-mode entry point: inspect serialized scenes without starting their gameplay bootstraps or saving them.
    public static void VerifyAuthoredScenes()
    {
        foreach (string theme in new[] { "Shadow", "Dragon", "Slime" })
        {
            string path = $"Assets/_Project/Scenes/Procedural{theme}Corridor.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var builders = scene.GetRootGameObjects()
                    .SelectMany(o => o.GetComponentsInChildren<DungeonRoomBuilder>(true)).ToArray();
                Assert.That(builders.Length, Is.EqualTo(1), path);
                var current = builders[0];
                Assert.That(current.VoidFillTile, Is.SameAs(AssetDatabase.LoadAssetAtPath<Tile>(TilePath)), path);
                Assert.That(current.VoidFillPaddingCells, Is.EqualTo(8), path);
                Assert.That(new SerializedObject(current).FindProperty("createMissingVoidFillTilemap").boolValue, Is.True, path);
                foreach (var layer in RoomTileLayerContract.OrderedLayers)
                {
                    var map = current.GetTilemap(layer);
                    Assert.That(map, Is.Not.Null, path + ": " + layer);
                    var renderer = map.GetComponent<TilemapRenderer>();
                    if (renderer.sortingLayerName == "Default")
                        Assert.That(renderer.sortingOrder, Is.GreaterThan(30), path + ": " + layer);
                }
                Debug.Log($"[DungeonVoidFillVerification] {theme}: tile bound, padding=8, background behind authored room layers.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [SetUp]
    public void SetUp()
    {
        fill = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
        Assert.That(fill, Is.Not.Null);
        Assert.That(fill.sprite, Is.Not.Null);
        Assert.That(fill.sprite.bounds.size.x, Is.EqualTo(1f).Within(0.001f));
        Assert.That(fill.sprite.bounds.size.y, Is.EqualTo(1f).Within(0.001f));
        root = new GameObject("VoidFillTest", typeof(Grid));
        builder = root.AddComponent<DungeonRoomBuilder>();
        builder.EditorAssignTilemaps(CreateTilemap("Floor", 50), CreateTilemap("Wall", 60));
        builder.EditorAssignVoidFill(null, fill, 8);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        foreach (var asset in assets) Object.DestroyImmediate(asset);
        assets.Clear();
    }

    [Test]
    public void Build_FillsRoomBoundsAndPadding_BehindFloorWithoutPhysics()
    {
        Assert.That(builder.TryBuild(CreateLayout(new RectInt(-5, -3, 3, 2)), DungeonBuildOptions.VisualOnly), Is.True);
        var map = builder.VoidFillTilemap;
        Assert.That(map, Is.Not.Null);
        Assert.That(map.cellBounds, Is.EqualTo(new BoundsInt(-13, -11, 0, 19, 18, 1)));
        Assert.That(map.GetUsedTilesCount(), Is.EqualTo(1));
        foreach (var cell in map.cellBounds.allPositionsWithin)
            Assert.That(map.GetTile(cell), Is.SameAs(fill));
        Assert.That(map.HasTile(new Vector3Int(-14, -11)), Is.False);
        Assert.That(builder.FloorTilemap.GetTile(new Vector3Int(-5, -3)), Is.SameAs(fill));
        Assert.That(map, Is.Not.SameAs(builder.FloorTilemap));
        Assert.That(map.GetComponent<TilemapRenderer>().sortingOrder, Is.LessThan(50));
        Assert.That(map.GetComponents<Collider2D>(), Is.Empty);
        Assert.That(map.GetComponent<Rigidbody2D>(), Is.Null);
    }

    [Test]
    public void Rebuild_UsesSameBackgroundAndRemovesPreviousArea()
    {
        Assert.That(builder.TryBuild(CreateLayout(new RectInt(-50, -50, 3, 2)), DungeonBuildOptions.VisualOnly), Is.True);
        var map = builder.VoidFillTilemap;
        Assert.That(builder.TryBuild(CreateLayout(new RectInt(10, 10, 2, 2)), DungeonBuildOptions.VisualOnly), Is.True);
        Assert.That(builder.VoidFillTilemap, Is.SameAs(map));
        Assert.That(map.HasTile(new Vector3Int(-50, -50)), Is.False);
        Assert.That(map.HasTile(new Vector3Int(10, 10)), Is.True);
        Assert.That(root.GetComponentsInChildren<Tilemap>().Count(t => t.name == "GeneratedVoidFill"), Is.EqualTo(1));
        builder.ClearGeneratedContent();
        Assert.That(map.GetUsedTilesCount(), Is.Zero);
    }

    [Test]
    public void FillBounds_IncludeCorridorExtentsAndPadding()
    {
        var layout = CreateLayout(new RectInt(0, 0, 2, 2));
        var corridor = Activator.CreateInstance(typeof(DungeonSocketConnection), InternalInstance, null,
            new object[] { 0, 0, 0, 0, 5, new RectInt(-7, -2, 5, 2) }, null);
        typeof(DungeonLayoutResult).GetMethod("AddConnection", InternalInstance).Invoke(layout, new[] { corridor });
        object[] arguments = { layout, 8, default(RectInt) };
        bool success = (bool)typeof(DungeonRoomBuilder).GetMethod("TryCalculateVoidFillBounds", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, arguments);
        Assert.That(success, Is.True);
        Assert.That((RectInt)arguments[2], Is.EqualTo(new RectInt(-15, -10, 25, 20)));
    }

    private Tilemap CreateTilemap(string name, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        go.transform.SetParent(root.transform, false);
        go.GetComponent<TilemapRenderer>().sortingOrder = order;
        return go.GetComponent<Tilemap>();
    }

    private DungeonLayoutResult CreateLayout(RectInt bounds)
    {
        var template = ScriptableObject.CreateInstance<RoomTemplateSO>();
        assets.Add(template);
        template.EditorSetData(new RoomLayoutData { roomId = "VoidTest", size = bounds.size,
            localBounds = new RectInt(Vector2Int.zero, bounds.size), sockets = new List<RoomSocketData>() },
            new RoomBuildData { floorTiles = new List<RoomTileData> { new() { localCell = Vector2Int.zero, tile = fill } } });
        var layout = (DungeonLayoutResult)Activator.CreateInstance(typeof(DungeonLayoutResult), InternalInstance, null,
            new object[] { 1, 1 }, null);
        var room = Activator.CreateInstance(typeof(DungeonRoomPlacement), InternalInstance, null,
            new object[] { 0, template, bounds.position, bounds, 0, false, false }, null);
        typeof(DungeonLayoutResult).GetMethod("AddRoom", InternalInstance).Invoke(layout, new[] { room });
        return layout;
    }
}
#endif

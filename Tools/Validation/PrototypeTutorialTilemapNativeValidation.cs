// Run against the extracted authored grid in an isolated Unity project, never the user's open scene.
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class PrototypeTutorialTilemapNativeValidation
{
    public static void Run()
    {
        try
        {
            EditorSceneManager.OpenScene("Assets/TilemapStructureProbe.unity");
            var ground = GameObject.Find("Ground");
            var wall = GameObject.Find("Wall");
            Check(ground.layer == LayerMask.NameToLayer("Ground"), "Ground layer");
            Check(wall.layer == LayerMask.NameToLayer("Wall"), "Wall layer");
            Check(ground.GetComponent<Collider2D>() == null, "Ground must remain walkable");
            var tiles = wall.GetComponent<Tilemap>();
            var tileCollider = wall.GetComponent<TilemapCollider2D>();
            var composite = wall.GetComponent<CompositeCollider2D>();
            Check(wall.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Static, "Static wall body");
            Check(tileCollider.compositeOperation == Collider2D.CompositeOperation.Merge, "Tile collider merge");
            tileCollider.ProcessTilemapChanges();
            composite.GenerateGeometry();
            Physics2D.SyncTransforms();
            Check(composite.pathCount > 0 && composite.pointCount > 0, "Authored wall collider geometry missing");
            var originalBounds = composite.bounds;
            if (ground.GetComponentInParent<Grid>().cellSize == new Vector3(1, 1, 0))
            {
                Check(ground.transform.lossyScale == Vector3.one && wall.transform.lossyScale == Vector3.one, "Unit grid must not hide a scale adjustment");
                for (int y = 0; y < 20; y++)
                {
                    Check(!composite.OverlapPoint(new Vector2(-.5f, y + .5f)) &&
                          !composite.OverlapPoint(new Vector2(.5f, y + .5f)), "Dodge corridor is blocked");
                }
            }
            TileBase sample = null;
            foreach (var position in tiles.cellBounds.allPositionsWithin)
                if (tiles.HasTile(position)) { sample = tiles.GetTile(position); break; }
            Check(sample != null, "Wall tile assets failed to load");
            var probe = new Vector3Int(500, 500, 0);
            tiles.SetTile(probe, sample);
            tileCollider.ProcessTilemapChanges(); composite.GenerateGeometry(); Physics2D.SyncTransforms();
            Check(composite.bounds.max.x > originalBounds.max.x + 10f, "Painting a wall did not extend collision");
            tiles.SetTile(probe, null);
            tileCollider.ProcessTilemapChanges(); composite.GenerateGeometry(); Physics2D.SyncTransforms();
            Check(Vector3.Distance(composite.bounds.max, originalBounds.max) < .01f, "Erasing a wall left stale collision");
            Debug.Log($"TUTORIAL_TILEMAP_PASS: layers, walkable ground, static merged wall; {composite.pathCount} paths; paint/erase collision refresh.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}

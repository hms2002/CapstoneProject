// Isolated Unity Editor probe using current Gameplay/Core DLLs.
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class LightningRelicPlacementRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
    public static void Run()
    {
        try
        {
            var grid = new GameObject("Grid").AddComponent<Grid>();
            var floor = new GameObject("Floor").AddComponent<Tilemap>();
            floor.transform.SetParent(grid.transform);
            int ground = LayerMask.NameToLayer("Ground"); floor.gameObject.layer = ground >= 0 ? ground : 7;
            var wall = new GameObject("Wall").AddComponent<Tilemap>();
            wall.transform.SetParent(grid.transform); wall.gameObject.layer = floor.gameObject.layer;
            var tile = ScriptableObject.CreateInstance<Tile>();
            for (int x = -7; x <= 7; x++) for (int y = -7; y <= 7; y++) floor.SetTile(new Vector3Int(x, y, 0), tile);
            var state = new GameObject("Spear runtime").AddComponent<LightningSpearRuntimeState>();
            state.enabled = false;
            var data = ScriptableObject.CreateInstance<LightningSpearSkill2Data>();
            var loadout = ScriptableObject.CreateInstance<LightningSpearLoadout>();
            var method = typeof(LightningSpearRuntimeState).GetMethod("TryFindNearestMarkPosition", Private);
            var pending = (IList)typeof(LightningSpearRuntimeState).GetField("pendingMarkSpawns", Private).GetValue(state);
            var request = typeof(LightningSpearRuntimeState).GetNestedType("MarkSpawnRequest", BindingFlags.NonPublic);
            Vector2 preferred = new Vector2(3.1f, .2f);
            Vector2 Find()
            {
                var args = new object[] { loadout, data, Vector2.zero, null, preferred, null };
                Check((bool)method.Invoke(state, args), "Must find a valid target or adjacent tile");
                return (Vector2)args[5];
            }
            void Reserve(Vector2 at) => pending.Add(Activator.CreateInstance(request,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { at, null }, null));
            Check(Find() == preferred, "Valid enemy position must remain exact, without tile snapping");
            Reserve(preferred);
            for (int i = 1; i < 6; i++)
            {
                Vector2 next = Find();
                foreach (object saved in pending)
                    Check(Vector2.Distance(next, (Vector2)request.GetField("position").GetValue(saved)) >= 1.999f, "Reserved marks retain two-unit spacing");
                float nearest = float.PositiveInfinity;
                var validate = typeof(LightningSpearRuntimeState).GetMethod("ValidateMarkCandidate", Private);
                foreach (Vector3Int cell in floor.cellBounds.allPositionsWithin)
                {
                    Vector2 point = floor.GetCellCenterWorld(cell);
                    if ((bool)validate.Invoke(state, new object[] { loadout, data, Vector2.zero, null, point }))
                        nearest = Mathf.Min(nearest, (point - preferred).sqrMagnitude);
                }
                Check(Mathf.Abs((next - preferred).sqrMagnitude - nearest) < .0001f, "Each reserved fallback must be the nearest valid tile");
                Reserve(next);
            }
            pending.Clear();
            wall.SetTile(new Vector3Int(3, 0, 0), tile);
            Check(Find() == new Vector2(2.5f, .5f), "Wall under enemy selects closest valid neighbor");
            floor.ClearAllTiles();
            var empty = new object[] { loadout, data, Vector2.zero, null, preferred, null };
            Check(!(bool)method.Invoke(state, empty), "No ground must not invent a landing location");
            Debug.Log("LIGHTNING_RELIC_PLACEMENT_PASS: exact enemy point, nearest valid tile, six distinct reserved marks with spacing, wall exclusion and no-ground rejection.");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
}

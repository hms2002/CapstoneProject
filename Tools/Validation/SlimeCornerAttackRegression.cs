// Run in an isolated Unity Editor with freshly built project DLLs in Assets/Plugins.
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
public static class SlimeCornerAttackRegression
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static GameObject Wall(Vector2 position, Vector2 size)
    {
        var go = new GameObject("Wall"); go.layer = 8; go.transform.position = position;
        go.AddComponent<BoxCollider2D>().size = size; return go;
    }
    static void KnightCase(int sx, int sy, bool blocked)
    {
        var host = new GameObject("Knight fixture"); host.SetActive(false);
        var knight = host.AddComponent<Knight>(); Set(knight, "jumpBlockedLayers", (LayerMask)(1 << 8));
        var target = new GameObject("Target"); target.transform.position = new Vector2(3 * sx, 3 * sy);
        var wallX = Wall(new Vector2(3.6f * sx, 0), new Vector2(1, 20));
        var wallY = Wall(new Vector2(0, 3.6f * sy), new Vector2(20, 1));
        GameObject divider = blocked ? Wall(new Vector2(1.5f * sx, 0), new Vector2(.2f, 20)) : null;
        try
        {
            Physics2D.SyncTransforms();
            Check(!(bool)Call(knight, "IsJumpPathClear", Vector2.zero, (Vector2)target.transform.position, .22f, target), "Baseline center path should fail");
            object[] args = { target, Vector2.zero };
            bool ok = (bool)Call(knight, "TryResolveJumpImpact", args);
            Check(ok == !blocked, "Knight corner or intervening wall");
            if (ok) Check(Vector2.Distance((Vector2)args[1], target.transform.position) <= 1.6f, "Knight misses target");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(wallX); UnityEngine.Object.DestroyImmediate(wallY); if (divider != null) UnityEngine.Object.DestroyImmediate(divider); }
    }
    static void RookCase(int sx, int sy, bool blocked)
    {
        var host = new GameObject("Rook fixture"); host.SetActive(false);
        var rook = host.AddComponent<Rook>(); Set(rook, "cachedDashBlockerMask", 1 << 8);
        host.AddComponent<BoxCollider2D>().size = Vector2.one; host.SetActive(true);
        var target = new GameObject("Target"); target.transform.position = new Vector2(3 * sx, 3 * sy);
        target.AddComponent<BoxCollider2D>().size = Vector2.one * .18f;
        var wallX = Wall(new Vector2(3.75f * sx, 0), new Vector2(1, 20));
        var wallY = Wall(new Vector2(0, 3.75f * sy), new Vector2(20, 1));
        GameObject divider = blocked ? Wall(new Vector2(1.5f * sx, 0), new Vector2(.2f, 20)) : null;
        try
        {
            Physics2D.SyncTransforms();
            Vector2 direction = ((Vector2)target.transform.position).normalized;
            // Force center failure with a tighter corner whose body still fits.
            wallX.transform.position -= new Vector3(.15f * sx, 0);
            wallY.transform.position -= new Vector3(0, .15f * sy);
            Physics2D.SyncTransforms();
            object original = Call(rook, "ResolveChargeCast", direction, target);
            float distance = (float)original.GetType().GetField("Distance").GetValue(original);
            Check(distance < ((Vector2)target.transform.position).magnitude, "Baseline Rook center should fail");
            object[] args = { direction, target };
            object result = Call(rook, "ResolveChargeCastCached", args);
            float required = (float)typeof(Rook).GetField("cachedChargeRequiredDistance", Flags).GetValue(rook);
            bool reaches = (bool)result.GetType().GetMethod("ReachesTarget").Invoke(result, new object[] { required });
            Check(reaches == !blocked, "Rook corner or intervening wall");
            object[] again = { Vector2.up, target }; Call(rook, "ResolveChargeCastCached", again);
            Check((Vector2)again[0] == (Vector2)args[0], "Cached warning/execution direction mismatch");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(wallX); UnityEngine.Object.DestroyImmediate(wallY); if (divider != null) UnityEngine.Object.DestroyImmediate(divider); }
    }
    public static void Run()
    {
        try
        {
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2)
            { KnightCase(x,y,false); KnightCase(x,y,true); RookCase(x,y,false); RookCase(x,y,true); }
            Debug.Log("SLIME_CORNER_PASS: 16 native physics cases; four corners, blocked paths, selected direction cache."); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}

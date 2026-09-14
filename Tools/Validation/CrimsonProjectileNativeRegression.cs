// Isolated Unity Play Mode probe with current Core/Gameplay DLLs in Assets/Plugins.
// -batchmode -nographics -executeMethod CrimsonProjectileNativeRegression.Run
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

[InitializeOnLoad]
public static class CrimsonProjectileNativeRegression
{
    private const string Pending = "Capstone.CrimsonColliderProbe";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly List<UnityEngine.Object> spawned = new();
    static CrimsonProjectileNativeRegression() => EditorApplication.playModeStateChanged += OnPlayMode;
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true; }
    private static void Set(object o, string name, object v) => o.GetType().GetField(name, Private).SetValue(o, v);
    private static void Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Private).Invoke(o, args);
    private static bool Impact(CrimsonBoundaryProjectile2D p) => (bool)p.GetType().GetField("impactPlayed", Private).GetValue(p);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static GameObject Object(string name)
    {
        var go = new GameObject(name); spawned.Add(go); return go;
    }
    private static BoxCollider2D Obstacle(Vector2 position, Vector2 size, bool enemy = false)
    {
        var go = Object(enemy ? "Enemy hurtbox" : "Wall"); go.layer = enemy ? 9 : 8;
        go.transform.position = position;
        var col = go.AddComponent<BoxCollider2D>(); col.size = size; col.isTrigger = enemy;
        if (enemy) go.AddComponent<CombatHurtbox2D>();
        return col;
    }
    private static CrimsonBoundaryProjectile2D Projectile()
    {
        var owner = Object("Owner"); owner.AddComponent<AttributeSet>(); var system = owner.AddComponent<AbilitySystem>();
        var go = Object("Crimson projectile");
        go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        var damage = go.AddComponent<BoxCollider2D>(); damage.size = Vector2.one * .32f; damage.isTrigger = true;
        var wall = go.AddComponent<BoxCollider2D>(); wall.size = Vector2.one * .12f; wall.isTrigger = true;
        var p = go.AddComponent<CrimsonBoundaryProjectile2D>();
        Set(p, "wallCollider", wall); Set(p, "damageCollider", damage);
        var effect = ScriptableObject.CreateInstance<GE_Damage_Spec>(); spawned.Add(effect);
        p.Setup(new ProjectileAttackSpawnContext {
            ownerSystem = system, ignoreTarget = owner, lifetime = 10f, wallLayers = 1 << 8,
            damageLayers = 1 << 9, speed = 18f, direction = Vector2.right,
            hitPayload = new CombatHitPayload { sourceSystem = system, damageEffect = effect,
                hasResolvedElementBuildUps = true, elementBuildUps = Array.Empty<ElementDamageResult>() }
        }, 0, null);
        p.enabled = false; // Drive production TickAttack at a deterministic delta.
        Physics2D.SyncTransforms();
        return p;
    }
    private static void Cleanup()
    {
        foreach (var o in spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
        spawned.Clear(); Physics2D.SyncTransforms();
    }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        var runner = new GameObject("Probe runner").AddComponent<AbilitySystem>();
        runner.StartCoroutine(Exercise());
    }
    private static IEnumerator Exercise()
    {
        try
        {
            var side = Obstacle(new Vector2(1f, .23f), new Vector2(6f, .2f));
            var p = Projectile();
            Call(p, "OnTriggerEnter2D", side); Call(p, "OnTriggerStay2D", side);
            Call(p, "TickAttack", .1f);
            Check(!Impact(p) && Mathf.Abs(p.transform.position.x - 1.8f) < .001f, "Damage collider touching side wall must survive");
            Cleanup();

            Obstacle(new Vector2(1f, 0), new Vector2(.1f, 4f)); p = Projectile();
            Call(p, "TickAttack", .2f);
            Check(Impact(p) && p.transform.position.x > .85f && p.transform.position.x < .91f, "Small wall sweep must stop fast head-on shot");
            var stopped = p.transform.position; Call(p, "TickAttack", .2f);
            Check(p.transform.position == stopped, "Pending destruction must not process another impact"); Cleanup();

            Obstacle(new Vector2(1f, .20f), new Vector2(.1f, .1f), true); p = Projectile();
            Call(p, "TickAttack", .1f);
            Check(Impact(p) && p.transform.position.x < 1f, "Wide damage sweep must still hit off-axis enemy"); Cleanup();

            Obstacle(new Vector2(1f, 0), new Vector2(.1f, 4f));
            Obstacle(new Vector2(2f, 0), new Vector2(.1f, .1f), true); p = Projectile();
            Call(p, "TickAttack", .2f);
            Check(Impact(p) && p.transform.position.x < 1f, "Wall must win before enemy behind it"); Cleanup();

            Obstacle(new Vector2(2f, 0), new Vector2(.1f, 4f));
            Obstacle(new Vector2(1f, 0), new Vector2(.1f, .1f), true); p = Projectile();
            Call(p, "TickAttack", .2f);
            Check(Impact(p) && p.transform.position.x < 1f, "Enemy before wall must win"); Cleanup();

            Obstacle(Vector2.zero, Vector2.one); p = Projectile();
            Call(p, "TickAttack", 0f);
            Check(Impact(p), "Zero-distance initial wall overlap must be handled"); Cleanup();
            Debug.Log("CRIMSON_COLLIDER_REGRESSION_PASS: wall grazing, trigger bypass, fast wall hit, wide enemy hit, ordered wall/enemy hits, duplicate guard, initial overlap.");
        }
        catch (Exception e)
        {
            Debug.LogException(e); Cleanup(); EditorApplication.Exit(1); yield break;
        }
        yield return null;
        EditorApplication.Exit(0);
    }
}

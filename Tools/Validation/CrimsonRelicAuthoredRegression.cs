// Isolated Unity probe. Stage the real Q definition, data, prefab and dependency assets in
// Assets/RelicAuthored, plus a source-guid/type-name map in relic-script-map.txt.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

[InitializeOnLoad]
public static class CrimsonRelicAuthoredRegression
{
    private const string Pending = "Capstone.CrimsonRelicAuthored";
    private const string Base = "Assets/RelicAuthored/_Project/";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static CrimsonRelicAuthoredRegression() => EditorApplication.playModeStateChanged += Enter;
    public static void Run()
    {
        // DLL-backed probes have different MonoScript local IDs. Remap ONLY isolated copies;
        // all authored object links, prefab fields and component fileIDs remain the originals.
        var scripts = new Dictionary<string, string>();
        foreach (string dll in Directory.GetFiles("Assets/Plugins", "*.dll"))
            foreach (MonoScript script in AssetDatabase.LoadAllAssetsAtPath(dll).OfType<MonoScript>())
                if (script.GetClass() != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long id))
                    scripts[script.GetClass().Name] = $"{{fileID: {id}, guid: {guid}, type: 3}}";
        var map = File.ReadAllLines("relic-script-map.txt").Select(line => line.Split(' ')).ToArray();
        foreach (string file in Directory.GetFiles("Assets/RelicAuthored", "*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".asset") && !file.EndsWith(".prefab")) continue;
            string text = File.ReadAllText(file);
            foreach (var pair in map)
            {
                string source = $"{{fileID: 11500000, guid: {pair[0]}, type: 3}}";
                if (text.Contains(source))
                {
                    Check(scripts.ContainsKey(pair[1]), "Missing DLL MonoScript " + pair[1]);
                    text = text.Replace(source, scripts[pair[1]]);
                }
            }
            File.WriteAllText(file, text);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var def = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Base + "Data/Abilities/Definitions/AD_CrimsonBoundaryBigExplosion.asset");
        var data = def.sourceObject as CrimsonBoundaryWeaponData;
        Check(data != null && data.relicLavaBallPrefab != null, "Authored Q must reference the relic projectile");
        Check(data.damageEffect != null && def.logic is AbilityLogic_CrimsonBoundaryBigExplosion, "Real Q logic and damage asset resolve");
        Check(data.relicLavaBallPrefab.GetComponent<CrimsonBoundaryLavaProjectile2D>() != null, "Authored projectile component resolves");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static object Read(object obj, string field) => obj.GetType().GetField(field, Private).GetValue(obj);
    private static void Enter(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Authored probe").AddComponent<AbilitySystem>().StartCoroutine(Guard(Exercise()));
    }
    private static IEnumerator Guard(IEnumerator inner)
    {
        while (true)
        {
            object next;
            try { if (!inner.MoveNext()) break; next = inner.Current; }
            catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); yield break; }
            yield return next;
        }
        Debug.Log("CRIMSON_RELIC_AUTHORED_PASS: real Q asset activation, spawned visible prefab, independent damage/wall shapes, side-wall grazing, piercing, front-wall stop, no wall explosion damage, owner cleanup.");
        EditorApplication.Exit(0);
    }
    private static IEnumerator Exercise()
    {
        var def = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(Base + "Data/Abilities/Definitions/AD_CrimsonBoundaryBigExplosion.asset");
        var data = (CrimsonBoundaryWeaponData)def.sourceObject;
        var relic = AssetDatabase.LoadAssetAtPath<RelicDefinition>(Base + "Data/Items/Relics/Definitions/RD_CrimsonLavaBall.asset");
        var owner = new GameObject("Owner"); owner.AddComponent<AttributeSet>();
        var system = owner.AddComponent<AbilitySystem>();
        var inventory = owner.AddComponent<RelicInventory>();
        var weapon = new GameObject("Weapon"); weapon.transform.SetParent(owner.transform);
        var runtime = weapon.AddComponent<CrimsonBoundaryRuntimeState>();
        Check(inventory.TryAcquireOrUpgrade(relic), "Acquire actual Q relic");
        var spec = new AbilitySpec(def);
        Check(system.TryActivateAbility(spec), "Actual Q activation accepted");
        CrimsonBoundaryLavaProjectile2D ball = null;
        for (int i = 0; i < 10 && ball == null; i++)
        {
            ball = UnityEngine.Object.FindFirstObjectByType<CrimsonBoundaryLavaProjectile2D>();
            if (ball == null) yield return null;
        }
        Check(ball != null, "Q must spawn a projectile, not only consume cooldown");
        ball.enabled = false;
        ball.transform.position = Vector3.zero;
        ball.Setup(system, spec, data, runtime, Vector2.right);
        var wall = (BoxCollider2D)Read(ball, "wallCollider");
        var damage = (CircleCollider2D)Read(ball, "damageCollider");
        Check(wall.size == new Vector2(.12f, .12f) && Mathf.Approximately(damage.radius, 2.5f), "Authored collision sizes");
        Check(ball.GetComponentInChildren<SpriteRenderer>().sprite != null, "Authored visible sprite");
        Obstacle(new Vector2(2, .3f), new Vector2(10, .1f), 30);
        Obstacle(new Vector2(3, 0), new Vector2(.1f, 8), 30);
        var first = Obstacle(new Vector2(1, -.7f), Vector2.one * .2f, 31);
        var second = Obstacle(new Vector2(2, -.7f), Vector2.one * .2f, 31);
        var behind = Obstacle(new Vector2(4, -.7f), Vector2.one * .2f, 31);
        Physics2D.SyncTransforms();
        var tick = ball.GetType().GetMethod("Tick", Private);
        tick.Invoke(ball, new object[] { .1f });
        Check(!(bool)Read(ball, "finished") && Mathf.Abs(ball.transform.position.x - 1.35f) < .001f, "Large damage shape may overlap side wall without ending flight");
        tick.Invoke(ball, new object[] { .3f });
        var hits = (HashSet<GameObject>)Read(ball, "hitTargets");
        Check(hits.Count == 2 && hits.Contains(first) && hits.Contains(second) && !hits.Contains(behind), "Pierce off-axis targets, block targets behind front wall");
        Check((bool)Read(ball, "finished") && Mathf.Abs(ball.transform.position.x - 2.89f) < .02f, "Small center box stops at front wall");
        tick.Invoke(ball, new object[] { .3f });
        Check(hits.Count == 2, "Wall impact cannot add damage targets");
        weapon.SetActive(false);
        yield return null;
        Check(UnityEngine.Object.FindFirstObjectByType<CrimsonBoundaryLavaProjectile2D>() == null, "Weapon cleanup releases projectiles");
    }
    private static GameObject Obstacle(Vector2 position, Vector2 size, int layer)
    {
        var go = new GameObject("Collision probe"); go.layer = layer; go.transform.position = position;
        var collider = go.AddComponent<BoxCollider2D>(); collider.size = size; collider.isTrigger = layer == 31;
        if (layer == 31) go.AddComponent<CombatHurtbox2D>();
        return go;
    }
}

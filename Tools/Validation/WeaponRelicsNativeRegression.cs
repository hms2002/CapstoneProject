// Run in an isolated Unity project with the current Core/Gameplay DLLs.
// -batchmode -nographics -executeMethod WeaponRelicsNativeRegression.Run
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

[InitializeOnLoad]
public static class WeaponRelicsNativeRegression
{
    private const string Pending = "Capstone.WeaponRelicsProbe";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static WeaponRelicsNativeRegression() => EditorApplication.playModeStateChanged += Enter;
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true; }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static object Read(object obj, string field) => obj.GetType().GetField(field, Private).GetValue(obj);
    private static RelicDefinition Relic(string id, int maximum = 1)
    {
        var def = ScriptableObject.CreateInstance<RelicDefinition>();
        def.relicId = id; def.maxLevel = maximum; def.dropLevel = 1;
        return def;
    }
    private static void Enter(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Relic probe").AddComponent<AbilitySystem>().StartCoroutine(Exercise());
    }
    private static IEnumerator Exercise()
    {
        try
        {
            var actor = new GameObject("Owner");
            actor.AddComponent<AttributeSet>();
            var system = actor.AddComponent<AbilitySystem>();
            var inventory = actor.AddComponent<RelicInventory>();
            var magazine = Relic(WeaponExclusiveRelics.OddIronMagazine, 3);
            for (int level = 1; level <= 3; level++)
            {
                Check(inventory.TryAcquireOrUpgrade(magazine), "Acquire magazine");
                Check(inventory.TryGetRelicLevelById(magazine.relicId, out int actual) && actual == level, "Acquisition adds exactly one");
            }
            Check(!inventory.TryAcquireOrUpgrade(magazine), "Magazine cap is three");
            var ammo = new OddIronRuntimeData();
            Check(!WeaponExclusiveRelics.TryReload(actor, ammo), "Nonempty magazine cannot consume a relic level");
            for (int remaining = 2; remaining >= 0; remaining--)
            {
                ammo.ConsumeAllRounds();
                Check(WeaponExclusiveRelics.TryReload(actor, ammo) && ammo.CurrentAmmo == ammo.MaxAmmo, "Empty magazine refills completely");
                Check(ammo.TryConsumeOneRound() && ammo.CurrentAmmo == ammo.MaxAmmo - 1, "Same attack consumes the first reloaded round");
                bool present = inventory.TryGetRelicLevelById(magazine.relicId, out int actual);
                Check(remaining == 0 ? !present : present && actual == remaining, "Reload consumes exactly one and removes at zero");
            }
            ammo.ConsumeAllRounds();
            Check(!WeaponExclusiveRelics.TryReload(actor, ammo), "Removed magazine cannot refill");
            Check(inventory.TryAcquireOrUpgrade(magazine) && inventory.GetRelicLevelInSlot(0) == 1, "Reacquisition starts at one");

            var spear = Relic(WeaponExclusiveRelics.SpearThirdStrike);
            inventory.TryAcquireOrUpgrade(spear);
            Check(WeaponExclusiveRelics.Has(actor, spear.relicId) && !WeaponExclusiveRelics.Has(actor, WeaponExclusiveRelics.SpearTargetedRain), "Spear relics are independent");
            var crimson = Relic(WeaponExclusiveRelics.CrimsonKillShot);
            inventory.TryAcquireOrUpgrade(crimson);
            Check(!WeaponExclusiveRelics.Has(actor, WeaponExclusiveRelics.CrimsonLavaBall), "Crimson relics are independent");
            var dash = ScriptableObject.CreateInstance<AbilityLogic_ApprenticeHeroSwordDashStab>();
            Check(!dash.RequestsParallelExecution(system, null), "Base apprentice Q stays exclusive");
            inventory.TryAcquireOrUpgrade(Relic(WeaponExclusiveRelics.ApprenticeChargeLink));
            Check(dash.RequestsParallelExecution(system, null), "Only linked Q requests parallel execution");

            var chargeLogic = ScriptableObject.CreateInstance<AbilityLogic_ApprenticeHeroSwordChargeSpin>();
            var chargeDef = ScriptableObject.CreateInstance<AbilityDefinition>();
            var chargeSpec = new AbilitySpec(chargeDef);
            chargeSpec.SetInt(AbilityLogic_ApprenticeHeroSwordChargeSpin.HoldingChargeKey, 1);
            chargeSpec.SetFloat(AbilityLogic_ApprenticeHeroSwordChargeSpin.ChargeSecondsKey, 2f);
            chargeLogic.CleanupForSceneTransition(system, chargeSpec, null);
            Check(chargeSpec.GetInt(AbilityLogic_ApprenticeHeroSwordChargeSpin.HoldingChargeKey, -1) == 0 &&
                  chargeSpec.GetFloat(AbilityLogic_ApprenticeHeroSwordChargeSpin.ChargeSecondsKey, -1) == 0, "Charge snapshot clears on transition");

            var data = ScriptableObject.CreateInstance<CrimsonBoundaryWeaponData>();
            data.projectileSpeed = 18f; data.skill2Diameter = 1f;
            data.wallLayers = 1 << 8; data.damageLayers = 1 << 9;
            var first = Target(new Vector2(1, 0), false);
            var second = Target(new Vector2(2, 0), false);
            var behind = Target(new Vector2(4, 0), false);
            Target(new Vector2(3, 0), true);
            var ball = new GameObject("Piercing ball").AddComponent<CrimsonBoundaryLavaProjectile2D>();
            ball.gameObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var wallShape = ball.gameObject.AddComponent<BoxCollider2D>();
            wallShape.size = Vector2.one * .12f; wallShape.isTrigger = true;
            var damageShape = ball.gameObject.AddComponent<CircleCollider2D>();
            damageShape.radius = .5f; damageShape.isTrigger = true;
            ball.GetType().GetField("wallCollider", Private).SetValue(ball, wallShape);
            ball.GetType().GetField("damageCollider", Private).SetValue(ball, damageShape);
            ball.Setup(system, new AbilitySpec(chargeDef), data, null, Vector2.right);
            ball.enabled = false;
            Physics2D.SyncTransforms();
            var tick = ball.GetType().GetMethod("Tick", Private);
            tick.Invoke(ball, new object[] { .1f });
            Check(Mathf.Abs(ball.transform.position.x - 1.35f) < .001f, "Projectile moves at 75 percent speed");
            tick.Invoke(ball, new object[] { .5f });
            var hit = (HashSet<GameObject>)Read(ball, "hitTargets");
            Check(hit.Contains(first) && hit.Contains(second) && !hit.Contains(behind) && hit.Count == 2, "Pierces two targets once; wall protects target behind it");
            Check((bool)Read(ball, "finished") && ball.transform.position.x < 3f, "First wall stops the projectile");
            var stopped = ball.transform.position;
            tick.Invoke(ball, new object[] { .5f });
            Check(ball.transform.position == stopped && hit.Count == 2, "No repeated hit or movement pending destruction");
            Debug.Log("WEAPON_RELICS_REGRESSION_PASS: independent gates, magazine acquisition/cap/reload/removal/reacquisition, conditional parallel policy, charge cleanup, 75% lava speed, piercing, wall occlusion and duplicate guard.");
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); yield break; }
        yield return null;
        EditorApplication.Exit(0);
    }
    private static GameObject Target(Vector2 at, bool wall)
    {
        var go = new GameObject(wall ? "Wall" : "Hurtbox"); go.layer = wall ? 8 : 9; go.transform.position = at;
        var col = go.AddComponent<BoxCollider2D>(); col.size = wall ? new Vector2(.1f, 4f) : Vector2.one * .2f;
        col.isTrigger = !wall;
        if (!wall) go.AddComponent<CombatHurtbox2D>();
        return go;
    }
}

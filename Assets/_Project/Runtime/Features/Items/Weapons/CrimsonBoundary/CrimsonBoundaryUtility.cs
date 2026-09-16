using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

// Responsibility: resolve fire-based combat values, targets and shared Crimson Boundary presentation.
public static class CrimsonBoundaryUtility
{
    public static void SpawnRelicKillShot(AbilitySystem system, AbilitySpec spec, CrimsonBoundaryWeaponData data,
        CrimsonBoundaryRuntimeState runtime, Vector3 position, GameObject killedTarget)
    {
        var enemies = WeaponExclusiveRelics.FindEnemies(position, 40f, killedTarget);
        if (enemies.Count == 0) return;
        Enemy target = enemies[0];
        float damage = CalculateDirectDamage(system, 1f, out bool critical);
        var visual = CrimsonBoundaryVisual2D.Spawn(data.projectilePrefab, position, Quaternion.identity, runtime);
        if (visual == null) return;
        var projectile = visual.GetComponent<CrimsonBoundaryProjectile2D>();
        projectile.Setup(new ProjectileAttackSpawnContext
        {
            ownerSystem = system, sourceSpec = spec, causer = visual.gameObject,
            ignoreTarget = system.gameObject, lifetime = data.projectileLifetime,
            wallLayers = data.wallLayers, damageLayers = data.damageLayers,
            direction = target.transform.position - position, speed = data.projectileSpeed,
            hitPayload = new CombatHitPayload
            {
                sourceSystem = system, sourceSpec = spec, damageEffect = data.damageEffect,
                finalHpDamage = damage, causer = visual.gameObject, isCriticalHit = critical,
                elementBuildUps = Array.Empty<ElementDamageResult>(), hasResolvedElementBuildUps = true
            }
        }, data.attackBurnStacks, data.damageEffect, data.projectileHitPrefab, data.burnTickPrefab, runtime, data.burnSustainPrefab);
        projectile.SetHomingTarget(target.transform);
    }
    private static readonly Collider2D[] HitBuffer = new Collider2D[128];
    private static readonly ElementDamageResult[] NoElementBuildUp = Array.Empty<ElementDamageResult>();

    public static float ReadFire(AbilitySystem system)
    {
        IStatProvider provider = AbilityStatProviderFactory.Create(system);
        return provider != null ? Mathf.Max(0f, provider.Get(StatId.FireFinal)) : 0f;
    }

    public static float CalculateDirectDamage(AbilitySystem system, float multiplier, out bool critical,
        ScaledStatFormula fireFormula = null)
    {
        IStatProvider provider = AbilityStatProviderFactory.Create(system);
        DamageResult result = DamageFormulaUtil.PostProcess(provider, ResolveFire(system, provider, fireFormula) * multiplier, 0f);
        critical = result.isCrit;
        return Mathf.Round(result.hpDamage);
    }

    public static float CalculateBurnConsumptionDamage(AbilitySystem system, int consumedStacks, float fireMultiplier,
        ScaledStatFormula fireFormula = null)
    {
        if (consumedStacks <= 0) return 0f;
        IStatProvider provider = AbilityStatProviderFactory.Create(system);
        float finalMultiplier = provider != null ? Mathf.Max(0f, provider.Get(StatId.FinalMul)) : 1f;
        return Mathf.Round(ResolveFire(system, provider, fireFormula) * Mathf.Max(0f, fireMultiplier) * consumedStacks * finalMultiplier);
    }

    private static float ResolveFire(AbilitySystem system, IStatProvider provider, ScaledStatFormula formula)
    {
        if (provider == null) return 0f;
        float fire = Mathf.Max(0f, provider.Get(StatId.FireFinal));
        return formula != null ? Mathf.Max(0f, formula.Evaluate(system.AttributeSet, provider, fire)) : fire;
    }

    public static void ApplyDamage(AbilitySystem system, AbilitySpec spec, GameplayEffect effect, GameObject target, float damage, bool critical, GameObject causer)
    {
        if (system == null || effect == null || target == null || damage <= 0f) return;
        CombatDamageAction.ApplyDamageAndEmitHit(
            system: system,
            spec: spec,
            damageEffect: effect,
            knockbackEffect: null,
            target: target,
            finalHpDamage: damage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            hitConfirmedTag: null,
            hitWorldPosition: target.transform.position,
            causer: causer,
            isCriticalHit: critical,
            elementBuildUps: NoElementBuildUp,
            hasResolvedElementBuildUps: true);
    }

    public static List<GameObject> CollectTargets(Vector2 center, float diameter, LayerMask damageLayers)
    {
        var results = new List<GameObject>();
        var seen = new HashSet<GameObject>();
        int count = Physics2D.OverlapCircleNonAlloc(center, diameter * 0.5f, HitBuffer, damageLayers);
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = HitBuffer[i];
            GameObject target = CombatTargetResolver2D.ResolveDamageTarget(collider);
            if (target == null || !seen.Add(target)) continue;
            if (TopDownEllipseHitUtility2D.ContainsCollider(collider, center, diameter))
                results.Add(target);
        }
        return results;
    }

    public static List<BurnStatus2D> CollectBurnTargetsInViewport()
    {
        var results = new List<BurnStatus2D>();
        Camera camera = Camera.main;
        foreach (BurnStatus2D status in BurnStatus2D.ActiveStatuses)
        {
            if (status == null || !status.isActiveAndEnabled || status.CurrentStacks <= 0) continue;
            if (camera == null)
            {
                results.Add(status);
                continue;
            }

            Vector3 viewport = camera.WorldToViewportPoint(status.transform.position);
            if (viewport.z >= 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
                results.Add(status);
        }
        return results;
    }

    public static bool HasBurnTargetInViewport()
    {
        Camera camera = Camera.main;
        foreach (BurnStatus2D status in BurnStatus2D.ActiveStatuses)
        {
            if (status == null || !status.isActiveAndEnabled || status.CurrentStacks <= 0) continue;
            if (camera == null) return true;
            Vector3 viewport = camera.WorldToViewportPoint(status.transform.position);
            if (viewport.z >= 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
                return true;
        }
        return false;
    }

    public static Vector2 ResolveCursor(AbilitySystem system)
    {
        if (system != null)
        {
            ICursorWorldSource2D source = system.GetComponent<ICursorWorldSource2D>();
            if (source != null) return source.CursorWorld;
        }
        return system != null ? (Vector2)system.transform.position + AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right) * 3f : Vector2.zero;
    }

    public static CrimsonBoundaryRuntimeState ResolveRuntimeState(AbilitySystem system)
    {
        return system != null ? system.GetComponentInChildren<CrimsonBoundaryRuntimeState>() : null;
    }

}

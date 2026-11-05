using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>처치 위치의 표적 탐색, 생성 시점 피해와 발사된 혼불의 수명을 소유한다.</summary>
[DisallowMultipleComponent]
public sealed class ShadowWispController : MonoBehaviour
{
    private readonly List<Collider2D> candidates = new(64);
    private readonly HashSet<GameObject> seenTargets = new();
    private readonly HashSet<GameObject> processedDeaths = new();
    private readonly List<RaycastHit2D> wallHits = new(16);
    private readonly List<ChloeLanternProjectile2D> projectiles = new();
    private UnityEngine.Object activeToken;
    private AbilitySystem system;
    private RelicLogic_ShadowWisp settings;

    public void Attach(UnityEngine.Object token, AbilitySystem ownerSystem, RelicLogic_ShadowWisp data)
    {
        ClearObjects();
        activeToken = token;
        system = ownerSystem;
        settings = data;
    }

    public void Detach(UnityEngine.Object token)
    {
        if (activeToken != null && token != null && token != activeToken) return;
        activeToken = null;
        ClearObjects();
        system = null;
        settings = null;
    }

    private bool CanSpawn() => activeToken != null && system != null && settings != null &&
        isActiveAndEnabled && !CombatTargetDeathUtility.IsPlayerDeathSequenceRunning(transform);

    public void OnOwnerKill(UnityEngine.Object token, AbilityEventData data)
    {
        if (token != activeToken || !CanSpawn() || data.Target == null ||
            data.Target.GetComponent<Enemy>() == null) return;
        if (!processedDeaths.Add(data.Target)) return;

        // Common kill events still reach other relics; suppress only this controller's own shots.
        if (data.Causer is GameObject causer &&
            causer.TryGetComponent(out ChloeLanternProjectile2D source) && projectiles.Contains(source)) return;
        Vector2 origin = CommonMonsterCombatUtility.ResolveAimPoint(data.Target);
        Enemy target = FindTarget(origin, data.Target);
        if (target != null) Fire(origin, target);
    }

    private Enemy FindTarget(Vector2 origin, GameObject killedEnemy)
    {
        Camera camera = Camera.main;
        if (camera == null) return null;
        float depth = camera.WorldToViewportPoint(transform.position).z;
        Vector2 min = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector2 max = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(settings.targetLayers);
        Physics2D.OverlapArea(min, max, filter, candidates);
        seenTargets.Clear();
        Enemy nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Collider2D candidate in candidates)
        {
            if (candidate == null || candidate.GetComponent<CombatHurtbox2D>() == null) continue;
            GameObject root = CombatTargetResolver2D.ResolveDamageTarget(candidate);
            if (root == null || root == killedEnemy || root == gameObject || !seenTargets.Add(root)) continue;
            Enemy enemy = root.GetComponent<Enemy>();
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDead) continue;
            Vector2 aim = CommonMonsterCombatUtility.ResolveAimPoint(root);
            Vector3 viewport = camera.WorldToViewportPoint(aim);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
            float distance = (aim - origin).sqrMagnitude;
            if (distance >= nearestDistance || !HasLineOfSight(origin, aim)) continue;
            nearest = enemy;
            nearestDistance = distance;
        }
        return nearest;
    }

    private bool HasLineOfSight(Vector2 origin, Vector2 destination)
    {
        Vector2 delta = destination - origin;
        if (delta.sqrMagnitude <= 0.0001f) return true;
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(settings.wallLayers);
        return Physics2D.Raycast(origin, delta.normalized, filter, wallHits, delta.magnitude) == 0;
    }

    private void Fire(Vector2 origin, Enemy target)
    {
        IStatProvider stats = AbilityStatProviderFactory.Create(system);
        if (stats == null) return;
        float damage = Mathf.Max(0f, stats.Get(StatId.AttackFinal));
        var projectile = Instantiate(settings.projectilePrefab, origin, Quaternion.identity);
        projectiles.Add(projectile);
        // Preserve the actual shot as the cause so its lethal hit cannot create another wisp.
        GameObject causer = projectile.gameObject;
        projectile.Launch(new ProjectileAttackSpawnContext
        {
            ownerSystem = system,
            causer = causer,
            ignoreTarget = gameObject,
            lifetime = settings.projectileLifetime,
            wallLayers = settings.wallLayers,
            damageLayers = settings.targetLayers,
            direction = CommonMonsterCombatUtility.ResolveAimPoint(target.gameObject) - origin,
            speed = CommonMonsterCombatUtility.ResolvePlayerBaseSpeed(transform) * settings.projectileSpeedMultiplier,
            hitPayload = new CombatHitPayload
            {
                sourceSystem = system,
                damageEffect = settings.damageEffect,
                causer = causer,
                finalHpDamage = damage,
                isCriticalHit = false,
                elementBuildUps = Array.Empty<ElementDamageResult>(),
                elementHpDamages = Array.Empty<ElementDamageResult>(),
                hasResolvedElementBuildUps = true
            }
        }, target, settings.homingTurnRate);
    }

    private void Update()
    {
        if (!CanSpawn())
        {
            ClearObjects();
            return;
        }
        for (int i = projectiles.Count - 1; i >= 0; i--)
            if (projectiles[i] == null) projectiles.RemoveAt(i);
        processedDeaths.RemoveWhere(target => target == null);
    }

    private void ClearObjects()
    {
        foreach (ChloeLanternProjectile2D projectile in projectiles)
            if (projectile != null) { projectile.gameObject.SetActive(false); Destroy(projectile.gameObject); }
        projectiles.Clear();
        candidates.Clear();
        seenTargets.Clear();
        processedDeaths.Clear();
    }

    private void OnDisable() => ClearObjects();
    private void OnDestroy() => ClearObjects();
}

using System;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>소유자에 붙어 공전, 화면 내 표적 선택, 발사와 모든 생성물의 회수를 담당한다.</summary>
[DisallowMultipleComponent]
public sealed class ChloeLanternOrbitController : MonoBehaviour
{
    private readonly List<Collider2D> candidates = new(64);
    private readonly HashSet<GameObject> seenTargets = new();
    private readonly List<RaycastHit2D> wallHits = new(16);
    private readonly List<ChloeLanternProjectile2D> projectiles = new();
    private UnityEngine.Object activeToken;
    private AbilitySystem system;
    private RelicLogic_ChloeLantern settings;
    private GameObject summon;
    private float elapsed;
    private float angle;

    internal static bool IsDialogueBlockingCombat(AbilitySystem ownerSystem)
    {
        // The encounter holds Talking through the camera lead-in and return, outside actual dialogue playback.
        return DialoguePlayback.IsPlaying ||
            (ownerSystem != null && ownerSystem.TryGetComponent(out PlayerInteractor2D interactor) &&
             interactor.CurrentState == InteractState.Talking);
    }

    public void Attach(UnityEngine.Object token, AbilitySystem ownerSystem, RelicLogic_ChloeLantern data)
    {
        ClearObjects();
        activeToken = token;
        system = ownerSystem;
        settings = data;
        angle = 0f;
    }

    public void Detach(UnityEngine.Object token)
    {
        if (activeToken != null && token != null && token != activeToken) return;
        activeToken = null;
        ClearObjects();
        settings = null;
        system = null;
    }

    private void Update()
    {
        if (activeToken == null || system == null || settings == null ||
            CombatTargetDeathUtility.IsPlayerDeathSequenceRunning(transform))
        {
            ClearObjects();
            return;
        }

        if (summon == null)
            summon = Instantiate(settings.summonPrefab, transform);

        angle = Mathf.Repeat(angle + settings.angularSpeedDegPerSec * Time.deltaTime, 360f);
        Vector3 center = transform.TransformPoint(settings.orbitCenterLocalOffset);
        float radians = angle * Mathf.Deg2Rad;
        summon.transform.position = center + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians)) * settings.orbitRadius;
        for (int i = projectiles.Count - 1; i >= 0; i--)
            if (projectiles[i] == null) projectiles.RemoveAt(i);

        if (IsDialogueBlockingCombat(system))
        {
            ClearProjectiles();
            elapsed = 0f;
            return;
        }

        elapsed = Mathf.Min(elapsed + Time.deltaTime, settings.fireInterval);
        if (Time.deltaTime <= 0f || elapsed < settings.fireInterval) return;

        elapsed = 0f;
        Enemy target = FindTarget(center);
        if (target == null) return;
        Fire(target);
    }

    private Enemy FindTarget(Vector2 playerCenter)
    {
        Camera camera = Camera.main;
        if (camera == null || !HasLineOfSight(playerCenter, summon.transform.position)) return null;
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
            if (root == null || root == gameObject || !seenTargets.Add(root)) continue;
            Enemy enemy = root.GetComponent<Enemy>();
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDead) continue;
            Vector2 aim = CommonMonsterCombatUtility.ResolveAimPoint(root);
            Vector3 viewport = camera.WorldToViewportPoint(aim);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) continue;
            float distance = ((Vector2)root.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (distance >= nearestDistance || !HasLineOfSight(playerCenter, aim) ||
                !HasLineOfSight(summon.transform.position, aim)) continue;
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

    private void Fire(Enemy target)
    {
        IStatProvider stats = AbilityStatProviderFactory.Create(system);
        if (stats == null) return;
        float damage = Mathf.Max(0f, stats.Get(StatId.AttackFinal));
        Vector2 origin = summon.transform.position;
        Vector2 direction = CommonMonsterCombatUtility.ResolveAimPoint(target.gameObject) - origin;
        var projectile = Instantiate(settings.projectilePrefab, origin, Quaternion.identity);
        projectiles.Add(projectile);
        projectile.Launch(new ProjectileAttackSpawnContext
        {
            ownerSystem = system,
            causer = gameObject,
            ignoreTarget = gameObject,
            lifetime = settings.projectileLifetime,
            wallLayers = settings.wallLayers,
            damageLayers = settings.targetLayers,
            direction = direction,
            speed = CommonMonsterCombatUtility.ResolvePlayerBaseSpeed(transform) * settings.projectileSpeedMultiplier,
            hitPayload = new CombatHitPayload
            {
                sourceSystem = system,
                damageEffect = settings.damageEffect,
                causer = gameObject,
                finalHpDamage = damage,
                isCriticalHit = false,
                elementBuildUps = Array.Empty<ElementDamageResult>(),
                elementHpDamages = Array.Empty<ElementDamageResult>(),
                hasResolvedElementBuildUps = true
            }
        }, target, settings.homingTurnRate);
        SoundPlaybackUtility.Play(RelicLogic_ChloeLantern.ShotSound,
            causer: gameObject, position: origin, sourceObject: settings);
    }

    private void ClearObjects()
    {
        if (summon != null)
        {
            summon.SetActive(false);
            Destroy(summon);
            summon = null;
        }
        ClearProjectiles();
        candidates.Clear();
        seenTargets.Clear();
        elapsed = 0f;
    }

    private void ClearProjectiles()
    {
        foreach (ChloeLanternProjectile2D projectile in projectiles)
            if (projectile != null) { projectile.gameObject.SetActive(false); Destroy(projectile.gameObject); }
        projectiles.Clear();
    }

    // Keep the attachment on a temporary disable; a later enable recreates only the authored visuals.
    private void OnDisable() => ClearObjects();
    private void OnDestroy() => ClearObjects();
}

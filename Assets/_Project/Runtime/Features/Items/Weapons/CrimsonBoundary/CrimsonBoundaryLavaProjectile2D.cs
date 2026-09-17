using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>Relic Q: swept piercing damage while travelling, visual-only impact at the first wall.</summary>
public sealed class CrimsonBoundaryLavaProjectile2D : MonoBehaviour, IAttackCollisionSource2D
{
    [SerializeField] private BoxCollider2D wallCollider;
    [SerializeField] private CircleCollider2D damageCollider;
    private readonly List<RaycastHit2D> wallHits = new(12);
    private readonly List<RaycastHit2D> damageHits = new(12);
    private readonly HashSet<GameObject> hitTargets = new();
    private AbilitySystem owner;
    private AbilitySpec spec;
    private CrimsonBoundaryWeaponData data;
    private CrimsonBoundaryRuntimeState visualOwner;
    private Vector2 direction;
    private float age;
    private bool finished;

    public void Setup(AbilitySystem system, AbilitySpec source, CrimsonBoundaryWeaponData weaponData,
        CrimsonBoundaryRuntimeState runtime, Vector2 aim)
    {
        owner = system; spec = source; data = weaponData; visualOwner = runtime;
        if (wallCollider == null || damageCollider == null)
        {
            Debug.LogError("Relic LavaBall requires separate wall and damage colliders.", this);
            finished = true;
            Destroy(gameObject);
            return;
        }
        direction = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private void Update() => Tick(TimeScalePausePlayback.PresentationDeltaTime);

    private void Tick(float dt)
    {
        if (finished) return;
        if (data == null || owner == null) { Destroy(gameObject); return; }
        if (dt <= 0f) return;
        age += dt;
        if (age > 30f) { Destroy(gameObject); return; }
        Vector2 origin = transform.position;
        float travel = Mathf.Max(0f, data.projectileSpeed * 0.75f * dt);
        float wallDistance = travel;
        bool hitWall = false;
        Sweep(wallCollider, data.wallLayers, wallHits, travel);
        foreach (var hit in wallHits)
        {
            if (IsOwnCollider(hit.collider) || hit.collider.isTrigger || hit.collider.transform.IsChildOf(owner.transform)) continue;
            if (hit.distance > wallDistance) continue;
            wallDistance = hit.distance; hitWall = true;
        }
        Sweep(damageCollider, data.damageLayers, damageHits, wallDistance);
        damageHits.Sort((a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in damageHits)
        {
            if (IsOwnCollider(hit.collider)) continue;
            GameObject target = CombatTargetResolver2D.ResolveDamageTarget(hit.collider);
            if (target == null || target == owner.gameObject || hitTargets.Contains(target)) continue;
            if (target.TryGetComponent(out Enemy enemy) && enemy.IsDead) continue;
            // A large damage volume must not reach through a wall beside the flight path.
            bool blocked = false;
            foreach (var wall in Physics2D.LinecastAll(origin, target.transform.position, data.wallLayers))
                if (wall.collider != null && !wall.collider.isTrigger) { blocked = true; break; }
            if (blocked) continue;
            hitTargets.Add(target);
            float damage = CrimsonBoundaryUtility.CalculateDirectDamage(owner, data.skill2BaseMultiplier, out bool critical, data.skillFireFormula);
            var burn = target.GetComponent<BurnStatus2D>();
            int consumed = burn != null ? burn.ConsumeAll() : 0;
            damage += CrimsonBoundaryUtility.CalculateBurnConsumptionDamage(owner, consumed, data.skill2BurnConsumptionMultiplier, data.skillFireFormula);
            CrimsonBoundaryUtility.ApplyDamage(owner, spec, data.damageEffect, target, damage, critical, gameObject);
        }
        transform.position = origin + direction * wallDistance;
        if (!hitWall) return;
        finished = true;
        CrimsonBoundaryVisual2D.Spawn(data.meteorHitPrefab, transform.position, Quaternion.identity, visualOwner);
        AbilityAudioRouter.PlayOneShotAtPosition(data.lavaBallHitSound, owner, spec, transform.position, data);
        data.lavaBallHitShake.TryPlay(gameObject, direction, debugReason: "Crimson LavaBall wall impact");
        Destroy(gameObject);
    }

    // Like the basic projectile, explicit sweeps own hit ordering; trigger overlap never destroys the ball.
    private void Sweep(Collider2D collider, LayerMask layers, List<RaycastHit2D> hits, float distance)
    {
        hits.Clear();
        if (layers.value == 0) return;
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = layers, useTriggers = true };
        collider.Cast(collider.transform.position, collider.transform.eulerAngles.z,
            direction, filter, hits, distance, true);
    }

    private bool IsOwnCollider(Collider2D collider) => collider == null || collider.transform.IsChildOf(transform);
}

using System.Collections;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "AL_CrimsonBoundaryAttack", menuName = "GAS/Weapon/Crimson Boundary/Attack Logic")]
public sealed class AbilityLogic_CrimsonBoundaryAttack : AbilityLogic
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        CrimsonBoundaryWeaponData data = spec?.Definition?.sourceObject as CrimsonBoundaryWeaponData;
        if (system == null || data == null || data.damageEffect == null)
            yield break;

        CrimsonBoundaryRuntimeState runtime = CrimsonBoundaryUtility.ResolveRuntimeState(system);
        if (runtime == null || !runtime.isActiveAndEnabled) yield break;

        runtime.BeginSwing(AbilityAttackSpeedResolver.ResolveFinalAttackSpeed(system));
        AbilityAudioRouter.PlayOneShot(data.swingSound, system, spec, sourceObjectOverride: data);
        bool released = false;
        try
        {
            while (runtime != null && runtime.isActiveAndEnabled && runtime.SwingTime < 0.12f)
            {
                if (spec.Token != null && spec.Token.IsCancelled) yield break;
                yield return null;
            }
            if (runtime == null || !runtime.isActiveAndEnabled ||
                (spec.Token != null && spec.Token.IsCancelled)) yield break;
            released = true;
        }
        finally
        {
            if (!released && runtime != null) runtime.ResetSwing();
        }

        Vector2 direction = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
        runtime.MarkProjectileReleased();
        Vector3 position = PlayerAttackOrigin.Resolve(system, direction, data.wallLayers);
        bool critical;
        float damage = CrimsonBoundaryUtility.CalculateDirectDamage(system, 1f, out critical);

        var payload = new CombatHitPayload
        {
            sourceSystem = system,
            sourceSpec = spec,
            damageEffect = data.damageEffect,
            finalHpDamage = damage,
            causer = system.gameObject,
            isCriticalHit = critical,
            elementBuildUps = System.Array.Empty<ElementDamageResult>(),
            hasResolvedElementBuildUps = true
        };

        var visual = CrimsonBoundaryVisual2D.Spawn(data.projectilePrefab, position, Quaternion.identity, runtime);
        if (visual == null) yield break;
        AbilityAudioRouter.PlayOneShotAtPosition(data.projectileLaunchSound, system, spec, position, data);
        GameObject projectileObject = visual.gameObject;
        var collider = projectileObject.GetComponent<BoxCollider2D>();
        collider.isTrigger = true;
        var body = projectileObject.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        var projectile = projectileObject.GetComponent<CrimsonBoundaryProjectile2D>();
        projectile.Setup(new ProjectileAttackSpawnContext
        {
            ownerSystem = system,
            sourceSpec = spec,
            causer = system.gameObject,
            ignoreTarget = system.gameObject,
            lifetime = data.projectileLifetime,
            wallLayers = data.wallLayers,
            damageLayers = data.damageLayers,
            hitPayload = payload,
            direction = direction,
            speed = data.projectileSpeed
        }, data.attackBurnStacks, data.damageEffect, data.projectileHitPrefab, data.burnTickPrefab, runtime, data.burnSustainPrefab);


    }
}

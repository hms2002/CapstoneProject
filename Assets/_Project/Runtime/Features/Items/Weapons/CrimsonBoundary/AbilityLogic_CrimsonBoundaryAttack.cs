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

        Vector2 direction = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
        Vector3 position = system.transform.position + (Vector3)(direction * 0.75f);
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

        GameObject projectileObject = CrimsonBoundaryUtility.CreateSquare(
            "CrimsonBoundary_Fireball",
            position,
            new Vector2(0.32f, 0.32f),
            new Color(1f, 0.2f, 0.01f, 1f),
            "Projectile",
            5);
        var collider = projectileObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        var body = projectileObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        var projectile = projectileObject.AddComponent<CrimsonBoundaryProjectile2D>();
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
        }, data.attackBurnStacks, data.damageEffect);

        CrimsonBoundaryUtility.ResolveRuntimeState(system)?.Register(projectileObject);
    }
}

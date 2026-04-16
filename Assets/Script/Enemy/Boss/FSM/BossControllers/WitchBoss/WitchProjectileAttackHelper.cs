using UnityEngine;
using UnityGAS;

public static class WitchProjectileAttackHelper
{
    // 이 클래스의 책임:
    // 마녀 보스 패턴이 재사용하는 LightBead 투사체 발사와 피격 정보 생성을 공통화한다.

    private const int PlayerLayer = 3;
    private const int WallLayer = 30;
    public const float DefaultProjectileLifetimeSeconds = 5f;

    /// <summary>주어진 위치에서 LightBead 부채꼴 탄막을 발사합니다.</summary>
    public static bool SpawnLightBeadBurst(
        AbilitySystem sourceSystem,
        GameObject causer,
        GameObject ignoreTarget,
        GameObject lightBeadPrefab,
        GE_Damage_Spec damageEffect,
        float damage,
        float projectileSpeed,
        Vector3 origin,
        Vector2 forward,
        int projectileCount,
        float spreadAngleDegrees,
        GameObject target)
    {
        if (sourceSystem == null || causer == null || lightBeadPrefab == null || damageEffect == null)
            return false;

        int count = Mathf.Max(1, projectileCount);
        Vector2 direction = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector2.right;
        CombatHitPayload payload = BuildPayload(sourceSystem, causer, damageEffect, damage);
        LayerMask damageLayers = 1 << PlayerLayer;

        if (count == 1)
        {
            SpawnSingleProjectile(lightBeadPrefab, payload, sourceSystem, causer, ignoreTarget, origin, direction, projectileSpeed, damageLayers);
            return true;
        }

        float startAngle = -spreadAngleDegrees * 0.5f;
        float angleStep = count > 1 ? spreadAngleDegrees / (count - 1) : 0f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + (angleStep * i);
            Vector2 shotDirection = Quaternion.Euler(0f, 0f, angle) * direction;
            SpawnSingleProjectile(lightBeadPrefab, payload, sourceSystem, causer, ignoreTarget, origin, shotDirection, projectileSpeed, damageLayers);
        }

        return true;
    }

    private static void SpawnSingleProjectile(
        GameObject lightBeadPrefab,
        CombatHitPayload payload,
        AbilitySystem sourceSystem,
        GameObject causer,
        GameObject ignoreTarget,
        Vector3 origin,
        Vector2 direction,
        float projectileSpeed,
        LayerMask damageLayers)
    {
        GameObject projectileObject = Object.Instantiate(lightBeadPrefab, origin, Quaternion.identity);
        LightBeadProjectile2D projectile = projectileObject.GetComponent<LightBeadProjectile2D>();

        if (projectile == null)
        {
            Object.Destroy(projectileObject);
            return;
        }

        ProjectileAttackSpawnContext context = new ProjectileAttackSpawnContext
        {
            ownerSystem = sourceSystem,
            sourceSpec = null,
            causer = causer,
            ignoreTarget = ignoreTarget != null ? ignoreTarget : causer,
            lifetime = DefaultProjectileLifetimeSeconds,
            wallLayers = 1 << WallLayer,
            damageLayers = damageLayers,
            hitPayload = payload,
            direction = direction,
            speed = Mathf.Max(0f, projectileSpeed)
        };

        projectile.Setup(context);

        Witch witchOwner = causer.GetComponent<Witch>();
        if (witchOwner != null)
        {
            projectile.BindRampageOwner(witchOwner);
            witchOwner.RegisterRampageProjectile(projectile);
        }
    }

    private static CombatHitPayload BuildPayload(
        AbilitySystem sourceSystem,
        GameObject causer,
        GE_Damage_Spec damageEffect,
        float damage)
    {
        CombatDamageSnapshot snapshot = new CombatDamageSnapshot(
            finalHpDamage: damage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null,
            isCriticalHit: false);

        return CombatHitPayload.FromSnapshot(
            sourceSystem: sourceSystem,
            sourceSpec: null,
            damageEffect: damageEffect,
            knockbackEffect: null,
            snapshot: snapshot,
            hitConfirmedTag: null,
            causer: causer);
    }
}

using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 Skill1 투척체를 생성하고 현재 무기를 드롭 없이 파기한다.
    /// - 월드 공격 실행과 인벤토리 슬롯 제거를 한 번의 성공 실행 흐름으로 묶는다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_OddIronThrow", menuName = "GAS/Weapon/Odd Iron/Throw Logic")]
    public sealed class AbilityLogic_OddIronThrow : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec?.Definition == null)
                yield break;

            OddIronThrowData data = spec.Definition.sourceObject as OddIronThrowData;
            if (data == null || data.projectilePrefab == null)
                yield break;

            Vector2 direction = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
            Vector3 spawnPosition = OddIronAbilityUtility.ResolveSpawnPosition(system, direction, data.spawnOffset);

            CombatHitPayload payload = OddIronAbilityUtility.BuildFixedPayload(
                system,
                spec,
                data.damageConfig,
                data.damageEffect,
                data.knockbackEffect,
                data.fixedDamage,
                data.fixedStaggerDamage,
                data.fixedKnockbackImpulse);

            if (payload == null)
                yield break;

            GameObject projectileObject = Object.Instantiate(data.projectilePrefab, spawnPosition, Quaternion.identity);
            OddIronAbilityUtility.ApplyProjectileScale(projectileObject, data.projectileScale);
            OddIronThrownWeaponProjectile2D projectile = projectileObject != null
                ? projectileObject.GetComponent<OddIronThrownWeaponProjectile2D>()
                : null;
            if (projectile == null)
            {
                if (projectileObject != null)
                    Object.Destroy(projectileObject);
                yield break;
            }

            projectile.Setup(
                new ProjectileAttackSpawnContext
                {
                    ownerSystem = system,
                    sourceSpec = spec,
                    causer = system.gameObject,
                    ignoreTarget = system.gameObject,
                    lifetime = data.lifetime,
                    wallLayers = data.wallLayers,
                    damageLayers = data.damageLayers,
                    hitPayload = payload,
                    direction = direction,
                    speed = data.throwSpeed
                },
                data.angularSpeedDegrees,
                data.impactVfxPrefab,
                data.impactSound);

            AbilityAudioRouter.PlayOneShot(data.throwSound, system, spec, sourceObjectOverride: data);

            WeaponInventory2D inventory = OddIronAbilityUtility.ResolveInventory(system);
            inventory?.DestroyActiveWeaponWithoutDrop(equipFallback: true);
        }
    }
}

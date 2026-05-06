using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 기묘한 쇳덩이 기본 사격의 잔탄 소비, 고정 피해 payload 생성, 투사체 생성을 담당한다.
    /// - 잔탄 선택은 strategy가 맡지만 실제 소비는 성공 실행 시점에 한 번 더 검증한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_OddIronShot", menuName = "GAS/Weapon/Odd Iron/Shot Logic")]
    public sealed class AbilityLogic_OddIronShot : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec?.Definition == null)
                yield break;

            OddIronShotData data = spec.Definition.sourceObject as OddIronShotData;
            OddIronRuntimeData runtimeData = OddIronAbilityUtility.ResolveRuntimeData(system);
            if (data == null || data.projectilePrefab == null || runtimeData == null)
                yield break;

            if (!runtimeData.TryConsumeOneRound())
                yield break;

            FireOnce(system, spec, data, 0f);
        }

        internal static void FireOnce(AbilitySystem system, AbilitySpec spec, OddIronShotData data, float spreadAngle)
        {
            Vector2 baseDirection = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
            Vector2 direction = OddIronAbilityUtility.ApplySpread(baseDirection, spreadAngle);
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
                return;

            GameObject projectileObject = Object.Instantiate(data.projectilePrefab, spawnPosition, Quaternion.identity);
            OddIronAbilityUtility.ApplyProjectileScale(projectileObject, data.projectileScale);
            OddIronProjectile2D projectile = projectileObject != null
                ? projectileObject.GetComponent<OddIronProjectile2D>()
                : null;
            if (projectile == null)
            {
                if (projectileObject != null)
                    Object.Destroy(projectileObject);
                return;
            }

            projectile.Setup(new ProjectileAttackSpawnContext
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
                speed = data.projectileSpeed
            });

            OddIronAbilityUtility.SpawnMuzzleFlash(data.muzzleFlashPrefab, spawnPosition, direction);
            AbilityAudioRouter.PlayOneShot(data.fireSound, system, spec, sourceObjectOverride: data);
        }
    }
}

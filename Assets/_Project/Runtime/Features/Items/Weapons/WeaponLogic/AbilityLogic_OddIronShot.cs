using System.Collections;
using CapstoneAudio;
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
        [SerializeField] private SoundRef reloadSound;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec?.Definition == null)
                yield break;

            OddIronShotData data = spec.Definition.sourceObject as OddIronShotData;
            OddIronRuntimeData runtimeData = OddIronAbilityUtility.ResolveRuntimeData(system);
            if (data == null || data.projectilePrefab == null || runtimeData == null)
                yield break;

            if (!runtimeData.HasAmmo)
            {
                OddIronRuntimeState state = OddIronAbilityUtility.ResolveRuntimeState(system);
                OddIronReloadExecutor executor = state != null ? state.GetComponent<OddIronReloadExecutor>() : null;
                WeaponExecutorRunner runner = system.GetComponent<WeaponExecutorRunner>();
                WeaponInventory2D inventory = OddIronAbilityUtility.ResolveInventory(system);
                if (executor == null || executor.IsRunning || runner == null || inventory == null ||
                    !WeaponExclusiveRelics.Has(system.gameObject, WeaponExclusiveRelics.OddIronMagazine))
                    yield break;

                executor.Configure(reloadSound);
                runner.StartExecutor(executor, new WeaponAbilityExecutionContext
                {
                    AbilitySystem = system,
                    Owner = system.gameObject,
                    Weapon = inventory.ActiveWeapon,
                    RuntimeState = state,
                    Ability = spec.Definition,
                    Spec = spec
                });
                // Keep GAS busy until completion; this input reloads without firing.
                try
                {
                    while (executor != null && executor.IsRunning)
                    {
                        if (spec.Token != null && spec.Token.IsCancelled)
                        {
                            executor.Cancel();
                            yield break;
                        }
                        yield return null;
                    }
                }
                finally
                {
                    if (executor != null && executor.IsRunning)
                        executor.Cancel();
                }
                yield break;
            }
            if (!runtimeData.TryConsumeOneRound())
                yield break;

            FireOnce(system, spec, data, 0f);
        }

        internal static bool FireOnce(AbilitySystem system, AbilitySpec spec, OddIronShotData data, float spreadAngle)
        {
            Vector2 baseDirection = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
            Vector2 direction = OddIronAbilityUtility.ApplySpread(baseDirection, spreadAngle);
            Vector3 spawnPosition = PlayerAttackOrigin.Resolve(system, direction, data.wallLayers);
            Quaternion muzzleRotation = OddIronAbilityUtility.ResolveMuzzleRotation(system, direction);

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
                return false;

            GameObject projectileObject = Object.Instantiate(data.projectilePrefab, spawnPosition, Quaternion.identity);
            OddIronAbilityUtility.ApplyProjectileScale(projectileObject, data.projectileScale);
            OddIronProjectile2D projectile = projectileObject != null
                ? projectileObject.GetComponent<OddIronProjectile2D>()
                : null;
            if (projectile == null)
            {
                if (projectileObject != null)
                    Object.Destroy(projectileObject);
                return false;
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

            OddIronAbilityUtility.PlayFireRecoil(system, direction);
            OddIronAbilityUtility.SpawnMuzzleFlash(data.muzzleFlashPrefab, spawnPosition, muzzleRotation);
            AbilityAudioRouter.PlayOneShot(data.fireSound, system, spec, sourceObjectOverride: data);
            return true;
        }
    }
}

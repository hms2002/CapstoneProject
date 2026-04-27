using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 파편검 기본 공격의 조각 수 기반 범위/피해 계산과 근접 히트박스 생성을 담당한다.
    /// - 조각 소모 자체는 성공 발동 후 FragmentBladeRuntimeState가 처리하므로, 이 로직은 실행 시점 공격 판정에 집중한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_FragmentBladeAttack", menuName = "GAS/Weapon/Fragment Blade/Attack Logic")]
    public sealed class AbilityLogic_FragmentBladeAttack : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec?.Definition == null || system.AttributeSet == null)
                yield break;

            FragmentBladeAttackData data = spec.Definition.sourceObject as FragmentBladeAttackData;
            if (data == null || data.hitboxPrefab == null)
                yield break;

            FragmentBladeRuntimeData runtimeData = ResolveRuntimeData(system);
            float boundRatio = runtimeData != null ? runtimeData.BoundRatio : 1f;
            float hitboxScale = Mathf.Lerp(data.minimumHitboxScale, 1f, boundRatio);
            float damageScale = Mathf.Lerp(data.minimumDamageScale, 1f, boundRatio);

            CombatHitPayload payload = FragmentBladeDamageUtility.BuildPayload(
                system,
                spec,
                data.DamageConfig,
                data.damageEffect,
                data.knockbackEffect,
                data.damageFormula,
                data.knockbackFormula,
                data.legacyDamage,
                data.legacyStaggerDamage,
                damageScale,
                data.hitConfirmedTag);

            if (payload == null)
                yield break;

            Vector2 direction = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
            Vector2 center = (Vector2)system.transform.position + direction.normalized * data.forwardOffset;
            MeleeHitboxActor hitbox = Object.Instantiate(data.hitboxPrefab, center, Quaternion.identity);
            if (hitbox == null)
                yield break;

            hitbox.Setup(new MeleeHitboxSpawnContext
            {
                ownerSystem = system,
                sourceSpec = spec,
                causer = system.gameObject,
                ignoreTarget = system.gameObject,
                lifetime = data.activeTime,
                wallLayers = 0,
                damageLayers = data.hitLayers,
                hitPayload = payload,
                worldPosition = center,
                hitboxSize = data.fullHitboxSize * hitboxScale,
                hitOncePerTarget = true,
                destroyOnFirstHit = false,
                direction = direction
            });
        }

        private static FragmentBladeRuntimeData ResolveRuntimeData(AbilitySystem system)
        {
            WeaponInventory2D inventory = system != null ? system.GetComponent<WeaponInventory2D>() : null;
            return inventory != null ? inventory.ActiveRuntimeData as FragmentBladeRuntimeData : null;
        }
    }
}

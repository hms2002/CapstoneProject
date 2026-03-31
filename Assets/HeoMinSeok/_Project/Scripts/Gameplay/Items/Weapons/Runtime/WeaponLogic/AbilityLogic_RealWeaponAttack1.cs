using System.Collections;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - "진짜 무기" 일반 공격(1타)의 발동 문맥을 계산한다.
    /// - 방향, 위치, 피해 스냅샷을 준비하고 MeleeHitboxActor를 생성해 넘긴다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_RW_Attack1", menuName = "GAS/Weapon/RealWeapon/Logic Attack1")]
    public sealed class AbilityLogic_RealWeaponAttack1 : AbilityLogic
    {
        public RealWeaponAttack1Data data;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || data == null) yield break;
            if (data.damageEffect == null) yield break;
            if (data.hitboxPrefab == null)
            {
                Debug.LogError("[AbilityLogic_RealWeaponAttack1] hitboxPrefab is null.");
                yield break;
            }

            if (system.AttributeSet == null) yield break;

            Vector2 dir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
            Vector2 center = (Vector2)system.transform.position + dir * data.forwardOffset;

            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);

            var snapshot = DamageSnapshotBuilder.Build(
                attributeSet: system.AttributeSet,
                statProvider: statProvider,
                config: data.DamageConfig,
                damageFormula: data.damageFormula,
                knockbackFormula: data.knockbackFormula
            );

            // 책임 :
            // - 근접 공격의 최종 피해량을 공용 CombatHitPayload 규약으로 고정한다.
            // - 이후 MeleeHitboxActor는 payload 적용만 수행하고 수치 계산 책임은 AbilityLogic에 남긴다.
            var payload = CombatHitPayload.FromSnapshot(
                sourceSystem: system,
                sourceSpec: spec,
                damageEffect: data.damageEffect,
                knockbackEffect: data.knockbackEffect,
                snapshot: snapshot,
                hitConfirmedTag: null,
                causer: system.gameObject);

            var hitbox = Object.Instantiate(data.hitboxPrefab, center, Quaternion.identity);
            if (hitbox == null)
                yield break;

            var context = new MeleeHitboxSpawnContext
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
                hitboxSize = data.hitboxSize,
                hitOncePerTarget = true,
                destroyOnFirstHit = false,
                direction = dir
            };

            hitbox.Setup(context);
            yield break;
        }
    }
}

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
            // - 근접 공격이 나중에 적중하더라도 발동 순간의 피해량을 사용하도록 payload를 고정한다.
            // - 즉시 질의형 근접에서 actor 기반 근접으로 옮겨도 수치 계산 책임은 여전히 AbilityLogic에 남긴다.
            var payload = new AttackHitPayload
            {
                damageEffect = data.damageEffect,
                knockbackEffect = data.knockbackEffect,
                finalHpDamage = snapshot.FinalHpDamage,
                finalStaggerBuildUp = snapshot.FinalStaggerBuildUp,
                elementDamages = snapshot.ElementBuildUps != null && snapshot.ElementBuildUps.Count > 0
                    ? snapshot.ElementBuildUps.ToArray()
                    : null,
                finalKnockbackImpulse = snapshot.FinalKnockbackImpulse,
                hitConfirmedTag = null
            };

            var hitbox = Object.Instantiate(data.hitboxPrefab, center, Quaternion.identity);
            if (hitbox == null)
                yield break;

            // 책임 :
            // - 근접 히트박스를 짧게 유지하면서, 생성 직후 겹쳐 있는 적과 이후 들어오는 적을 모두 처리한다.
            // - destroyOnFirstHit=false 로 두어 기존 OverlapBox 방식처럼 여러 적을 동시에 맞출 수 있게 유지한다.
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
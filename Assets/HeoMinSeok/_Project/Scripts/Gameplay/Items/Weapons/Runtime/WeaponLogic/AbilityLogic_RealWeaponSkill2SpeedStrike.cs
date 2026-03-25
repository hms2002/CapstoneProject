using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - SpeedStrike의 발동 문맥을 계산한다.
    /// - 공격 방향, 히트박스 위치, 피해 스냅샷을 준비한 뒤 MeleeHitboxActor를 생성한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_RW_Skill2_SpeedStrike", menuName = "GAS/Weapon/RealWeapon/Logic Skill2 SpeedStrike")]
    public sealed class AbilityLogic_RealWeaponSkill2SpeedStrike : AbilityLogic
    {
        public RealWeaponSkill2SpeedStrikeData data;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || data == null) yield break;
            if (data.damageEffect == null) yield break;
            if (data.hitboxPrefab == null)
            {
                Debug.LogError("[AbilityLogic_RealWeaponSkill2SpeedStrike] hitboxPrefab is null.");
                yield break;
            }

            var attr = system.AttributeSet;
            if (attr == null) yield break;

            var cfg = data.DamageConfig;
            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);

            Vector2 dir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);
            Vector2 center = (Vector2)system.transform.position + dir * data.forwardOffset;

            float atk = 0f;
            float ms = 1f;

            if (statProvider != null)
            {
                atk = statProvider.Get(data.attackStatId);
                ms = statProvider.Get(data.moveSpeedMultiplierStatId);
            }
            else
            {
                if (data.attackAttribute == null || data.moveSpeedMultiplierAttribute == null)
                    yield break;

                atk = attr.GetAttributeValue(data.attackAttribute);
                ms = attr.GetAttributeValue(data.moveSpeedMultiplierAttribute);
            }

            ms = Mathf.Max(0f, ms);
            float baseHp = atk * (ms * data.speedScale);

            float baseKnockback = 0f;
            if (data.knockbackFormula != null)
                baseKnockback = data.knockbackFormula.Evaluate(attr, statProvider, defaultIfEmpty: 0f);

            float baseStagger = 0f;
            if (cfg != null && cfg.includeStaggerBuildUp && cfg.staggerFormula != null)
                baseStagger = cfg.staggerFormula.Evaluate(attr, statProvider, defaultIfEmpty: 0f);

            List<ElementDamageInput> elementInputs = null;
            if (cfg != null && cfg.includeElementBuildUp && cfg.HasElementFormulas)
            {
                elementInputs = new List<ElementDamageInput>(cfg.elementFormulas.Length);

                for (int i = 0; i < cfg.elementFormulas.Length; i++)
                {
                    var e = cfg.elementFormulas[i];
                    if (e == null || e.elementType == null || e.formula == null)
                        continue;

                    float v = e.formula.Evaluate(attr, statProvider, defaultIfEmpty: 0f);
                    if (v <= 0f)
                        continue;

                    elementInputs.Add(new ElementDamageInput
                    {
                        elementType = e.elementType,
                        baseDamage = v
                    });
                }
            }

            var snapshot = DamageSnapshotBuilder.BuildFromBaseValues(
                statProvider: statProvider,
                config: cfg,
                baseHp: baseHp,
                baseStagger: baseStagger,
                baseKnockback: baseKnockback,
                elementInputs: elementInputs
            );

            var payload = new CombatHitPayload
            {
                sourceSystem = system,
                sourceSpec = spec,
                damageEffect = data.damageEffect,
                knockbackEffect = data.knockbackEffect,
                finalHpDamage = snapshot.FinalHpDamage,
                finalStaggerBuildUp = snapshot.FinalStaggerBuildUp,
                elementBuildUps = snapshot.ElementBuildUps != null && snapshot.ElementBuildUps.Count > 0
                    ? snapshot.ElementBuildUps.ToArray()
                    : null,
                finalKnockbackImpulse = snapshot.FinalKnockbackImpulse,
                hitConfirmedTag = null,
                causer = system.gameObject
            };

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
        }
    }
}
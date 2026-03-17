using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 스킬2: 전방 1회 공격
    /// - baseHp = ATK_FINAL * (MoveSpeedMultiplier_FINAL * speedScale)
    /// - 이후 DamageFormulaUtil.PostProcess로 공통 후처리(치명/최종배율 등)만 적용
    /// </summary>
    [CreateAssetMenu(fileName = "AL_RW_Skill2_SpeedStrike", menuName = "GAS/Weapon/RealWeapon/Logic Skill2 SpeedStrike")]
    public sealed class AbilityLogic_RealWeaponSkill2SpeedStrike : AbilityLogic
    {
        public RealWeaponSkill2SpeedStrikeData data;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || data == null) yield break;
            if (data.damageEffect == null) yield break;

            var attr = system.AttributeSet;
            if (attr == null) yield break;

            var cfg = data.DamageConfig;
            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);

            Vector2 dir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);

            Vector2 center = (Vector2)system.transform.position + dir * data.forwardOffset;
            var td = AbilityTargetData2D.FromOverlapBox(
                center,
                data.hitboxSize,
                0f,
                data.hitLayers,
                ignore: system.gameObject);

            if (td.Targets.Count == 0)
                yield break;

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

            CombatDamageApplicator.ApplyToTargets(
                system: system,
                spec: spec,
                damageEffect: data.damageEffect,
                knockbackEffect: data.knockbackEffect,
                targets: td.Targets,
                snapshot: snapshot,
                hitConfirmedTag: null,
                causer: system.gameObject
            );

            yield break;
        }
    }
}
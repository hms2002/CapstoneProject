using System.Collections;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_SwordSkill2_BigSlash", menuName = "GAS/Samples/AbilityLogic/Sword Skill2 BigSlash")]
    public class AbilityLogic_SwordSkill2_BigSlash : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            var def = spec?.Definition;
            if (system == null || def == null) yield break;

            var data = def.sourceObject as SwordSkill2BigSlashData;
            if (data == null || data.damageEffect == null) yield break;

            Vector2 dir = ResolveAimDirection(system);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();

            if (data.hitEventTag != null)
            {
                yield return AbilityTasks.WaitGameplayEvent(
                    system, spec, data.hitEventTag,
                    onReceived: _ => { },
                    timeout: data.hitEventTimeout,
                    predicate: d => d.Spec == spec
                );
            }

            spec.SetFloat("RecoveryOverride", data.recoveryOverride);

            Vector2 center = (Vector2)system.transform.position + dir * data.forwardOffset;
            var td = AbilityTargetData2D.FromOverlapBox(center, data.hitboxSize, 0f, data.hitLayers, ignore: system.gameObject);
            if (td.Targets.Count == 0) yield break;

            var runner = system.EffectRunner;
            if (runner == null) yield break;

            float legacyBaseHp = data.damage;
            float baseHp = legacyBaseHp;
            if (data.damageFormula != null)
                baseHp = data.damageFormula.Evaluate(system.AttributeSet, defaultIfEmpty: legacyBaseHp);
            float finalHp = baseHp;
            float finalStagger = 0f;
            float finalKnockback = 0f;
            if (data.knockbackFormula != null)
                finalKnockback = data.knockbackFormula.Evaluate(system.AttributeSet, defaultIfEmpty: 0f);
            var stats = system.DamageProfile != null ? system.DamageProfile.formulaStats : null;

            System.Collections.Generic.List<ElementDamageResult> elementResults = null;

            if (stats != null)
            {
                var attackerAttr = system.AttributeSet;
                DamageResult result;
                var dmgCfg = data.DamageConfig;
                bool includeElementBuildup = dmgCfg != null ? dmgCfg.includeElementBuildup : data.includeElementDamage;
                bool includeStagger = dmgCfg != null ? dmgCfg.includeStaggerDamage : data.includeStaggerDamage;

                if (includeElementBuildup)
                {
                    var inputs = (data.elementDamages != null) ? (System.Collections.Generic.IReadOnlyList<ElementDamageInput>)data.elementDamages : System.Array.Empty<ElementDamageInput>();
                    elementResults = new System.Collections.Generic.List<ElementDamageResult>(inputs.Count);
                    result = DamageFormulaUtil.Compute(
                        attackerAttr,
                        stats,
                        DamageAttackKind.Skill,
                        baseHpDamage: data.damage,
                        baseStaggerDamage: data.baseStaggerDamage,
                        elementInputs: inputs,
                        outElementResults: elementResults,
                        includeStagger: includeStagger
                    );
                }
                else
                {
                    result = DamageFormulaUtil.Compute(
                        attackerAttr,
                        stats,
                        DamageAttackKind.Skill,
                        baseHpDamage: data.damage,
                        baseStaggerDamage: data.baseStaggerDamage,
                        includeElement: includeElementBuildup,
                        includeStagger: includeStagger
                    );
                }

                finalHp = result.hpDamage;
                finalStagger = result.staggerDamage;
            }

            for (int i = 0; i < td.Targets.Count; i++)
            {
                var target = td.Targets[i];
                if (target == null) continue;

                CombatDamageAction.ApplyDamageAndEmitHit(
                    system, spec,
                    data.damageEffect,
                    target,
                    finalHp,
                    finalStagger,
                    elementResults,
                    hitConfirmedTag: null,
                    causer: system.gameObject
                );
            }
        }


        private Vector2 ResolveAimDirection(AbilitySystem system)
        {
            var input = system.GetComponent<PlayerCombatInput2D>();
            if (input != null) return input.AimDirection;

            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
                w.z = 0f;
                Vector2 d = (Vector2)(w - system.transform.position);
                if (d.sqrMagnitude > 0.0001f) return d.normalized;
            }
            return Vector2.right;
        }
    }
}

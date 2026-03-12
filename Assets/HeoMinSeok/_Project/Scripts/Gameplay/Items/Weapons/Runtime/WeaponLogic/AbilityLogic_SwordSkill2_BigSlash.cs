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

            var cfg = data.DamageConfig;
            var bindings = system.DamageProfile != null ? system.DamageProfile.GetStatBindings() : null;
            IStatProvider statProvider = bindings != null ? new AttributeStatProvider(system.AttributeSet, bindings) : null;

            var post = (cfg != null && cfg.postProcess != null)
                ? cfg.postProcess
                : (system.DamageProfile != null ? system.DamageProfile.GetDefaultPostProcess() : null);

            float legacyBaseHp = data.damage;
            float baseHp = (data.damageFormula != null)
                ? data.damageFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: legacyBaseHp)
                : legacyBaseHp;

            float baseKnockback = (data.knockbackFormula != null)
                ? data.knockbackFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : 0f;

            float baseStagger = (cfg != null && cfg.includeStaggerBuildUp && cfg.staggerFormula != null)
                ? cfg.staggerFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : Mathf.Max(0f, data.baseStaggerDamage);

            System.Collections.Generic.List<ElementDamageInput> elementInputs = null;
            if (cfg != null && cfg.includeElementBuildUp && cfg.HasElementFormulas)
            {
                elementInputs = new System.Collections.Generic.List<ElementDamageInput>(cfg.elementFormulas.Length);
                for (int i = 0; i < cfg.elementFormulas.Length; i++)
                {
                    var e = cfg.elementFormulas[i];
                    if (e == null || e.elementType == null || e.formula == null) continue;
                    float v = e.formula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f);
                    if (v <= 0f) continue;
                    elementInputs.Add(new ElementDamageInput { elementType = e.elementType, baseDamage = v });
                }
            }
            else if (data.elementDamages != null && data.elementDamages.Count > 0)
            {
                elementInputs = new System.Collections.Generic.List<ElementDamageInput>(data.elementDamages);
            }

            System.Collections.Generic.List<ElementDamageResult> elementResults = (elementInputs != null && elementInputs.Count > 0)
                ? new System.Collections.Generic.List<ElementDamageResult>(elementInputs.Count)
                : null;

            var processed = DamageFormulaUtil.PostProcess(
                attacker: system.AttributeSet,
                post: post,
                baseHpDamage: baseHp,
                baseStaggerDamage: baseStagger,
                elementInputs: elementInputs,
                outElementResults: elementResults,
                critAffectsElement: (cfg == null ? true : cfg.critAffectsElement)
            );

            float finalHp = processed.hpDamage;
            float finalStagger = processed.staggerDamage;
            float finalKnockback = baseKnockback;

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

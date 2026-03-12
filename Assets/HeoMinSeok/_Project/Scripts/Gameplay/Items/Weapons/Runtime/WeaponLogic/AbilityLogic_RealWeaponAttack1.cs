using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// "진짜 무기" - 일반 공격(1타)
    /// - OverlapBox 1회
    /// - 피해/넉백은 ScaledStatFormula 기반
    /// </summary>
    [CreateAssetMenu(fileName = "AL_RW_Attack1", menuName = "GAS/Weapon/RealWeapon/Logic Attack1")]
    public sealed class AbilityLogic_RealWeaponAttack1 : AbilityLogic
    {
        public RealWeaponAttack1Data data;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || data == null) yield break;
            if (data.damageEffect == null) yield break;

            // Aim direction (player) fallback
            Vector2 dir = Vector2.right;
            if (system.TryGetComponent<SampleTopDownPlayer>(out var p))
                dir = p.AimDirection.sqrMagnitude > 0.0001f ? p.AimDirection.normalized : Vector2.right;
            else
                dir = system.transform.right;

            // Hitbox
            Vector2 center = (Vector2)system.transform.position + dir * data.forwardOffset;
            var td = AbilityTargetData2D.FromOverlapBox(center, data.hitboxSize, 0f, data.hitLayers, ignore: system.gameObject);
            if (td.Targets.Count == 0) yield break;

            var cfg = data.DamageConfig;
            var bindings = system.DamageProfile != null ? system.DamageProfile.GetStatBindings() : null;
            IStatProvider statProvider = bindings != null ? new AttributeStatProvider(system.AttributeSet, bindings) : null;

            var post = (cfg != null && cfg.postProcess != null)
                ? cfg.postProcess
                : (system.DamageProfile != null ? system.DamageProfile.GetDefaultPostProcess() : null);

            // Compute base values
            float baseHp = 0f;
            if (data.damageFormula != null)
                baseHp = data.damageFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f);

            float baseKnockback = 0f;
            if (data.knockbackFormula != null)
                baseKnockback = data.knockbackFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f);

            float baseStagger = 0f;
            if (cfg != null && cfg.includeStaggerBuildUp && cfg.staggerFormula != null)
                baseStagger = cfg.staggerFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f);

            List<ElementDamageInput> elementInputs = null;
            if (cfg != null && cfg.includeElementBuildUp && cfg.HasElementFormulas)
            {
                elementInputs = new List<ElementDamageInput>(cfg.elementFormulas.Length);
                for (int i = 0; i < cfg.elementFormulas.Length; i++)
                {
                    var e = cfg.elementFormulas[i];
                    if (e == null || e.elementType == null || e.formula == null) continue;
                    float v = e.formula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f);
                    if (v <= 0f) continue;
                    elementInputs.Add(new ElementDamageInput { elementType = e.elementType, baseDamage = v });
                }
            }

            List<ElementDamageResult> elementResults = null;
            if (elementInputs != null && elementInputs.Count > 0)
                elementResults = new List<ElementDamageResult>(elementInputs.Count);

            var r = DamageFormulaUtil.PostProcess(
                attacker: system.AttributeSet,
                post: post,
                baseHpDamage: baseHp,
                baseStaggerDamage: baseStagger,
                elementInputs: elementInputs,
                outElementResults: elementResults,
                critAffectsElement: (cfg == null ? true : cfg.critAffectsElement)
            );

            float finalHp = r.hpDamage;
            float finalStagger = r.staggerDamage;

            // Apply to all targets
            for (int i = 0; i < td.Targets.Count; i++)
            {
                var target = td.Targets[i];
                if (target == null) continue;

                CombatDamageAction.ApplyDamageAndEmitHit(
                    system: system,
                    spec: spec,
                    target: target,
                    damageEffect: data.damageEffect,
                    finalHpDamage: finalHp,
                    finalStaggerBuildUp: finalStagger,
                    elementBuildUps: elementResults,
                    finalKnockbackImpulse: baseKnockback,
                    hitConfirmedTag: null,
                    causer: system.gameObject
                );
            }

            yield break;
        }
    }
}

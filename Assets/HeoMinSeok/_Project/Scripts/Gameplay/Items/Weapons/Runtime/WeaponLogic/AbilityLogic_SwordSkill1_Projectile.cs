using System.Collections;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_SwordSkill1_Projectile", menuName = "GAS/Samples/AbilityLogic/Sword Skill1 Projectile")]
    public class AbilityLogic_SwordSkill1_Projectile : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            var def = spec?.Definition;
            if (system == null || def == null) yield break;

            var data = def.sourceObject as SwordSkill1ProjectileData;
            if (data == null || data.projectilePrefab == null) yield break;

            Vector2 dir = ResolveAimDirection(system);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();

            Vector3 spawnPos = system.transform.position + data.spawnOffset;
            var go = Object.Instantiate(data.projectilePrefab, spawnPos, Quaternion.identity);

            // Damage snapshot at cast time
            var cfg = data.DamageConfig;
            var bindings = system.DamageProfile != null ? system.DamageProfile.GetStatBindings() : null;
            IStatProvider statProvider = bindings != null ? new AttributeStatProvider(system.AttributeSet, bindings) : null;

            var post = (cfg != null && cfg.postProcess != null)
                ? cfg.postProcess
                : (system.DamageProfile != null ? system.DamageProfile.GetDefaultPostProcess() : null);

            float legacyBaseHp = data.damage;
            float baseHp = data.damageFormula != null
                ? data.damageFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: legacyBaseHp)
                : legacyBaseHp;

            float baseKnockback = (data.knockbackFormula != null)
                ? data.knockbackFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : 0f;

            float baseStagger = (cfg != null && cfg.includeStaggerBuildUp && cfg.staggerFormula != null)
                ? cfg.staggerFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : Mathf.Max(0f, data.baseStaggerDamage);

            // Element build-up: prefer formulas in config. If none, fall back to legacy list (treated as FINAL values).
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

            var elementResults = (elementInputs != null && elementInputs.Count > 0)
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
            ElementDamageResult[] elementSnapshot = (elementResults != null && elementResults.Count > 0)
                ? elementResults.ToArray()
                : null;

            float finalKnockback = baseKnockback;
            var proj = go.GetComponent<SwordSkill1Projectile2D>();
            if (proj != null)
            {
                proj.Setup(
                    owner: system,
                    direction: dir,
                    spd: data.projectileSpeed,
                    lifetime: data.lifetime,
                    walls: data.wallLayers,
                    dmgLayers: data.damageLayers,
                    dmgEffect: data.damageEffect,
                    dmg: finalHp,
                    staggerBuildUp: finalStagger,
                    elementDamages: elementSnapshot,
                    knockbackImpulse: finalKnockback,
                    ignore: system.gameObject
                );
            }

            yield break;
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

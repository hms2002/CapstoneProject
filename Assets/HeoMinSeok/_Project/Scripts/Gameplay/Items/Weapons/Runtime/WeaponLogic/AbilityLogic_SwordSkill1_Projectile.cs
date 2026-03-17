using System.Collections;
using System.Collections.Generic;
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
            if (system.AttributeSet == null) yield break;

            var data = def.sourceObject as SwordSkill1ProjectileData;
            if (data == null || data.projectilePrefab == null) yield break;

            Vector2 dir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);

            Vector3 spawnPos = system.transform.position + data.spawnOffset;
            var go = Object.Instantiate(data.projectilePrefab, spawnPos, Quaternion.identity);

            var cfg = data.DamageConfig;
            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);

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

            // Element build-up:
            // 1) config formula 우선
            // 2) 없으면 legacy list를 최종값처럼 사용
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

                    elementInputs.Add(new ElementDamageInput
                    {
                        elementType = e.elementType,
                        baseDamage = v
                    });
                }
            }
            else if (data.elementDamages != null && data.elementDamages.Count > 0)
            {
                elementInputs = new List<ElementDamageInput>(data.elementDamages);
            }

            var snapshot = DamageSnapshotBuilder.BuildFromBaseValues(
                statProvider: statProvider,
                config: cfg,
                baseHp: baseHp,
                baseStagger: baseStagger,
                baseKnockback: baseKnockback,
                elementInputs: elementInputs
            );

            ElementDamageResult[] elementSnapshot =
                (snapshot.ElementBuildUps != null && snapshot.ElementBuildUps.Count > 0)
                ? snapshot.ElementBuildUps.ToArray()
                : null;

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
                    kbEffect: data.knockbackEffect,
                    dmg: snapshot.FinalHpDamage,
                    staggerBuildUp: snapshot.FinalStaggerBuildUp,
                    elementDamages: elementSnapshot,
                    knockbackImpulse: snapshot.FinalKnockbackImpulse,
                    ignore: system.gameObject
                );
            }

            yield break;
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

namespace UnityGAS
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
                // 책임:
                // 발사 시점에 고정되어야 하는 피해 정보를 캡처한다.
                // projectile가 나중에 충돌해도 CurrentExecSpec를 다시 읽지 않도록 sourceSpec과 payload를 묶어 전달한다.
                var payload = new AttackHitPayload
                {
                    damageEffect = data.damageEffect,
                    knockbackEffect = data.knockbackEffect,
                    finalHpDamage = snapshot.FinalHpDamage,
                    finalStaggerBuildUp = snapshot.FinalStaggerBuildUp,
                    elementDamages = elementSnapshot != null ? (ElementDamageResult[])elementSnapshot.Clone() : null,
                    finalKnockbackImpulse = snapshot.FinalKnockbackImpulse,
                    hitConfirmedTag = null
                };

                var context = new ProjectileAttackSpawnContext
                {
                    ownerSystem = system,
                    sourceSpec = spec,
                    causer = system.gameObject,
                    ignoreTarget = system.gameObject,
                    lifetime = data.lifetime,
                    wallLayers = data.wallLayers,
                    damageLayers = data.damageLayers,
                    hitPayload = payload,
                    direction = dir,
                    speed = data.projectileSpeed
                };

                proj.Setup(context);
            }

            yield break;
        }
    }
}
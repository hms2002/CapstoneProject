using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - SwordSkill1 투사체 스킬의 발사 시점 계산을 담당한다.
    /// - 방향, 피해 스냅샷, 투사체 생성 문맥을 준비한 뒤 projectile에게 넘긴다.
    /// </summary>
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
            if (proj == null)
            {
                Debug.LogError("[AbilityLogic_SwordSkill1_Projectile] Projectile prefab is missing SwordSkill1Projectile2D.", go);
                Object.Destroy(go);
                yield break;
            }

            // 책임 :
            // - 발사 시점에 확정된 피해 정보를 공용 CombatHitPayload로 고정한다.
            // - 이후 투사체/근접/유물 공격이 같은 payload 규약으로 피해를 적용하게 한다.
            var payload = new CombatHitPayload
            {
                sourceSystem = system,
                sourceSpec = spec,
                damageEffect = data.damageEffect,
                knockbackEffect = data.knockbackEffect,
                finalHpDamage = snapshot.FinalHpDamage,
                finalStaggerBuildUp = snapshot.FinalStaggerBuildUp,
                elementBuildUps = elementSnapshot != null ? (ElementDamageResult[])elementSnapshot.Clone() : null,
                finalKnockbackImpulse = snapshot.FinalKnockbackImpulse,
                hitConfirmedTag = null,
                causer = system.gameObject
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
            yield break;
        }
    }
}
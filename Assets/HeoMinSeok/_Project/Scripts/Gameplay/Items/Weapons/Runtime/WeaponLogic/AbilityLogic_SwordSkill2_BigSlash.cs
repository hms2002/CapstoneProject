using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - BigSlash의 발동 문맥을 계산한다.
    /// - 히트 이벤트를 기다린 뒤, 피해 스냅샷과 히트박스 문맥을 준비해서 MeleeHitboxActor를 생성한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_SwordSkill2_BigSlash", menuName = "GAS/Samples/AbilityLogic/Sword Skill2 BigSlash")]
    public class AbilityLogic_SwordSkill2_BigSlash : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            var def = spec?.Definition;
            if (system == null || def == null) yield break;

            var data = def.sourceObject as SwordSkill2BigSlashData;
            if (data == null || data.damageEffect == null) yield break;
            if (data.hitboxPrefab == null)
            {
                Debug.LogError("[AbilityLogic_SwordSkill2_BigSlash] hitboxPrefab is null.");
                yield break;
            }

            if (system.AttributeSet == null) yield break;

            Vector2 dir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);

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

            var cfg = data.DamageConfig;
            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);

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

            var payload = CombatHitPayload.FromSnapshot(
                sourceSystem: system,
                sourceSpec: spec,
                damageEffect: data.damageEffect,
                knockbackEffect: data.knockbackEffect,
                snapshot: snapshot,
                hitConfirmedTag: null,
                causer: system.gameObject);

            Vector2 center = (Vector2)system.transform.position + dir * data.forwardOffset;

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

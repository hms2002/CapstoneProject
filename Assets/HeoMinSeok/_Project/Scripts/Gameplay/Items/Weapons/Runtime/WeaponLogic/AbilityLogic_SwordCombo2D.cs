using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 3연속 콤보 공격의 상태 진행을 담당한다.
    /// - 콤보 인덱스 결정, 애니메이션/돌진 처리, 히트 이벤트 대기 후 MeleeHitboxActor를 생성한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_SwordCombo2D", menuName = "GAS/Samples/AbilityLogic/Sword Combo 2D")]
    public class AbilityLogic_SwordCombo2D : AbilityLogic
    {
        private const string KEY_COMBO_INDEX = "Sword.ComboIndex";
        private const string KEY_COMBO_EXPIRE = "Sword.ComboExpire";

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec == null || spec.Definition == null) yield break;
            if (system.AttributeSet == null) yield break;

            var data = spec.Definition.sourceObject as SwordCombo2DData;
            if (data == null)
            {
                Debug.LogError("[SwordCombo2D] AbilityDefinition.sourceObject must be SwordCombo2DData.");
                yield break;
            }

            if (data.hitboxPrefab == null)
            {
                Debug.LogError("[SwordCombo2D] hitboxPrefab is null.");
                yield break;
            }

            Vector2 dir = AbilityAimResolver2D.Resolve(system.gameObject, Vector2.right);

            int comboIndex = ResolveComboIndex(spec, data.comboResetTime);
            spec.SetInt(KEY_COMBO_INDEX, comboIndex);
            spec.SetFloat(KEY_COMBO_EXPIRE, Time.time + data.comboResetTime);

            TryPlayAnim(system, data, comboIndex, spec.Definition);

            yield return Lunge(
                system,
                spec,
                dir,
                GetArraySafe(data.lungeDistances, comboIndex, 0f),
                GetArraySafe(data.lungeDurations, comboIndex, 0f));

            if (data.hitEventTag != null)
            {
                yield return AbilityTasks.WaitGameplayEvent(
                    system, spec, data.hitEventTag,
                    onReceived: _ => { },
                    timeout: data.hitEventTimeout,
                    predicate: d => d.Spec == spec
                );
            }

            float rec = GetArraySafe(data.recoveryOverrides, comboIndex, spec.Definition.recoveryTime);
            spec.SetFloat("RecoveryOverride", rec);

            SpawnHitbox(system, spec, data, comboIndex, dir);
        }

        private int ResolveComboIndex(AbilitySpec spec, float resetTime)
        {
            float expire = spec.GetFloat(KEY_COMBO_EXPIRE, -1f);
            int current = spec.GetInt(KEY_COMBO_INDEX, -1);

            if (expire > 0f && Time.time <= expire && current >= 0)
                return (current + 1) % 3;

            return 0;
        }

        private void TryPlayAnim(AbilitySystem system, SwordCombo2DData data, int comboIndex, AbilityDefinition definition)
        {
            string trig = GetArraySafe(data.animTriggers, comboIndex, "");
            if (string.IsNullOrEmpty(trig)) return;
            system.TryPlayAnimationTriggerHash(Animator.StringToHash(trig), definition);
        }

        private IEnumerator Lunge(AbilitySystem system, AbilitySpec spec, Vector2 dir, float distance, float duration)
        {
            if (distance <= 0f || duration <= 0f) yield break;

            var motion = system.GetComponent<AbilityMotionController2D>();
            if (motion == null)
            {
                Debug.LogError("[SwordCombo2D] AbilityMotionController2D가 필요합니다.");
                yield break;
            }

            Vector2 start = system.transform.position;
            motion.StartLunge(start, dir, distance, duration);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (spec?.Token != null && spec.Token.IsCancelled)
                {
                    motion.CancelMotion();
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 책임 :
        /// - 콤보 단계별 중심점, 피해량, 넉백, 원소 누적량을 계산한다.
        /// - 계산 결과를 payload/context로 묶어 짧게 유지되는 근접 히트박스를 생성한다.
        /// </summary>
        private void SpawnHitbox(AbilitySystem system, AbilitySpec abilitySpec, SwordCombo2DData data, int comboIndex, Vector2 dir)
        {
            if (data.damageEffect == null) return;
            if (system.AttributeSet == null) return;

            var cfg = data.DamageConfig;
            bool includeStagger = (cfg != null) && cfg.includeStaggerBuildUp;

            Vector2 perp = new Vector2(-dir.y, dir.x);
            int sideSign = GetArraySafe(data.sideSigns, comboIndex, 0);

            Vector2 center = (Vector2)system.transform.position
                             + dir * data.forwardOffset
                             + perp * (data.sideOffset * sideSign);

#if UNITY_EDITOR
            if (system.TryGetComponent<UnityGAS.Sample.RealtimeHitboxGizmo2D>(out var gizmo))
            {
                var col = (comboIndex == 0) ? Color.green : (comboIndex == 1 ? Color.yellow : Color.cyan);
                gizmo.RecordBox(center, data.hitboxSize, 0f, 0.15f, col);
            }
#endif

            IStatProvider statProvider = AbilityStatProviderFactory.Create(system);

            float legacyBaseHp = GetArraySafe(data.damages, comboIndex, 0f);
            float baseHp = legacyBaseHp;
            if (data.damageFormulas != null &&
                comboIndex >= 0 &&
                comboIndex < data.damageFormulas.Length &&
                data.damageFormulas[comboIndex] != null)
            {
                baseHp = data.damageFormulas[comboIndex].Evaluate(
                    system.AttributeSet,
                    statProvider,
                    defaultIfEmpty: legacyBaseHp);
            }

            float baseStagger = (cfg != null && cfg.includeStaggerBuildUp && cfg.staggerFormula != null)
                ? cfg.staggerFormula.Evaluate(system.AttributeSet, statProvider, defaultIfEmpty: 0f)
                : GetArraySafe(data.staggerDamages, comboIndex, 0f);

            float baseKnockback = 0f;
            if (data.knockbackFormulas != null &&
                comboIndex >= 0 &&
                comboIndex < data.knockbackFormulas.Length &&
                data.knockbackFormulas[comboIndex] != null)
            {
                baseKnockback = data.knockbackFormulas[comboIndex].Evaluate(
                    system.AttributeSet,
                    statProvider,
                    defaultIfEmpty: 0f);
            }

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
            else
            {
                var grp = GetArraySafe(data.elementDamagesByCombo, comboIndex, null);
                if (grp != null && grp.elements != null && grp.elements.Count > 0)
                    elementInputs = new List<ElementDamageInput>(grp.elements);
            }

            var snapshot = DamageSnapshotBuilder.BuildFromBaseValues(
                statProvider: statProvider,
                config: cfg,
                baseHp: baseHp,
                baseStagger: includeStagger ? baseStagger : 0f,
                baseKnockback: baseKnockback,
                elementInputs: elementInputs
            );

            if (snapshot.FinalHpDamage <= 0f)
                return;

            // 책임 :
            // - 콤보 단계별 최종 피해량을 공용 CombatHitPayload 규약으로 고정한다.
            // - 이후 콤보 히트박스도 다른 공격체와 같은 방식으로 피해를 적용한다.
            var payload = new CombatHitPayload
            {
                sourceSystem = system,
                sourceSpec = abilitySpec,
                damageEffect = data.damageEffect,
                knockbackEffect = data.knockbackEffect,
                finalHpDamage = snapshot.FinalHpDamage,
                finalStaggerBuildUp = snapshot.FinalStaggerBuildUp,
                elementBuildUps = snapshot.ElementBuildUps != null && snapshot.ElementBuildUps.Count > 0
                    ? snapshot.ElementBuildUps.ToArray()
                    : null,
                finalKnockbackImpulse = snapshot.FinalKnockbackImpulse,
                hitConfirmedTag = data.hitConfirmedTag,
                causer = system.gameObject
            };

            var hitbox = Object.Instantiate(data.hitboxPrefab, center, Quaternion.identity);
            if (hitbox == null)
                return;

            var context = new MeleeHitboxSpawnContext
            {
                ownerSystem = system,
                sourceSpec = abilitySpec,
                causer = system.gameObject,
                ignoreTarget = system.gameObject,
                lifetime = GetArraySafe(data.activeTimes, comboIndex, 0.08f),
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

        private static T GetArraySafe<T>(T[] arr, int index, T fallback)
        {
            if (arr == null || arr.Length == 0) return fallback;
            index = Mathf.Clamp(index, 0, arr.Length - 1);
            return arr[index];
        }
    }
}
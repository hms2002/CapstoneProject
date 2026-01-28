using System.Collections;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_SwordCombo2D", menuName = "GAS/Samples/AbilityLogic/Sword Combo 2D")]
    public class AbilityLogic_SwordCombo2D : AbilityLogic
    {
        private const string KEY_COMBO_INDEX = "Sword.ComboIndex";
        private const string KEY_COMBO_EXPIRE = "Sword.ComboExpire";

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec == null || spec.Definition == null) yield break;

            var data = spec.Definition.sourceObject as SwordCombo2DData;
            if (data == null)
            {
                Debug.LogError("[SwordCombo2D] AbilityDefinition.sourceObject must be SwordCombo2DData.");
                yield break;
            }

            int comboIndex = ResolveComboIndex(spec, data.comboResetTime);
            spec.SetInt(KEY_COMBO_INDEX, comboIndex);
            spec.SetFloat(KEY_COMBO_EXPIRE, Time.time + data.comboResetTime);

            Vector2 dir = ResolveAimDirection(system);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();

            TryPlayAnim(system, data, comboIndex, spec.Definition);

            yield return Lunge(system, dir,
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

            DoHit(system, spec, data, comboIndex, dir);
        }

        private int ResolveComboIndex(AbilitySpec spec, float resetTime)
        {
            float expire = spec.GetFloat(KEY_COMBO_EXPIRE, -1f);
            int current = spec.GetInt(KEY_COMBO_INDEX, -1);

            if (expire > 0f && Time.time <= expire && current >= 0)
                return (current + 1) % 3;

            return 0;
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

        private void TryPlayAnim(AbilitySystem system, SwordCombo2DData data, int comboIndex, AbilityDefinition definition)
        {
            string trig = GetArraySafe(data.animTriggers, comboIndex, "");
            if (string.IsNullOrEmpty(trig)) return;
            system.TryPlayAnimationTriggerHash(Animator.StringToHash(trig), definition);
        }

        private IEnumerator Lunge(AbilitySystem system, Vector2 dir, float distance, float duration)
        {
            if (distance <= 0f || duration <= 0f) yield break;

            var rb = system.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                Vector3 start = system.transform.position;
                Vector3 end = start + (Vector3)(dir * distance);
                float t = 0f;
                while (t < duration)
                {
                    if (system.CurrentExecSpec?.Token != null && system.CurrentExecSpec.Token.IsCancelled) yield break;
                    t += Time.deltaTime;
                    float a = Mathf.Clamp01(t / duration);
                    system.transform.position = Vector3.Lerp(start, end, a);
                    yield return null;
                }
                yield break;
            }

            Vector2 startPos = rb.position;
            Vector2 endPos = startPos + dir * distance;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (system.CurrentExecSpec?.Token != null && system.CurrentExecSpec.Token.IsCancelled) yield break;
                elapsed += Time.fixedDeltaTime;
                float a = Mathf.Clamp01(elapsed / duration);
                rb.MovePosition(Vector2.Lerp(startPos, endPos, a));
                yield return new WaitForFixedUpdate();
            }
        }

        private void DoHit(AbilitySystem system, AbilitySpec abilitySpec, SwordCombo2DData data, int comboIndex, Vector2 dir)
        {
            if (data.damageEffect == null) return;

            Vector2 perp = new Vector2(-dir.y, dir.x);
            int sideSign = GetArraySafe(data.sideSigns, comboIndex, 0);

            Vector2 center = (Vector2)system.transform.position
                             + dir * data.forwardOffset
                             + perp * (data.sideOffset * sideSign);
#if UNITY_EDITOR
            if (system.TryGetComponent<UnityGAS.Sample.RealtimeHitboxGizmo2D>(out var gizmo))
            {
                // 콤보별 색 (원하면 바꿔도 됨)
                var col = (comboIndex == 0) ? Color.green : (comboIndex == 1 ? Color.yellow : Color.cyan);

                // DoHit은 OverlapBox 각도 0f를 쓰고 있으니 angleDeg=0
                gizmo.RecordBox(center, data.hitboxSize, 0f, 0.15f, col);
            }
#endif
            var td = AbilityTargetData2D.FromOverlapBox(center, data.hitboxSize, 0f, data.hitLayers, ignore: system.gameObject);
            if (td.Targets.Count == 0) return;

            float baseHp = GetArraySafe(data.damages, comboIndex, 0f);
            if (baseHp <= 0f) return;
            float finalHp = baseHp;
            float baseStagger = GetArraySafe(data.staggerDamages, comboIndex, 0f);
            var stats = system.DamageProfile != null ? system.DamageProfile.formulaStats : null;

            if (stats != null)
            {
                var attackerAttr = system.AttributeSet;
                var result = DamageFormulaUtil.Compute(
                    attackerAttr,
                    stats,                      // ✅ data.formulaStats → stats
                    DamageAttackKind.Normal,    // ✅ Skill → Normal (콤보는 일반공격)
                    baseHpDamage: baseHp,
                    baseStaggerDamage: baseStagger,
                    includeElement: data.includeElementDamage,
                    includeStagger: data.includeStaggerDamage
                );
                finalHp = result.hpDamage;
            }

            if (finalHp <= 0f) return;

            var runner = system.EffectRunner;
            if (runner == null) return;

            GameplayTag damageKey = null;
            if (data.damageEffect is GE_Damage_Spec geDmg)
                damageKey = geDmg.damageKey;

            for (int i = 0; i < td.Targets.Count; i++)
            {
                var target = td.Targets[i];
                if (target == null) continue;

                var geSpec = system.MakeSpec(data.damageEffect, causer: system.gameObject, sourceObject: abilitySpec.Definition);
                if (damageKey != null) geSpec.SetSetByCallerMagnitude(damageKey, finalHp);

                // legacy : runner.ApplyEffectSpec(geSpec, target);

                CombatDamageAction.ApplyDamageAndEmitHit(
                    system, abilitySpec,
                    data.damageEffect,
                    target,
                    finalHp,
                    data.hitConfirmedTag,     // 새로 만든 태그
                    system.gameObject         // causer
                );

            }
        }

        private static T GetArraySafe<T>(T[] arr, int index, T fallback)
        {
            if (arr == null || arr.Length == 0) return fallback;
            index = Mathf.Clamp(index, 0, arr.Length - 1);
            return arr[index];
        }
    }
}

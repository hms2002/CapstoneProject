using System.Collections;
using UnityEngine;

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

            // 1) 콤보 인덱스 결정(런타임 상태는 Spec에)
            int comboIndex = ResolveComboIndex(spec, data.comboResetTime);
            spec.SetInt(KEY_COMBO_INDEX, comboIndex);
            spec.SetFloat(KEY_COMBO_EXPIRE, Time.time + data.comboResetTime);

            // 2) 에임 방향(플레이어 -> 마우스)
            Vector2 dir = ResolveAimDirection(system);
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;
            dir.Normalize();

            // 3) 애니 트리거
            TryPlayAnim(system, data, comboIndex, spec.Definition);

            // 4) 전진(Lunge)
            yield return Lunge(system, dir,
                GetArraySafe(data.lungeDistances, comboIndex, 0f),
                GetArraySafe(data.lungeDurations, comboIndex, 0f));

            // 5) 히트 이벤트 대기(애니 이벤트 동기화)
            if (data.hitEventTag != null)
            {
                yield return AbilityTasks.WaitGameplayEvent(
                    system, spec, data.hitEventTag,
                    onReceived: _ => { },
                    timeout: data.hitEventTimeout,
                    predicate: d => d.Spec == spec
                );
            }

            // 6) 타격 판정 + 데미지 적용
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
            // 네 샘플 입력 스크립트가 AimDirection을 제공한다는 전제(없으면 카메라 폴백)
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
            Debug.DrawLine(center, center+data.hitboxSize, Color.red, 4f);
            Debug.Log(center);
            var td = AbilityTargetData2D.FromOverlapBox(center, data.hitboxSize, 0f, data.hitLayers, ignore: system.gameObject);
            if (td.Targets.Count == 0) return;

            float dmg = GetArraySafe(data.damages, comboIndex, 0f);
            if (dmg <= 0f) return;

            var runner = system.GetComponent<GameplayEffectRunner>();
            if (runner == null) return;

            // SetByCaller 키는 GE_Damage_Spec에서 가져오는 편이 실수 방지에 좋음
            GameplayTag damageKey = null;
            if (data.damageEffect is GE_Damage_Spec geDmg)
                damageKey = geDmg.damageKey;

            for (int i = 0; i < td.Targets.Count; i++)
            {
                var target = td.Targets[i];
                if (target == null) continue;

                var geSpec = system.MakeSpec(data.damageEffect, causer: system.gameObject, sourceObject: abilitySpec.Definition);
                if (damageKey != null) geSpec.SetSetByCallerMagnitude(damageKey, dmg);

                runner.ApplyEffectSpec(geSpec, target);
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

using System.Collections;
using UnityEngine;

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

            // 애니 이벤트(무기 애니메이션)에 맞춰 타격 시점 동기화
            if (data.hitEventTag != null)
            {
                yield return AbilityTasks.WaitGameplayEvent(
                    system, spec, data.hitEventTag,
                    onReceived: _ => { },
                    timeout: data.hitEventTimeout,
                    predicate: d => d.Spec == spec
                );
            }

            // 후딜 개별 조정
            spec.SetFloat("RecoveryOverride", data.recoveryOverride);

            // OverlapBox로 4x4 타격
            Vector2 center = (Vector2)system.transform.position + dir * data.forwardOffset;
            var td = AbilityTargetData2D.FromOverlapBox(center, data.hitboxSize, 0f, data.hitLayers, ignore: system.gameObject);
            if (td.Targets.Count == 0) yield break;

            var runner = system.GetComponent<GameplayEffectRunner>();
            if (runner == null) yield break;

            GameplayTag damageKey = null;
            if (data.damageEffect is GE_Damage_Spec ge) damageKey = ge.damageKey;

            for (int i = 0; i < td.Targets.Count; i++)
            {
                var target = td.Targets[i];
                if (target == null) continue;

                var geSpec = system.MakeSpec(data.damageEffect, causer: system.gameObject, sourceObject: def);
                if (damageKey != null) geSpec.SetSetByCallerMagnitude(damageKey, data.damage);

                runner.ApplyEffectSpec(geSpec, target);
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

using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 파편검 Skill1 회수 피해 payload를 만들고 현재 장착 무기 runtime state에 회수 실행을 요청한다.
    /// - detached 조각이 없을 때의 실패/쿨다운 미소모 판단은 selection strategy가 먼저 처리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_FragmentBladeRecall", menuName = "GAS/Weapon/Fragment Blade/Recall Logic")]
    public sealed class AbilityLogic_FragmentBladeRecall : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null || spec?.Definition == null || system.AttributeSet == null)
                yield break;

            FragmentBladeRecallData data = spec.Definition.sourceObject as FragmentBladeRecallData;
            if (data == null)
                yield break;

            FragmentBladeRuntimeState runtimeState = ResolveRuntimeState(system);
            if (runtimeState == null || runtimeState.BoundData == null || runtimeState.BoundData.DetachedShardCount <= 0)
                yield break;

            CombatHitPayload payload = FragmentBladeDamageUtility.BuildPayload(
                system,
                spec,
                data.DamageConfig,
                data.damageEffect,
                data.knockbackEffect,
                data.damageFormula,
                data.knockbackFormula,
                data.legacyDamage,
                data.legacyStaggerDamage,
                1f);

            runtimeState.BeginRecallFromAbility(payload, data.hitLayers, system.gameObject);
        }

        private static FragmentBladeRuntimeState ResolveRuntimeState(AbilitySystem system)
        {
            WeaponEquipController equipController = system != null
                ? system.GetComponentInChildren<WeaponEquipController>(true)
                : null;

            return equipController != null
                ? equipController.GetCurrentWeaponRuntimeState() as FragmentBladeRuntimeState
                : null;
        }
    }
}

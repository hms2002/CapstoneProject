using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 번개 창 Q AD를 현재 장착된 RuntimeState의 표식 돌진 또는 sweep 실행으로 연결할 책임을 가집니다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_LightningSpear_Skill1", menuName = "GAS/Weapon/Lightning Spear/Logic Skill1")]
    public sealed class AbilityLogic_LightningSpearSkill1 : AbilityLogic
    {
        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (!TryResolve(system, out LightningSpearRuntimeState runtimeState, out LightningSpearLoadout loadout))
                yield break;

            LightningSpearSkill1Data data = spec?.Definition?.sourceObject as LightningSpearSkill1Data;
            yield return runtimeState.ExecuteSkill1(system, spec, loadout, data);
        }

        private static bool TryResolve(
            AbilitySystem system,
            out LightningSpearRuntimeState runtimeState,
            out LightningSpearLoadout loadout)
        {
            runtimeState = null;
            loadout = null;

            if (system == null)
                return false;

            WeaponEquipController equipController = system.GetComponentInChildren<WeaponEquipController>(true);
            runtimeState = equipController != null
                ? equipController.GetCurrentWeaponRuntimeState() as LightningSpearRuntimeState
                : null;

            WeaponInventory2D inventory = system.GetComponent<WeaponInventory2D>();
            WeaponDefinition activeWeapon = inventory != null ? inventory.ActiveWeapon : null;
            loadout = activeWeapon != null ? activeWeapon.abilityLoadout as LightningSpearLoadout : null;

            return runtimeState != null && loadout != null;
        }
    }
}

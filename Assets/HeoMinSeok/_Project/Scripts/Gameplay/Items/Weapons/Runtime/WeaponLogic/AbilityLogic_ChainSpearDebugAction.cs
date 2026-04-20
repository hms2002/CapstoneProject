using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    /// <summary>
    /// 책임 :
    /// - 사슬창 1차 구조 검증용으로 던지기, 당기기, 회수 액션이 어떤 AD로 실행됐는지 로그로 남긴다.
    /// - 실제 투척 판정보다 먼저 연결 상태가 슬롯 분기와 소비 흐름에 맞게 바뀌는지 확인하게 돕는다.
    /// </summary>
    [CreateAssetMenu(fileName = "AL_ChainSpearDebugAction", menuName = "GAS/Weapon/Chain Spear/Debug Action")]
    public sealed class AbilityLogic_ChainSpearDebugAction : AbilityLogic
    {
        [SerializeField] private string debugLabel = "Chain Spear Action";
        [SerializeField] private bool startThrowExecutor;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            string ownerName = system != null ? system.gameObject.name : "<null>";
            string abilityName = spec?.Definition != null ? spec.Definition.abilityName : "<null>";
            Debug.Log($"[ChainSpear] {debugLabel} activated by {ownerName} via {abilityName}.", system);

            if (startThrowExecutor)
                TryStartThrowExecutor(system, spec, initialTarget);

            yield break;
        }

        /// <summary>
        /// 책임 :
        /// - 사슬창 던지기 AD가 성공했을 때 현재 장착 무기 프리팹의 ChainSpearThrowExecutor를 runner로 시작한다.
        /// - 이 로직은 executor 시작 진입점만 맡고, 이후 대기/이벤트/cleanup 운영은 executor와 runner에 맡긴다.
        /// </summary>
        private static void TryStartThrowExecutor(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            if (system == null)
                return;

            WeaponExecutorRunner runner = system.GetComponent<WeaponExecutorRunner>();
            WeaponInventory2D inventory = system.GetComponent<WeaponInventory2D>();
            WeaponEquipController equipController = system.GetComponentInChildren<WeaponEquipController>(true);
            WeaponDefinition activeWeapon = inventory != null ? inventory.ActiveWeapon : null;
            WeaponAbilityRuntimeState runtimeState = equipController != null
                ? equipController.GetCurrentWeaponRuntimeState()
                : null;
            ChainSpearThrowExecutor executor = equipController != null
                ? equipController.GetComponentInChildren<ChainSpearThrowExecutor>(true)
                : null;

            if (runner == null || activeWeapon == null || runtimeState == null || executor == null)
                return;

            WeaponAbilityExecutionContext context = new()
            {
                AbilitySystem = system,
                Weapon = activeWeapon,
                Loadout = activeWeapon.abilityLoadout,
                RuntimeState = runtimeState,
                Ability = spec?.Definition,
                Spec = spec,
                InitialTarget = initialTarget,
                Owner = system.gameObject,
                WeaponTransform = equipController.transform,
                EquipController = equipController
            };

            runner.StartExecutor(executor, context);
        }
    }
}

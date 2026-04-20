using UnityGAS;

/// <summary>
/// 책임 :
/// - 현재 장착 무기와 무기 런타임 상태를 함께 보고 입력 슬롯이 실행할 AbilityDefinition을 결정한다.
/// - 무기 상태가 별도 선택을 제안하지 않으면 WeaponDefinition의 기본 attack/skill1/skill2를 fallback으로 사용한다.
/// - 입력 계층이 WeaponInventory와 WeaponAbilityRuntimeState의 세부를 직접 섞지 않도록 선택 책임을 한 곳에 모은다.
/// </summary>
public sealed class WeaponAbilitySelector
{
    private readonly WeaponInventory2D weaponInventory;
    private readonly IWeaponRuntimeStateProvider runtimeStateProvider;

    public WeaponAbilitySelector(
        WeaponInventory2D weaponInventory,
        IWeaponRuntimeStateProvider runtimeStateProvider)
    {
        this.weaponInventory = weaponInventory;
        this.runtimeStateProvider = runtimeStateProvider;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 활성 무기를 기준으로 입력 슬롯의 실행 ability를 확정한다.
    /// - 무기 상태 override가 없으면 기본 슬롯 ability를 그대로 반환한다.
    /// </summary>
    public AbilityDefinition ResolveAbility(WeaponAbilitySlot slot)
    {
        int activeSlotIndex = weaponInventory != null
            ? weaponInventory.ActiveIndex
            : -1;
        WeaponDefinition activeWeapon = weaponInventory != null
            ? weaponInventory.ActiveWeapon
            : null;
        WeaponRuntimeData runtimeData = weaponInventory != null
            ? weaponInventory.ActiveRuntimeData
            : null;
        int otherSlotIndex = weaponInventory != null
            ? weaponInventory.GetOtherSlotIndex(activeSlotIndex)
            : -1;
        WeaponDefinition otherWeapon = weaponInventory != null
            ? weaponInventory.GetWeaponInSlot(otherSlotIndex)
            : null;
        WeaponRuntimeData otherRuntimeData = weaponInventory != null
            ? weaponInventory.GetRuntimeDataInSlot(otherSlotIndex)
            : null;
        WeaponAbilityRuntimeState runtimeState = runtimeStateProvider != null
            ? runtimeStateProvider.GetCurrentWeaponRuntimeState()
            : null;

        if (activeWeapon == null)
            return null;

        WeaponAbilityLoadout loadout = activeWeapon.abilityLoadout;
        if (loadout != null)
        {
            var context = new WeaponSelectionContext(
                activeWeapon,
                activeSlotIndex,
                slot,
                runtimeState,
                runtimeData,
                otherWeapon,
                otherSlotIndex,
                otherRuntimeData);

            if (loadout.SelectionStrategy != null &&
                loadout.SelectionStrategy.TrySelectAbility(context, loadout, out AbilityDefinition strategyAbility) &&
                     strategyAbility != null)
            {
                return strategyAbility;
            }

            AbilityDefinition loadoutDefault = loadout.GetDefaultAbility(slot);
            if (loadoutDefault != null)
                return loadoutDefault;
        }

        if (runtimeState != null &&
            runtimeState.TrySelectAbility(activeWeapon, slot, out AbilityDefinition selectedAbility) &&
            selectedAbility != null)
        {
            return selectedAbility;
        }

        return activeWeapon.GetAbility(slot);
    }
}

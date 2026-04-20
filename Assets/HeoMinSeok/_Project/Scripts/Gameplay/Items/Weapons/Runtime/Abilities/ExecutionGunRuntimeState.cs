using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 처형총 장착 중 프리팹 live adapter로서 현재 슬롯 ExecutionGunRuntimeData와 무기 상호작용 계층을 연결한다.
/// - 표식 소비 사격 같은 교차 무기 결과는 pair rule에 사실만 전달하고, 직접 반대 슬롯 data를 수정하지 않는다.
/// </summary>
public sealed class ExecutionGunRuntimeState : WeaponAbilityRuntimeState
{
    private WeaponInventory2D weaponInventory;
    private IWeaponInteractionLayer interactionLayer;

    public ExecutionGunRuntimeData BoundData => GetBoundData();

    private void Awake()
    {
        CacheInventory();
    }

    public override void HandleEquippedWeaponChanged(WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
        CacheInventory();
    }

    public override void HandleAbilityActivated(
        WeaponDefinition weapon,
        WeaponAbilitySlot slot,
        AbilityDefinition activatedAbility)
    {
        if (weapon == null || activatedAbility == null)
            return;

        if (weapon.abilityLoadout is not ExecutionGunLoadout loadout)
            return;

        if (activatedAbility != loadout.ExecutionShot)
            return;

        ExecutionGunRuntimeData gunData = GetBoundData();
        if (gunData == null)
            return;

        BuildInteractionLayer()?.NotifyAbilityActivated(new WeaponInteractionContext(
            weaponInventory,
            weapon,
            weaponInventory != null ? weaponInventory.ActiveIndex : -1,
            slot,
            this,
            gunData,
            activatedAbility,
            weaponInventory != null ? weaponInventory.GetOtherWeaponInSlot(weaponInventory.ActiveIndex) : null,
            weaponInventory != null ? weaponInventory.GetOtherSlotIndex(weaponInventory.ActiveIndex) : -1,
            weaponInventory != null ? weaponInventory.GetOtherRuntimeData(weaponInventory.ActiveIndex) : null));
    }

    private void CacheInventory()
    {
        if (weaponInventory == null)
            weaponInventory = GetComponentInParent<WeaponInventory2D>();

        if (interactionLayer == null && weaponInventory != null)
            interactionLayer = weaponInventory.InteractionLayer;
    }

    private ExecutionGunRuntimeData GetBoundData()
    {
        CacheInventory();
        return weaponInventory != null
            ? weaponInventory.ActiveRuntimeData as ExecutionGunRuntimeData
            : null;
    }

    private IWeaponInteractionLayer BuildInteractionLayer()
    {
        CacheInventory();
        return interactionLayer;
    }
}

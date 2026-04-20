using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 인벤토리가 소유한 EclipseSwordRuntimeData를 장착 중 프리팹 live component에 연결하는 thin adapter 역할을 한다.
/// - live behavior와 기존 runtime state 계약은 유지하되, 실제 상태 소유권은 슬롯 data 쪽에 두도록 전환 지점을 제공한다.
/// </summary>
public sealed class EclipseSwordRuntimeState : WeaponAbilityRuntimeState
{
    private WeaponInventory2D weaponInventory;

    public EclipseSwordRuntimeData BoundData => GetBoundData();
    public bool IsInEclipseStance => BoundData != null && BoundData.IsInEclipseStance;
    public int NextStanceAttackIndex => BoundData != null ? BoundData.NextStanceAttackIndex : 0;
    public int CurrentStanceAttackCount => BoundData != null ? BoundData.CurrentStanceAttackCount : 0;
    public bool CanUseBloomFinish => BoundData != null && BoundData.CanUseBloomFinish;

    private void Awake()
    {
        CacheInventory();
    }

    public override void HandleEquippedWeaponChanged(WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
        CacheInventory();
    }

    public override bool TrySelectAbility(
        WeaponDefinition weapon,
        WeaponAbilitySlot slot,
        out AbilityDefinition ability)
    {
        ability = null;
        return false;
    }

    public override void HandleAbilityActivated(
        WeaponDefinition weapon,
        WeaponAbilitySlot slot,
        AbilityDefinition activatedAbility)
    {
        if (weapon == null || activatedAbility == null)
            return;

        if (weapon.abilityLoadout is not EclipseSwordLoadout loadout)
            return;

        EclipseSwordRuntimeData data = GetBoundData();
        if (data == null)
            return;

        if (activatedAbility == loadout.EnterStance)
        {
            data.EnterStance();
            return;
        }

        if (activatedAbility == loadout.BloomFinish)
        {
            data.ExitStance();
            return;
        }

        if (activatedAbility == loadout.ExitStance)
        {
            data.ExitStance();
            return;
        }

        if (!data.IsInEclipseStance || slot != WeaponAbilitySlot.Attack)
            return;

        if (!data.AlternateStanceAttacks)
            return;

        if (activatedAbility == loadout.StanceAttackA)
        {
            data.AdvanceStanceAttackState(nextAttackIndex: 1);
            return;
        }

        if (activatedAbility == loadout.StanceAttackB)
        {
            data.AdvanceStanceAttackState(nextAttackIndex: 0);
        }
    }

    private void CacheInventory()
    {
        if (weaponInventory == null)
            weaponInventory = GetComponentInParent<WeaponInventory2D>();
    }

    private EclipseSwordRuntimeData GetBoundData()
    {
        CacheInventory();
        return weaponInventory != null
            ? weaponInventory.ActiveRuntimeData as EclipseSwordRuntimeData
            : null;
    }
}

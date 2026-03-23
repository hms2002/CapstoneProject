using System.Collections.Generic;
using UnityGAS;

/// <summary>
/// 책임 : 무기 인벤토리가 보유한 AbilityDefinition의 소유 참조 수를 관리한다.
/// 일반 장착/해제에서는 실제 AbilitySystem grant/take를 수행하고,
/// 복원 경로에서는 ref count만 재구성하여 이후 장착/해제 흐름이 정상 동작하도록 돕는다.
/// </summary>
public sealed class WeaponAbilityOwnershipBinder
{
    private readonly AbilitySystem abilitySystem;
    private readonly Dictionary<AbilityDefinition, int> refCounts = new();

    public WeaponAbilityOwnershipBinder(AbilitySystem abilitySystem)
    {
        this.abilitySystem = abilitySystem;
    }

    public void OnWeaponAdded(WeaponDefinition weapon)
    {
        if (weapon == null) return;

        GiveRef(weapon.attack);
        GiveRef(weapon.skill1);
        GiveRef(weapon.skill2);
    }

    public void OnWeaponRemoved(WeaponDefinition weapon)
    {
        if (weapon == null) return;

        TakeRef(weapon.attack);
        TakeRef(weapon.skill1);
        TakeRef(weapon.skill2);
    }

    /// <summary>
    /// 책임 : 복원 직후 현재 슬롯에 들어 있는 무기들을 기준으로 ref count만 다시 계산한다.
    /// AbilitySystem에는 아무 것도 부여하지 않으며, 이후 Remove 시 count가 맞게 동작하도록 내부 상태만 맞춘다.
    /// </summary>
    public void RebuildReferencesWithoutApplying(IEnumerable<WeaponDefinition> weapons)
    {
        refCounts.Clear();

        if (weapons == null)
            return;

        foreach (var weapon in weapons)
        {
            if (weapon == null) continue;

            AddRefOnly(weapon.attack);
            AddRefOnly(weapon.skill1);
            AddRefOnly(weapon.skill2);
        }
    }

    /// <summary>
    /// 책임 : 복원/초기화 시 내부 ref count만 비운다.
    /// AbilitySystem에는 영향을 주지 않는다.
    /// </summary>
    public void ClearReferencesWithoutRemoving()
    {
        refCounts.Clear();
    }

    private void AddRefOnly(AbilityDefinition def)
    {
        if (def == null) return;

        refCounts.TryGetValue(def, out int count);
        refCounts[def] = count + 1;
    }

    private void GiveRef(AbilityDefinition def)
    {
        if (def == null || abilitySystem == null) return;

        refCounts.TryGetValue(def, out int count);
        refCounts[def] = count + 1;

        if (count == 0)
            abilitySystem.GiveAbility(def);
    }

    private void TakeRef(AbilityDefinition def)
    {
        if (def == null || abilitySystem == null) return;
        if (!refCounts.TryGetValue(def, out int count)) return;

        count--;
        if (count <= 0)
        {
            refCounts.Remove(def);
            abilitySystem.TakeAbility(def);
        }
        else
        {
            refCounts[def] = count;
        }
    }
}
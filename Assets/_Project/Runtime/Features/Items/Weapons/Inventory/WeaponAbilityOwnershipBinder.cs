using System.Collections.Generic;
using UnityGAS;

/// <summary>
/// 책임 : 무기 인벤토리가 보유한 AbilityDefinition의 소유 참조 수를 관리한다.
/// 일반 장착/해제에서는 실제 AbilitySystem grant/take를 수행하고,
/// 복원 경로에서는 ref count를 재구성하면서 필요한 ability grant를 보장한다.
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

        foreach (AbilityDefinition ability in weapon.EnumerateGrantedAbilities())
            GiveRef(ability);
    }

    public void OnWeaponRemoved(WeaponDefinition weapon)
    {
        if (weapon == null) return;

        foreach (AbilityDefinition ability in weapon.EnumerateGrantedAbilities())
            TakeRef(ability);
    }

    /// <summary>
    /// 책임 : 복원 직후 현재 슬롯 무기들을 기준으로 ref count를 다시 계산하고,
    /// 해당 무기 ability가 AbilitySystem에 없으면 grant까지 보장한다.
    /// 이후 런타임 복원 단계가 cooldown, charges, stack, custom vars를 덮어쓴다.
    /// </summary>
    public void RebuildReferencesAndEnsureGranted(IEnumerable<WeaponDefinition> weapons)
    {
        refCounts.Clear();

        if (weapons == null)
            return;

        foreach (var weapon in weapons)
        {
            if (weapon == null) continue;

            foreach (AbilityDefinition ability in weapon.EnumerateGrantedAbilities())
                AddRefAndEnsureGranted(ability);
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

    /// <summary>
    /// 책임 : 복원 경로에서 ref count를 올리고, 아직 없는 ability는 기본 spec를 생성한다.
    /// persistent state 복원 이전의 최소 보장 단계다.
    /// </summary>
    private void AddRefAndEnsureGranted(AbilityDefinition def)
    {
        if (def == null) return;

        refCounts.TryGetValue(def, out int count);
        refCounts[def] = count + 1;

        if (count == 0 && abilitySystem != null)
            abilitySystem.GiveAbility(def);
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

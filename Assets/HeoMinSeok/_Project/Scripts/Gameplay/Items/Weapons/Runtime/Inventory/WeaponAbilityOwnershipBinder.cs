using System.Collections.Generic;
using UnityGAS;

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
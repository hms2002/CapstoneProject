using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "WAS_LightningSpear", menuName = "Game/Weapon Ability Strategy/Lightning Spear")]
public sealed class LightningSpearSelectionStrategy : WeaponAbilitySelectionStrategy
{
    public override Type ExpectedLoadoutType => typeof(LightningSpearLoadout);
    public override Type ExpectedRuntimeStateType => typeof(LightningSpearRuntimeState);

    public override bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability)
    {
        ability = null;

        if (loadout is not LightningSpearLoadout lightningLoadout)
            return false;

        ability = context.Slot switch
        {
            WeaponAbilitySlot.Attack => lightningLoadout.BaseAttack,
            WeaponAbilitySlot.Skill1 => lightningLoadout.MarkRushOrSweep,
            WeaponAbilitySlot.Skill2 => lightningLoadout.MarkRain,
            _ => null
        };

        return ability != null;
    }
}

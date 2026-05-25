using System;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "WAS_Flowering", menuName = "Game/Weapon Ability Strategy/Flowering")]
public sealed class FloweringSelectionStrategy : WeaponAbilitySelectionStrategy
{
    public override Type ExpectedLoadoutType => typeof(FloweringLoadout);

    public override bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability)
    {
        ability = null;

        if (loadout is not FloweringLoadout floweringLoadout)
            return false;

        switch (context.Slot)
        {
            case WeaponAbilitySlot.Attack:
                bool bloomActive = context.RuntimeData is FloweringRuntimeData floweringData &&
                                   floweringData.IsBloomActive;
                ability = bloomActive ? floweringLoadout.BloomAttack : floweringLoadout.BaseAttack;
                return ability != null;

            case WeaponAbilitySlot.Skill1:
                ability = floweringLoadout.BloomSkill;
                return ability != null;

            case WeaponAbilitySlot.Skill2:
                return false;

            default:
                return false;
        }
    }
}

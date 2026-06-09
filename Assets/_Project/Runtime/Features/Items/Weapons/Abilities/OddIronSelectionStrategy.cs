using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 기묘한 쇳덩이 입력 슬롯을 잔탄 상태에 따라 사격/빈총/투척/전탄난사 AD로 해석한다.
/// - 잔탄이 없을 때도 입력 피드백이 나오도록 Attack과 Skill2를 dry-fire AD로 바꾼다.
/// </summary>
[CreateAssetMenu(fileName = "WAS_OddIron", menuName = "Game/Weapon Ability Strategy/Odd Iron")]
public sealed class OddIronSelectionStrategy : WeaponAbilitySelectionStrategy
{
    public override Type ExpectedLoadoutType => typeof(OddIronLoadout);
    public override Type ExpectedRuntimeStateType => null;

    public override bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability)
    {
        ability = null;

        if (loadout is not OddIronLoadout oddIronLoadout)
            return false;

        OddIronRuntimeData oddIronData = context.RuntimeData as OddIronRuntimeData;
        bool hasAmmo = oddIronData == null || oddIronData.HasAmmo;

        switch (context.Slot)
        {
            case WeaponAbilitySlot.Attack:
                ability = hasAmmo ? oddIronLoadout.Shot : oddIronLoadout.DryFire;
                return ability != null;

            case WeaponAbilitySlot.Skill1:
                ability = oddIronLoadout.ThrowAndBreak;
                return ability != null;

            case WeaponAbilitySlot.Skill2:
                ability = hasAmmo ? oddIronLoadout.Barrage : oddIronLoadout.DryFire;
                return ability != null;

            default:
                return false;
        }
    }
}

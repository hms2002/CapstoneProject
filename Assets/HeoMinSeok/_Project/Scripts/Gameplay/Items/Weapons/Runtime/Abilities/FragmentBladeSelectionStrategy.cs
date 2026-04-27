using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 파편검 입력 슬롯을 loadout의 attack/recall/bind-enhance AD로 해석한다.
/// - Skill1은 회수할 detached 조각이 없으면 선택 실패로 처리해 쿨다운이 소모되지 않게 한다.
/// </summary>
[CreateAssetMenu(fileName = "WAS_FragmentBlade", menuName = "Game/Weapon Ability Strategy/Fragment Blade")]
public sealed class FragmentBladeSelectionStrategy : WeaponAbilitySelectionStrategy
{
    public override Type ExpectedLoadoutType => typeof(FragmentBladeLoadout);
    public override Type ExpectedRuntimeStateType => typeof(FragmentBladeRuntimeState);

    public override bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability)
    {
        ability = null;

        if (loadout is not FragmentBladeLoadout fragmentLoadout)
            return false;

        FragmentBladeRuntimeData fragmentData = context.RuntimeData as FragmentBladeRuntimeData;

        switch (context.Slot)
        {
            case WeaponAbilitySlot.Attack:
                ability = fragmentLoadout.BaseAttack;
                return ability != null;

            case WeaponAbilitySlot.Skill1:
                if (fragmentData == null || fragmentData.DetachedShardCount <= 0)
                    return false;

                ability = fragmentLoadout.RecallSkill;
                return ability != null;

            case WeaponAbilitySlot.Skill2:
                ability = fragmentLoadout.BindEnhanceSkill;
                return ability != null;

            default:
                return false;
        }
    }
}

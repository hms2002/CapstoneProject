using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 월영도의 공격/스킬 슬롯을 태양도 열기 상태와 자신의 냉기 상태를 함께 읽어 일반 액션 또는 공명 액션으로 해석한다.
/// - 비활성 슬롯에 있는 태양도의 persistent runtime data를 읽어도 자연스럽게 선택이 바뀌는 실제 쌍무기 전략 사례를 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "WAS_MoonBlade", menuName = "Game/Weapon Ability Strategy/Moon Blade")]
public sealed class MoonBladeSelectionStrategy : WeaponAbilitySelectionStrategy
{
    public override Type ExpectedLoadoutType => typeof(MoonBladeLoadout);

    public override Type ExpectedRuntimeStateType => typeof(MoonBladeRuntimeState);

    public override bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability)
    {
        ability = null;

        if (loadout is not MoonBladeLoadout moonLoadout)
            return false;

        MoonBladeRuntimeData moonData = context.RuntimeData as MoonBladeRuntimeData;
        SunBladeRuntimeData sunData = context.OtherRuntimeData as SunBladeRuntimeData;

        switch (context.Slot)
        {
            case WeaponAbilitySlot.Attack:
                ability = sunData != null && sunData.HeatStacks >= moonLoadout.RequiredSunHeatForFrostedAttack
                    ? moonLoadout.FrostedAttack
                    : moonLoadout.BaseAttack;
                return ability != null;

            case WeaponAbilitySlot.Skill1:
                ability = moonData != null &&
                          sunData != null &&
                          moonData.ColdStacks >= moonLoadout.RequiredColdForLunarFinish &&
                          sunData.HeatStacks >= moonLoadout.RequiredSunHeatForLunarFinish
                    ? moonLoadout.LunarFinishStarter
                    : moonLoadout.DefaultSkill1;
                return ability != null;

            default:
                return false;
        }
    }
}

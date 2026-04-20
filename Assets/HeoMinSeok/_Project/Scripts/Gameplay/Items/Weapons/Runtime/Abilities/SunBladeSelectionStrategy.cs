using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 태양도의 공격/스킬 슬롯을 월영도 냉기 상태와 자신의 열기 상태를 함께 읽어 일반 액션 또는 공명 액션으로 해석한다.
/// - 비활성 슬롯에 있는 월영도의 persistent runtime data를 읽어도 자연스럽게 선택이 바뀌는 실제 쌍무기 전략 사례를 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "WAS_SunBlade", menuName = "Game/Weapon Ability Strategy/Sun Blade")]
public sealed class SunBladeSelectionStrategy : WeaponAbilitySelectionStrategy
{
    public override Type ExpectedLoadoutType => typeof(SunBladeLoadout);

    public override Type ExpectedRuntimeStateType => typeof(SunBladeRuntimeState);

    public override bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability)
    {
        ability = null;

        if (loadout is not SunBladeLoadout sunLoadout)
            return false;

        SunBladeRuntimeData sunData = context.RuntimeData as SunBladeRuntimeData;
        MoonBladeRuntimeData moonData = context.OtherRuntimeData as MoonBladeRuntimeData;

        switch (context.Slot)
        {
            case WeaponAbilitySlot.Attack:
                ability = moonData != null && moonData.ColdStacks >= sunLoadout.RequiredMoonColdForHeatedAttack
                    ? sunLoadout.HeatedAttack
                    : sunLoadout.BaseAttack;
                return ability != null;

            case WeaponAbilitySlot.Skill1:
                ability = sunData != null &&
                          moonData != null &&
                          sunData.HeatStacks >= sunLoadout.RequiredHeatForSolarFinish &&
                          moonData.ColdStacks >= sunLoadout.RequiredMoonColdForSolarFinish
                    ? sunLoadout.SolarFinishStarter
                    : sunLoadout.DefaultSkill1;
                return ability != null;

            default:
                return false;
        }
    }
}

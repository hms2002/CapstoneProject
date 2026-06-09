using UnityEngine;
using UnityGAS;
using System;

/// <summary>
/// 책임 :
/// - 월식도 런타임 상태를 읽어 기본 상태와 자세 상태에서 각 입력 슬롯이 어떤 AD를 실행할지 결정한다.
/// - Attack은 기본 공격 또는 자세 A/B로 분기하고, Skill1은 자세 진입/종료 전환 입력으로 해석한다.
/// </summary>
[CreateAssetMenu(fileName = "WAS_EclipseSword", menuName = "Game/Weapon Ability Strategy/Eclipse Sword")]
public sealed class EclipseSwordSelectionStrategy : WeaponAbilitySelectionStrategy
{
    public override Type ExpectedLoadoutType => typeof(EclipseSwordLoadout);

    public override Type ExpectedRuntimeStateType => typeof(EclipseSwordRuntimeState);

    public override bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability)
    {
        ability = null;

        if (loadout is not EclipseSwordLoadout eclipseLoadout)
            return false;

        EclipseSwordRuntimeData eclipseData = context.RuntimeData as EclipseSwordRuntimeData;
        if (eclipseData == null && context.RuntimeState is EclipseSwordRuntimeState eclipseStateAdapter)
            eclipseData = eclipseStateAdapter.BoundData;

        if (eclipseData == null)
            return false;

        switch (context.Slot)
        {
            case WeaponAbilitySlot.Attack:
                ability = eclipseData.IsInEclipseStance
                    ? (eclipseData.NextStanceAttackIndex == 0
                        ? eclipseLoadout.StanceAttackA
                        : eclipseLoadout.StanceAttackB)
                    : eclipseLoadout.BaseAttack;
                return ability != null;

            case WeaponAbilitySlot.Skill1:
                ability = eclipseData.IsInEclipseStance
                    ? (eclipseData.CanUseBloomFinish
                        ? eclipseLoadout.BloomFinish
                        : eclipseLoadout.ExitStance)
                    : eclipseLoadout.EnterStance;
                return ability != null;

            case WeaponAbilitySlot.Skill2:
                ability = eclipseLoadout.Skill2DefaultAbility;
                return ability != null;

            default:
                return false;
        }
    }
}

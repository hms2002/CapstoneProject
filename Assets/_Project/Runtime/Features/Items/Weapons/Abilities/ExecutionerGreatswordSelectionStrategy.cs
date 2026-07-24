using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 대검 처형자의 런타임 상태를 읽어 기본 공격, 처형 준비, 처형 성공/실패 분기를 슬롯별로 선택한다.
/// - Skill1이 기본 상태에서는 준비, 대기 상태에서는 Finish 또는 Fallback으로 해석되게 만드는 전용 선택 규칙을 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "WAS_ExecutionerGreatsword", menuName = "Game/Weapon Ability Strategy/Executioner Greatsword")]
public sealed class ExecutionerGreatswordSelectionStrategy : WeaponAbilitySelectionStrategy
{
    public override Type ExpectedLoadoutType => typeof(ExecutionerGreatswordLoadout);

    public override Type ExpectedRuntimeStateType => typeof(ExecutionerGreatswordRuntimeState);

    public override bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability)
    {
        ability = null;

        if (loadout is not ExecutionerGreatswordLoadout executionerLoadout)
            return false;

        if (context.RuntimeState is not ExecutionerGreatswordRuntimeState executionerState)
            return false;

        switch (context.Slot)
        {
            case WeaponAbilitySlot.Attack:
                ability = executionerLoadout.BaseAttack;
                return ability != null;

            case WeaponAbilitySlot.Skill1:
                if (!executionerState.IsWaitingForExecutionWindow)
                {
                    ability = executionerLoadout.ExecutionReadyAttack;
                    return ability != null;
                }

                ability = executionerState.CanExecute
                    ? executionerLoadout.ExecutionFinish
                    : executionerLoadout.ExecutionFallback;
                return ability != null;

            case WeaponAbilitySlot.Skill2:
                ability = executionerLoadout.Skill2DefaultAbility;
                return ability != null;

            default:
                return false;
        }
    }
}

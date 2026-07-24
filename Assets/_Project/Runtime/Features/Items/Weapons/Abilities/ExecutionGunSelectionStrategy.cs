using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 처형총의 공격 슬롯을 반대 슬롯 검의 표식 스택 수에 따라 일반 사격 또는 표식 소비 사격으로 해석한다.
/// - 비활성 슬롯에 있는 검의 persistent runtime data를 읽어도 무기 선택 구조가 자연스럽게 동작하는지 검증하는 전용 전략이다.
/// </summary>
[CreateAssetMenu(fileName = "WAS_ExecutionGun", menuName = "Game/Weapon Ability Strategy/Execution Gun")]
public sealed class ExecutionGunSelectionStrategy : WeaponAbilitySelectionStrategy
{
    public override Type ExpectedLoadoutType => typeof(ExecutionGunLoadout);

    public override Type ExpectedRuntimeStateType => typeof(ExecutionGunRuntimeState);

    public override bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability)
    {
        ability = null;

        if (loadout is not ExecutionGunLoadout gunLoadout)
            return false;

        MarkSwordRuntimeData swordData = context.OtherRuntimeData as MarkSwordRuntimeData;

        switch (context.Slot)
        {
            case WeaponAbilitySlot.Attack:
                ability = swordData != null && swordData.MarkStacks >= gunLoadout.RequiredMarksForExecutionShot
                    ? gunLoadout.ExecutionShot
                    : gunLoadout.BaseShot;
                return ability != null;

            default:
                return false;
        }
    }
}

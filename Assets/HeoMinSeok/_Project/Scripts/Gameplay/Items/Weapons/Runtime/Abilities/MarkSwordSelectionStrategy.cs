using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 표식검의 기본 공격은 그대로 유지하고, Skill1은 반대 슬롯 총이 연 반격 창 여부에 따라 일반 검격 또는 반격 검격으로 해석한다.
/// - 현재 슬롯 data와 반대 슬롯 data를 함께 읽는 쌍무기 상호참조 선택 규칙의 기준 사례를 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "WAS_MarkSword", menuName = "Game/Weapon Ability Strategy/Mark Sword")]
public sealed class MarkSwordSelectionStrategy : WeaponAbilitySelectionStrategy
{
    public override Type ExpectedLoadoutType => typeof(MarkSwordLoadout);

    public override Type ExpectedRuntimeStateType => typeof(MarkSwordRuntimeState);

    public override bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability)
    {
        ability = null;

        if (loadout is not MarkSwordLoadout markLoadout)
            return false;

        ExecutionGunRuntimeData gunData = context.OtherRuntimeData as ExecutionGunRuntimeData;

        switch (context.Slot)
        {
            case WeaponAbilitySlot.Attack:
                ability = markLoadout.BaseAttack;
                return ability != null;

            case WeaponAbilitySlot.Skill1:
                ability = gunData != null && gunData.ReboundSlashReady
                    ? markLoadout.ReboundSlash
                    : markLoadout.DefaultSkill1;
                return ability != null;

            default:
                return false;
        }
    }
}

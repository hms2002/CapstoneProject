using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 사슬창의 연결 상태를 읽어 Skill1을 던지기/당기기로, Skill2를 회수 또는 비활성으로 해석한다.
/// - Attack은 항상 기본 공격으로 유지하고 관계 상태 소비는 사슬 전용 슬롯에서만 일어나게 만든다.
/// </summary>
[CreateAssetMenu(fileName = "WAS_ChainSpear", menuName = "Game/Weapon Ability Strategy/Chain Spear")]
public sealed class ChainSpearSelectionStrategy : WeaponAbilitySelectionStrategy
{
    public override Type ExpectedLoadoutType => typeof(ChainSpearLoadout);

    public override Type ExpectedRuntimeStateType => typeof(ChainSpearRuntimeState);

    public override Type ExpectedExecutorType => typeof(ChainSpearThrowExecutor);

    public override bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability)
    {
        ability = null;

        if (loadout is not ChainSpearLoadout chainLoadout)
            return false;

        if (context.RuntimeState is not ChainSpearRuntimeState chainState)
            return false;

        switch (context.Slot)
        {
            case WeaponAbilitySlot.Attack:
                ability = chainLoadout.BaseAttack;
                return ability != null;

            case WeaponAbilitySlot.Skill1:
                ability = chainState.HasLinkedTarget
                    ? chainLoadout.ChainPull
                    : chainLoadout.ChainThrow;
                return ability != null;

            case WeaponAbilitySlot.Skill2:
                ability = chainState.HasLinkedTarget
                    ? chainLoadout.ChainRecall
                    : null;
                return ability != null;

            default:
                return false;
        }
    }
}

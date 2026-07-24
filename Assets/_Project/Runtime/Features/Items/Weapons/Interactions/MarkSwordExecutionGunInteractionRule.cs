/// <summary>
/// 책임 :
/// - 표식검과 처형총 사이의 상태 생성, 소비, 역반영 규칙을 하나의 전투 문법으로 해석한다.
/// - runtime state가 반대 슬롯 data를 직접 수정하지 않도록 coordinator를 통해 필요한 슬롯 상태 변경만 요청한다.
/// </summary>
public sealed class MarkSwordExecutionGunInteractionRule : WeaponPairInteractionRule
{
    public override bool SupportsPair(WeaponDefinition sourceWeapon, WeaponDefinition otherWeapon)
    {
        if (sourceWeapon == null || otherWeapon == null)
            return false;

        return (sourceWeapon.abilityLoadout is ExecutionGunLoadout && otherWeapon.abilityLoadout is MarkSwordLoadout)
            || (sourceWeapon.abilityLoadout is MarkSwordLoadout && otherWeapon.abilityLoadout is ExecutionGunLoadout);
    }

    public override bool TryHandleAbilityActivated(
        in WeaponInteractionContext context,
        WeaponRuntimeCoordinator coordinator)
    {
        if (context.SourceWeapon == null || context.ActivatedAbility == null || context.OtherWeapon == null)
            return false;

        if (context.SourceWeapon.abilityLoadout is ExecutionGunLoadout gunLoadout &&
            context.OtherWeapon.abilityLoadout is MarkSwordLoadout &&
            context.ActivatedAbility == gunLoadout.ExecutionShot &&
            context.OtherRuntimeData is MarkSwordRuntimeData swordData)
        {
            int consumedMarks = swordData.MarkStacks;
            if (consumedMarks <= 0)
                return false;

            coordinator.TryMutateRuntimeData<MarkSwordRuntimeData>(context.OtherSlotIndex, static data => data.ClearMarks());
            coordinator.TryMutateRuntimeData<ExecutionGunRuntimeData>(context.SourceSlotIndex, data => data.OpenReboundSlashWindow(consumedMarks));
            return true;
        }

        if (context.SourceWeapon.abilityLoadout is MarkSwordLoadout swordLoadout &&
            context.OtherWeapon.abilityLoadout is ExecutionGunLoadout &&
            context.ActivatedAbility == swordLoadout.ReboundSlash)
        {
            coordinator.TryMutateRuntimeData<ExecutionGunRuntimeData>(context.OtherSlotIndex, static data => data.CloseReboundSlashWindow());
            return true;
        }

        return false;
    }
}

/// <summary>
/// 책임 :
/// - runtime state가 올린 상호작용 사실을 현재 무기 조합에 맞는 pair rule로 전달하는 진입점 역할을 한다.
/// - inventory/coordinator는 슬롯 상태만 소유하고, 쌍무기 전투 문법 해석은 이 계층에서 분리되게 만든다.
/// </summary>
public sealed class WeaponInteractionLayer : IWeaponInteractionLayer
{
    private readonly WeaponRuntimeCoordinator coordinator;
    private readonly WeaponPairInteractionRule[] rules;

    public WeaponInteractionLayer(WeaponRuntimeCoordinator coordinator)
    {
        this.coordinator = coordinator;
        rules = WeaponPairInteractionRuleRegistry.CreateDefaultRules();
    }

    public void NotifyAbilityActivated(in WeaponInteractionContext context)
    {
        if (coordinator == null || context.SourceWeapon == null)
            return;

        for (int i = 0; i < rules.Length; i++)
        {
            if (rules[i] == null || !rules[i].SupportsPair(context.SourceWeapon, context.OtherWeapon))
                continue;

            if (rules[i].TryHandleAbilityActivated(context, coordinator))
                return;
        }
    }
}

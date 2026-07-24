using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 월영도 전용 소켓을 정의해 기본 공격, 열기 조건 강화 공격, 기본 스킬, 공명 피니시 시동기를 명시적으로 authoring 하게 한다.
/// - 냉기 스택 최대치, 감쇠 시간, 반대 슬롯 조건 임계치를 함께 제공해 data/processor/strategy가 같은 규칙을 공유하게 만든다.
/// </summary>
[CreateAssetMenu(fileName = "WAL_MoonBlade", menuName = "Game/Weapon Ability Loadout/Moon Blade")]
public sealed class MoonBladeLoadout : WeaponAbilityLoadout
{
    [Header("Core Actions")]
    [SerializeField] private AbilityDefinition baseAttack;
    [SerializeField] private AbilityDefinition frostedAttack;
    [SerializeField] private AbilityDefinition defaultSkill1;
    [SerializeField] private AbilityDefinition lunarFinishStarter;

    [Header("Runtime Defaults")]
    [SerializeField, Min(1)] private int maxColdStacks = 3;
    [SerializeField, Min(0f)] private float coldDecaySeconds = 5f;

    [Header("Cross-Weapon Thresholds")]
    [SerializeField, Min(1)] private int requiredSunHeatForFrostedAttack = 2;
    [SerializeField, Min(1)] private int requiredColdForLunarFinish = 3;
    [SerializeField, Min(1)] private int requiredSunHeatForLunarFinish = 3;

    public AbilityDefinition BaseAttack => baseAttack;
    public AbilityDefinition FrostedAttack => frostedAttack;
    public AbilityDefinition DefaultSkill1 => defaultSkill1;
    public AbilityDefinition LunarFinishStarter => lunarFinishStarter;
    public int MaxColdStacks => Mathf.Max(1, maxColdStacks);
    public float ColdDecaySeconds => Mathf.Max(0f, coldDecaySeconds);
    public int RequiredSunHeatForFrostedAttack => Mathf.Max(1, requiredSunHeatForFrostedAttack);
    public int RequiredColdForLunarFinish => Mathf.Max(1, requiredColdForLunarFinish);
    public int RequiredSunHeatForLunarFinish => Mathf.Max(1, requiredSunHeatForLunarFinish);
    public override System.Type ExpectedRuntimeDataType => typeof(MoonBladeRuntimeData);
    public override System.Type ExpectedRuntimeProcessorType => typeof(MoonBladeRuntimeProcessor);

    public override AbilityDefinition GetDefaultAbility(WeaponAbilitySlot slot) => slot switch
    {
        WeaponAbilitySlot.Attack => baseAttack,
        WeaponAbilitySlot.Skill1 => defaultSkill1,
        _ => null
    };

    public override IEnumerable<AbilityDefinition> EnumerateGrantedAbilities()
    {
        HashSet<AbilityDefinition> yielded = new();

        if (baseAttack != null && yielded.Add(baseAttack))
            yield return baseAttack;

        if (frostedAttack != null && yielded.Add(frostedAttack))
            yield return frostedAttack;

        if (defaultSkill1 != null && yielded.Add(defaultSkill1))
            yield return defaultSkill1;

        if (lunarFinishStarter != null && yielded.Add(lunarFinishStarter))
            yield return lunarFinishStarter;
    }

    protected override IEnumerable<string> EnumerateCustomValidationErrors()
    {
        if (SelectionStrategy is not MoonBladeSelectionStrategy)
            yield return "MoonBladeLoadout에는 MoonBladeSelectionStrategy가 필요합니다.";

        if (baseAttack == null)
            yield return "Base Attack 참조가 비어 있습니다.";

        if (frostedAttack == null)
            yield return "Frosted Attack 참조가 비어 있습니다.";

        if (defaultSkill1 == null)
            yield return "Default Skill 1 참조가 비어 있습니다.";

        if (lunarFinishStarter == null)
            yield return "Lunar Finish Starter 참조가 비어 있습니다.";
    }
}

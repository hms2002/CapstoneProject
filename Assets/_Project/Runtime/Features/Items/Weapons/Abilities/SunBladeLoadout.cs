using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 태양도 전용 소켓을 정의해 기본 공격, 냉기 조건 강화 공격, 기본 스킬, 공명 피니시 시동기를 명시적으로 authoring 하게 한다.
/// - 열기 스택 최대치, 감쇠 시간, 반대 슬롯 조건 임계치를 함께 제공해 data/processor/strategy가 같은 규칙을 공유하게 만든다.
/// </summary>
[CreateAssetMenu(fileName = "WAL_SunBlade", menuName = "Game/Weapon Ability Loadout/Sun Blade")]
public sealed class SunBladeLoadout : WeaponAbilityLoadout
{
    [Header("Core Actions")]
    [SerializeField] private AbilityDefinition baseAttack;
    [SerializeField] private AbilityDefinition heatedAttack;
    [SerializeField] private AbilityDefinition defaultSkill1;
    [SerializeField] private AbilityDefinition solarFinishStarter;

    [Header("Runtime Defaults")]
    [SerializeField, Min(1)] private int maxHeatStacks = 3;
    [SerializeField, Min(0f)] private float heatDecaySeconds = 5f;

    [Header("Cross-Weapon Thresholds")]
    [SerializeField, Min(1)] private int requiredMoonColdForHeatedAttack = 2;
    [SerializeField, Min(1)] private int requiredHeatForSolarFinish = 3;
    [SerializeField, Min(1)] private int requiredMoonColdForSolarFinish = 3;

    public AbilityDefinition BaseAttack => baseAttack;
    public AbilityDefinition HeatedAttack => heatedAttack;
    public AbilityDefinition DefaultSkill1 => defaultSkill1;
    public AbilityDefinition SolarFinishStarter => solarFinishStarter;
    public int MaxHeatStacks => Mathf.Max(1, maxHeatStacks);
    public float HeatDecaySeconds => Mathf.Max(0f, heatDecaySeconds);
    public int RequiredMoonColdForHeatedAttack => Mathf.Max(1, requiredMoonColdForHeatedAttack);
    public int RequiredHeatForSolarFinish => Mathf.Max(1, requiredHeatForSolarFinish);
    public int RequiredMoonColdForSolarFinish => Mathf.Max(1, requiredMoonColdForSolarFinish);
    public override System.Type ExpectedRuntimeDataType => typeof(SunBladeRuntimeData);
    public override System.Type ExpectedRuntimeProcessorType => typeof(SunBladeRuntimeProcessor);

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

        if (heatedAttack != null && yielded.Add(heatedAttack))
            yield return heatedAttack;

        if (defaultSkill1 != null && yielded.Add(defaultSkill1))
            yield return defaultSkill1;

        if (solarFinishStarter != null && yielded.Add(solarFinishStarter))
            yield return solarFinishStarter;
    }

    protected override IEnumerable<string> EnumerateCustomValidationErrors()
    {
        if (SelectionStrategy is not SunBladeSelectionStrategy)
            yield return "SunBladeLoadout에는 SunBladeSelectionStrategy가 필요합니다.";

        if (baseAttack == null)
            yield return "Base Attack 참조가 비어 있습니다.";

        if (heatedAttack == null)
            yield return "Heated Attack 참조가 비어 있습니다.";

        if (defaultSkill1 == null)
            yield return "Default Skill 1 참조가 비어 있습니다.";

        if (solarFinishStarter == null)
            yield return "Solar Finish Starter 참조가 비어 있습니다.";
    }
}

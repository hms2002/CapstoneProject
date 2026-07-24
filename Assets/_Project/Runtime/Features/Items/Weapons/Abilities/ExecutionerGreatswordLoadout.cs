using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 대검 처형자 전용 소켓을 정의해 기본 공격, 처형 준비, 처형 성공/실패 분기를 명시적으로 authoring 할 수 있게 한다.
/// - selector와 runtime state가 공통 검색 없이 필요한 AD를 직접 참조하도록 대검 전용 loadout 계약을 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "WAL_ExecutionerGreatsword", menuName = "Game/Weapon Ability Loadout/Executioner Greatsword")]
public sealed class ExecutionerGreatswordLoadout : WeaponAbilityLoadout
{
    [Header("Core Actions")]
    [SerializeField] private AbilityDefinition baseAttack;
    [SerializeField] private AbilityDefinition executionReadyAttack;
    [SerializeField] private AbilityDefinition executionFinish;
    [SerializeField] private AbilityDefinition executionFallback;

    [Header("Other Slots")]
    [SerializeField] private AbilityDefinition skill2DefaultAbility;

    public AbilityDefinition BaseAttack => baseAttack;
    public AbilityDefinition ExecutionReadyAttack => executionReadyAttack;
    public AbilityDefinition ExecutionFinish => executionFinish;
    public AbilityDefinition ExecutionFallback => executionFallback;
    public AbilityDefinition Skill2DefaultAbility => skill2DefaultAbility;

    public override AbilityDefinition GetDefaultAbility(WeaponAbilitySlot slot) => slot switch
    {
        WeaponAbilitySlot.Attack => baseAttack,
        WeaponAbilitySlot.Skill1 => executionReadyAttack,
        WeaponAbilitySlot.Skill2 => skill2DefaultAbility,
        _ => null
    };

    public override IEnumerable<AbilityDefinition> EnumerateGrantedAbilities()
    {
        HashSet<AbilityDefinition> yielded = new();

        if (baseAttack != null && yielded.Add(baseAttack))
            yield return baseAttack;

        if (executionReadyAttack != null && yielded.Add(executionReadyAttack))
            yield return executionReadyAttack;

        if (executionFinish != null && yielded.Add(executionFinish))
            yield return executionFinish;

        if (executionFallback != null && yielded.Add(executionFallback))
            yield return executionFallback;

        if (skill2DefaultAbility != null && yielded.Add(skill2DefaultAbility))
            yield return skill2DefaultAbility;
    }

    protected override IEnumerable<string> EnumerateCustomValidationErrors()
    {
        if (SelectionStrategy is not ExecutionerGreatswordSelectionStrategy)
            yield return "ExecutionerGreatswordLoadout에는 ExecutionerGreatswordSelectionStrategy가 필요합니다.";

        if (baseAttack == null)
            yield return "Base Attack 참조가 비어 있습니다.";

        if (executionReadyAttack == null)
            yield return "Execution Ready Attack 참조가 비어 있습니다.";

        if (executionFinish == null)
            yield return "Execution Finish 참조가 비어 있습니다.";

        if (executionFallback == null)
            yield return "Execution Fallback 참조가 비어 있습니다.";
    }
}

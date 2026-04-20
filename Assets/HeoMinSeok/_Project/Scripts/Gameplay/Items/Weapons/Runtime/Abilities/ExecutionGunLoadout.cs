using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 처형총 전용 소켓을 정의해 일반 사격과 표식 소비 사격을 명시적으로 authoring 하게 한다.
/// - 처형 사격이 요구하는 표식 수와 반격 창 유지 시간을 runtime default로 제공해 총/검 상태 교환 규칙을 같은 계약으로 묶는다.
/// </summary>
[CreateAssetMenu(fileName = "WAL_ExecutionGun", menuName = "Game/Weapon Ability Loadout/Execution Gun")]
public sealed class ExecutionGunLoadout : WeaponAbilityLoadout
{
    [Header("Core Actions")]
    [SerializeField] private AbilityDefinition baseShot;
    [SerializeField] private AbilityDefinition executionShot;

    [Header("Runtime Defaults")]
    [SerializeField, Min(1)] private int requiredMarksForExecutionShot = 3;
    [SerializeField, Min(0f)] private float reboundWindowSeconds = 6f;

    public AbilityDefinition BaseShot => baseShot;
    public AbilityDefinition ExecutionShot => executionShot;
    public int RequiredMarksForExecutionShot => Mathf.Max(1, requiredMarksForExecutionShot);
    public float ReboundWindowSeconds => Mathf.Max(0f, reboundWindowSeconds);
    public override System.Type ExpectedRuntimeDataType => typeof(ExecutionGunRuntimeData);
    public override System.Type ExpectedRuntimeProcessorType => typeof(ExecutionGunRuntimeProcessor);

    public override AbilityDefinition GetDefaultAbility(WeaponAbilitySlot slot) => slot switch
    {
        WeaponAbilitySlot.Attack => baseShot,
        _ => null
    };

    public override IEnumerable<AbilityDefinition> EnumerateGrantedAbilities()
    {
        HashSet<AbilityDefinition> yielded = new();

        if (baseShot != null && yielded.Add(baseShot))
            yield return baseShot;

        if (executionShot != null && yielded.Add(executionShot))
            yield return executionShot;
    }

    protected override IEnumerable<string> EnumerateCustomValidationErrors()
    {
        if (SelectionStrategy is not ExecutionGunSelectionStrategy)
            yield return "ExecutionGunLoadout에는 ExecutionGunSelectionStrategy가 필요합니다.";

        if (baseShot == null)
            yield return "Base Shot 참조가 비어 있습니다.";

        if (executionShot == null)
            yield return "Execution Shot 참조가 비어 있습니다.";
    }
}

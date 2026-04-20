using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 표식검 전용 소켓을 정의해 기본 공격, 일반 검격 스킬, 총 소비 후 열리는 반격 검격을 명시적으로 authoring 하게 한다.
/// - 표식 스택 최대치와 감쇠 시간 같은 persistent runtime default도 함께 제공해 data/processor가 같은 규칙을 공유하게 만든다.
/// </summary>
[CreateAssetMenu(fileName = "WAL_MarkSword", menuName = "Game/Weapon Ability Loadout/Mark Sword")]
public sealed class MarkSwordLoadout : WeaponAbilityLoadout
{
    [Header("Core Actions")]
    [SerializeField] private AbilityDefinition baseAttack;
    [SerializeField] private AbilityDefinition defaultSkill1;
    [SerializeField] private AbilityDefinition reboundSlash;

    [Header("Runtime Defaults")]
    [SerializeField, Min(1)] private int maxMarkStacks = 3;
    [SerializeField, Min(0f)] private float markDecaySeconds = 5f;

    public AbilityDefinition BaseAttack => baseAttack;
    public AbilityDefinition DefaultSkill1 => defaultSkill1;
    public AbilityDefinition ReboundSlash => reboundSlash;
    public int MaxMarkStacks => Mathf.Max(1, maxMarkStacks);
    public float MarkDecaySeconds => Mathf.Max(0f, markDecaySeconds);
    public override System.Type ExpectedRuntimeDataType => typeof(MarkSwordRuntimeData);
    public override System.Type ExpectedRuntimeProcessorType => typeof(MarkSwordRuntimeProcessor);

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

        if (defaultSkill1 != null && yielded.Add(defaultSkill1))
            yield return defaultSkill1;

        if (reboundSlash != null && yielded.Add(reboundSlash))
            yield return reboundSlash;
    }

    protected override IEnumerable<string> EnumerateCustomValidationErrors()
    {
        if (SelectionStrategy is not MarkSwordSelectionStrategy)
            yield return "MarkSwordLoadout에는 MarkSwordSelectionStrategy가 필요합니다.";

        if (baseAttack == null)
            yield return "Base Attack 참조가 비어 있습니다.";

        if (defaultSkill1 == null)
            yield return "Default Skill 1 참조가 비어 있습니다.";

        if (reboundSlash == null)
            yield return "Rebound Slash 참조가 비어 있습니다.";
    }
}

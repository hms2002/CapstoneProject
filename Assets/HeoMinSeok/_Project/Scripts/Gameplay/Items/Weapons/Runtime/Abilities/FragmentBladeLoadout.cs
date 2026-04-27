using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 파편검 전용 ability 소켓과 조각 기본 규칙을 authoring 한다.
/// - attack/recall/bind-enhance 세 AD가 하나의 FragmentBladeRuntimeData를 공유한다는 무기 계약을 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "WAL_FragmentBlade", menuName = "Game/Weapon Ability Loadout/Fragment Blade")]
public sealed class FragmentBladeLoadout : WeaponAbilityLoadout
{
    [Header("Abilities")]
    [SerializeField] private AbilityDefinition baseAttack;
    [SerializeField] private AbilityDefinition recallSkill;
    [SerializeField] private AbilityDefinition bindEnhanceSkill;

    [Header("Shard Defaults")]
    [SerializeField, Min(1)] private int maxShardCount = 6;
    [SerializeField, Min(0f)] private float skill2DurationSeconds = 10f;

    public AbilityDefinition BaseAttack => baseAttack;
    public AbilityDefinition RecallSkill => recallSkill;
    public AbilityDefinition BindEnhanceSkill => bindEnhanceSkill;
    public int MaxShardCount => Mathf.Max(1, maxShardCount);
    public float Skill2DurationSeconds => Mathf.Max(0f, skill2DurationSeconds);
    public override System.Type ExpectedRuntimeDataType => typeof(FragmentBladeRuntimeData);

    public override AbilityDefinition GetDefaultAbility(WeaponAbilitySlot slot) => slot switch
    {
        WeaponAbilitySlot.Attack => baseAttack,
        WeaponAbilitySlot.Skill1 => recallSkill,
        WeaponAbilitySlot.Skill2 => bindEnhanceSkill,
        _ => null
    };

    public override IEnumerable<AbilityDefinition> EnumerateGrantedAbilities()
    {
        HashSet<AbilityDefinition> yielded = new();

        if (baseAttack != null && yielded.Add(baseAttack))
            yield return baseAttack;

        if (recallSkill != null && yielded.Add(recallSkill))
            yield return recallSkill;

        if (bindEnhanceSkill != null && yielded.Add(bindEnhanceSkill))
            yield return bindEnhanceSkill;
    }

    protected override IEnumerable<string> EnumerateCustomValidationErrors()
    {
        if (SelectionStrategy is not FragmentBladeSelectionStrategy)
            yield return "FragmentBladeLoadout에는 FragmentBladeSelectionStrategy가 필요합니다.";

        if (baseAttack == null)
            yield return "Base Attack 참조가 비어 있습니다.";

        if (recallSkill == null)
            yield return "Recall Skill 참조가 비어 있습니다.";

        if (bindEnhanceSkill == null)
            yield return "Bind Enhance Skill 참조가 비어 있습니다.";
    }
}

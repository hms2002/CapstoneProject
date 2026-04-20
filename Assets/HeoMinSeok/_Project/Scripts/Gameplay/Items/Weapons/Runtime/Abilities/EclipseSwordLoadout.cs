using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 월식도 전용 능력 소켓을 정의해 기본 공격, 자세 진입/종료, 자세 중 공격을 명시적으로 authoring 할 수 있게 한다.
/// - selector와 runtime state가 공통 리스트 검색 없이 필요한 AD를 직접 참조하도록 전용 loadout 계약을 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "WAL_EclipseSword", menuName = "Game/Weapon Ability Loadout/Eclipse Sword")]
public sealed class EclipseSwordLoadout : WeaponAbilityLoadout
{
    [Header("Base State")]
    [SerializeField] private AbilityDefinition baseAttack;
    [SerializeField] private AbilityDefinition enterStance;

    [Header("Eclipse Stance")]
    [SerializeField] private AbilityDefinition stanceAttackA;
    [SerializeField] private AbilityDefinition stanceAttackB;
    [SerializeField] private AbilityDefinition bloomFinish;
    [SerializeField] private AbilityDefinition exitStance;

    [Header("Other Slots")]
    [SerializeField] private AbilityDefinition skill2DefaultAbility;

    [Header("Runtime Defaults")]
    [SerializeField] private bool startsInEclipseStance;
    [SerializeField] private bool alternateStanceAttacks = true;
    [SerializeField, Min(1)] private int attacksRequiredForBloom = 2;

    public AbilityDefinition BaseAttack => baseAttack;
    public AbilityDefinition EnterStance => enterStance;
    public AbilityDefinition StanceAttackA => stanceAttackA;
    public AbilityDefinition StanceAttackB => stanceAttackB;
    public AbilityDefinition BloomFinish => bloomFinish;
    public AbilityDefinition ExitStance => exitStance;
    public AbilityDefinition Skill2DefaultAbility => skill2DefaultAbility;
    public bool StartsInEclipseStance => startsInEclipseStance;
    public bool AlternateStanceAttacks => alternateStanceAttacks;
    public int AttacksRequiredForBloom => Mathf.Max(1, attacksRequiredForBloom);
    public override System.Type ExpectedRuntimeDataType => typeof(EclipseSwordRuntimeData);

    public override AbilityDefinition GetDefaultAbility(WeaponAbilitySlot slot) => slot switch
    {
        WeaponAbilitySlot.Attack => baseAttack,
        WeaponAbilitySlot.Skill1 => enterStance,
        WeaponAbilitySlot.Skill2 => skill2DefaultAbility,
        _ => null
    };

    public override IEnumerable<AbilityDefinition> EnumerateGrantedAbilities()
    {
        HashSet<AbilityDefinition> yielded = new();

        if (baseAttack != null && yielded.Add(baseAttack))
            yield return baseAttack;

        if (enterStance != null && yielded.Add(enterStance))
            yield return enterStance;

        if (stanceAttackA != null && yielded.Add(stanceAttackA))
            yield return stanceAttackA;

        if (stanceAttackB != null && yielded.Add(stanceAttackB))
            yield return stanceAttackB;

        if (bloomFinish != null && yielded.Add(bloomFinish))
            yield return bloomFinish;

        if (exitStance != null && yielded.Add(exitStance))
            yield return exitStance;

        if (skill2DefaultAbility != null && yielded.Add(skill2DefaultAbility))
            yield return skill2DefaultAbility;
    }

    protected override IEnumerable<string> EnumerateCustomValidationErrors()
    {
        if (SelectionStrategy is not EclipseSwordSelectionStrategy)
            yield return "EclipseSwordLoadout에는 EclipseSwordSelectionStrategy가 필요합니다.";

        if (baseAttack == null)
            yield return "Base Attack 참조가 비어 있습니다.";

        if (enterStance == null)
            yield return "Enter Stance 참조가 비어 있습니다.";

        if (stanceAttackA == null)
            yield return "Stance Attack A 참조가 비어 있습니다.";

        if (stanceAttackB == null)
            yield return "Stance Attack B 참조가 비어 있습니다.";

        if (bloomFinish == null)
            yield return "Bloom Finish 참조가 비어 있습니다.";

        if (exitStance == null)
            yield return "Exit Stance 참조가 비어 있습니다.";
    }
}

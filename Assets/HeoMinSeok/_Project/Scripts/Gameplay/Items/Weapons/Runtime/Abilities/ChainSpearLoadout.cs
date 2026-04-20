using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 사슬창 전용 능력 소켓을 정의해 기본 공격, 연결 시작, 연결 소비(당기기), 연결 해제(회수)를 명시적으로 authoring 할 수 있게 한다.
/// - selector와 runtime state가 공통 검색 없이 필요한 AD를 직접 참조하도록 사슬창 전용 loadout 계약을 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "WAL_ChainSpear", menuName = "Game/Weapon Ability Loadout/Chain Spear")]
public sealed class ChainSpearLoadout : WeaponAbilityLoadout
{
    [Header("Core Actions")]
    [SerializeField] private AbilityDefinition baseAttack;
    [SerializeField] private AbilityDefinition chainThrow;
    [SerializeField] private AbilityDefinition chainPull;
    [SerializeField] private AbilityDefinition chainRecall;

    public AbilityDefinition BaseAttack => baseAttack;
    public AbilityDefinition ChainThrow => chainThrow;
    public AbilityDefinition ChainPull => chainPull;
    public AbilityDefinition ChainRecall => chainRecall;

    public override AbilityDefinition GetDefaultAbility(WeaponAbilitySlot slot) => slot switch
    {
        WeaponAbilitySlot.Attack => baseAttack,
        WeaponAbilitySlot.Skill1 => chainThrow,
        WeaponAbilitySlot.Skill2 => null,
        _ => null
    };

    public override IEnumerable<AbilityDefinition> EnumerateGrantedAbilities()
    {
        HashSet<AbilityDefinition> yielded = new();

        if (baseAttack != null && yielded.Add(baseAttack))
            yield return baseAttack;

        if (chainThrow != null && yielded.Add(chainThrow))
            yield return chainThrow;

        if (chainPull != null && yielded.Add(chainPull))
            yield return chainPull;

        if (chainRecall != null && yielded.Add(chainRecall))
            yield return chainRecall;
    }

    protected override IEnumerable<string> EnumerateCustomValidationErrors()
    {
        if (SelectionStrategy is not ChainSpearSelectionStrategy)
            yield return "ChainSpearLoadout에는 ChainSpearSelectionStrategy가 필요합니다.";

        if (baseAttack == null)
            yield return "Base Attack 참조가 비어 있습니다.";

        if (chainThrow == null)
            yield return "Chain Throw 참조가 비어 있습니다.";

        if (chainPull == null)
            yield return "Chain Pull 참조가 비어 있습니다.";

        if (chainRecall == null)
            yield return "Chain Recall 참조가 비어 있습니다.";
    }
}

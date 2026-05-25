using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "WAL_Flowering", menuName = "Game/Weapon Ability Loadout/Flowering")]
public sealed class FloweringLoadout : WeaponAbilityLoadout
{
    [Header("Abilities")]
    [SerializeField] private AbilityDefinition baseAttack;
    [SerializeField] private AbilityDefinition bloomAttack;
    [SerializeField] private AbilityDefinition bloomSkill;

    public AbilityDefinition BaseAttack => baseAttack;
    public AbilityDefinition BloomAttack => bloomAttack;
    public AbilityDefinition BloomSkill => bloomSkill;
    public override System.Type ExpectedRuntimeDataType => typeof(FloweringRuntimeData);

    public override AbilityDefinition GetDefaultAbility(WeaponAbilitySlot slot)
    {
        return slot switch
        {
            WeaponAbilitySlot.Attack => baseAttack,
            WeaponAbilitySlot.Skill1 => bloomSkill,
            _ => null
        };
    }

    public override IEnumerable<AbilityDefinition> EnumerateGrantedAbilities()
    {
        HashSet<AbilityDefinition> yielded = new();

        if (baseAttack != null && yielded.Add(baseAttack))
            yield return baseAttack;

        if (bloomAttack != null && yielded.Add(bloomAttack))
            yield return bloomAttack;

        if (bloomSkill != null && yielded.Add(bloomSkill))
            yield return bloomSkill;
    }

    protected override IEnumerable<string> EnumerateCustomValidationErrors()
    {
        if (SelectionStrategy is not FloweringSelectionStrategy)
            yield return "FloweringLoadout requires FloweringSelectionStrategy.";

        if (baseAttack == null)
            yield return "Base Attack reference is empty.";

        if (bloomAttack == null)
            yield return "Bloom Attack reference is empty.";

        if (bloomSkill == null)
            yield return "Bloom Skill1 reference is empty.";
    }
}

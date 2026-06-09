using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 기묘한 쇳덩이 전용 ability 소켓과 탄창 기본값을 authoring 한다.
/// - 사격/빈총/투척/전탄난사가 하나의 OddIronRuntimeData 잔탄 상태를 공유한다는 계약을 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "WAL_OddIron", menuName = "Game/Weapon Ability Loadout/Odd Iron")]
public sealed class OddIronLoadout : WeaponAbilityLoadout
{
    [Header("Abilities")]
    [SerializeField] private AbilityDefinition shot;
    [SerializeField] private AbilityDefinition dryFire;
    [SerializeField] private AbilityDefinition throwAndBreak;
    [SerializeField] private AbilityDefinition barrage;

    [Header("Ammo Defaults")]
    [SerializeField, Min(0)] private int maxAmmo = 6;

    public AbilityDefinition Shot => shot;
    public AbilityDefinition DryFire => dryFire;
    public AbilityDefinition ThrowAndBreak => throwAndBreak;
    public AbilityDefinition Barrage => barrage;
    public int MaxAmmo => Mathf.Max(0, maxAmmo);
    public override System.Type ExpectedRuntimeDataType => typeof(OddIronRuntimeData);

    public override AbilityDefinition GetDefaultAbility(WeaponAbilitySlot slot) => slot switch
    {
        WeaponAbilitySlot.Attack => shot,
        WeaponAbilitySlot.Skill1 => throwAndBreak,
        WeaponAbilitySlot.Skill2 => barrage,
        _ => null
    };

    public override IEnumerable<AbilityDefinition> EnumerateGrantedAbilities()
    {
        HashSet<AbilityDefinition> yielded = new();

        if (shot != null && yielded.Add(shot))
            yield return shot;

        if (dryFire != null && yielded.Add(dryFire))
            yield return dryFire;

        if (throwAndBreak != null && yielded.Add(throwAndBreak))
            yield return throwAndBreak;

        if (barrage != null && yielded.Add(barrage))
            yield return barrage;
    }

    protected override IEnumerable<string> EnumerateCustomValidationErrors()
    {
        if (SelectionStrategy is not OddIronSelectionStrategy)
            yield return "OddIronLoadout에는 OddIronSelectionStrategy가 필요합니다.";

        if (shot == null)
            yield return "Shot 참조가 비어 있습니다.";

        if (dryFire == null)
            yield return "Dry Fire 참조가 비어 있습니다.";

        if (throwAndBreak == null)
            yield return "Throw And Break 참조가 비어 있습니다.";

        if (barrage == null)
            yield return "Barrage 참조가 비어 있습니다.";
    }
}

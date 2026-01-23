using UnityEngine;
using UnityGAS;

public enum WeaponAbilitySlot { Attack, Skill1, Skill2 }

[CreateAssetMenu(fileName = "WD_NewWeapon", menuName = "Game/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Info")]
    public string weaponId = "Weapon.New";
    public string displayName = "New Weapon";
    public Sprite icon;

    [Header("Prefab")]
    public GameObject weaponPrefab;

    [Header("Equipped Tag (예: State.Equip.Weapon.Sword)")]
    public GameplayTag equippedTag;

    [Header("Abilities (Attack 1 + Skills 2)")]
    public AbilityDefinition attack;
    public AbilityDefinition skill1;
    public AbilityDefinition skill2;

    public AbilityDefinition GetAbility(WeaponAbilitySlot slot) => slot switch
    {
        WeaponAbilitySlot.Attack => attack,
        WeaponAbilitySlot.Skill1 => skill1,
        WeaponAbilitySlot.Skill2 => skill2,
        _ => null
    };
}

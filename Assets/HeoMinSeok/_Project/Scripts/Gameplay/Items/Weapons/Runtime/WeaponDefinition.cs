using UnityEngine;
using UnityGAS;
using System;
using System.Collections.Generic;

public enum WeaponAbilitySlot { Attack, Skill1, Skill2 }

[CreateAssetMenu(fileName = "WD_NewWeapon", menuName = "Game/Weapon Definition")]
public class WeaponDefinition : ScriptableObject, IInventoryItemDefinition
{
    [Header("Info")]
    public string weaponId = "Weapon.New";
    public string displayName = "New Weapon";
    public Sprite icon;

    [TextArea] public string description;

    [Header("Stats (applied only while equipped)")]
    public List<WeaponStatModifier> statModifiers = new();

    [Header("Input Hints (UI)")]
    public string attackInputHint = "우클릭";
    public string skill1InputHint = "Q";
    public string skill2InputHint = "E";

    [Serializable]
    public struct WeaponStatModifier
    {
        public AttributeDefinition attribute;
        public ModifierType type;  // Flat / Percent

        [Tooltip("Flat: +10, Percent: +0.1 (즉 +10%)")]
        public float value;

        [Tooltip("(선택) UI에 표시할 라벨. 비우면 AttributeDefinition.name 사용")]
        public string labelOverride;
    }

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

    // IInventoryItemDefinition
    public InventoryItemKind Kind => InventoryItemKind.Weapon;
    public string ItemId => weaponId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "GraveLootTable", menuName = "Game/Loot/Grave Loot Table")]
public class GraveLootTable : ScriptableObject
{
    [Header("Weapon Grave Drop Count")]
    [SerializeField] private CountRangeWeightProfile weaponDropCountProfile = new CountRangeWeightProfile();

    [Header("Relic Grave Drop Count")]
    [SerializeField] private CountRangeWeightProfile relicDropCountProfile = new CountRangeWeightProfile();

    [Header("Relic Rarity Weights")]
    public float normalRelicWeight = 90f;
    public float rareRelicWeight = 10f;
    public float epicRelicWeight = 0f;

    [FormerlySerializedAs("weaponDropMinCount")]
    [SerializeField, HideInInspector] private int legacyWeaponDropMinCount = 1;
    [FormerlySerializedAs("weaponDropMaxCount")]
    [SerializeField, HideInInspector] private int legacyWeaponDropMaxCount = 2;
    [FormerlySerializedAs("weaponDropCounts")]
    [SerializeField, HideInInspector] private List<DropCountOption> legacyWeaponDropCounts = new List<DropCountOption>();

    [FormerlySerializedAs("relicDropMinCount")]
    [SerializeField, HideInInspector] private int legacyRelicDropMinCount = 1;
    [FormerlySerializedAs("relicDropMaxCount")]
    [SerializeField, HideInInspector] private int legacyRelicDropMaxCount = 2;
    [FormerlySerializedAs("relicDropCounts")]
    [SerializeField, HideInInspector] private List<DropCountOption> legacyRelicDropCounts = new List<DropCountOption>();

    public CountRangeWeightProfile WeaponDropCountProfile => weaponDropCountProfile;
    public CountRangeWeightProfile RelicDropCountProfile => relicDropCountProfile;

    private void OnValidate()
    {
        weaponDropCountProfile ??= new CountRangeWeightProfile();
        relicDropCountProfile ??= new CountRangeWeightProfile();

        weaponDropCountProfile.TryInitializeFromLegacy(legacyWeaponDropMinCount, legacyWeaponDropMaxCount, legacyWeaponDropCounts);
        relicDropCountProfile.TryInitializeFromLegacy(legacyRelicDropMinCount, legacyRelicDropMaxCount, legacyRelicDropCounts);
    }
}

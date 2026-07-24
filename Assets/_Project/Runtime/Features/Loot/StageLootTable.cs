using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Table_Stage1", menuName = "Game/Loot/Stage Loot Table")]
public class StageLootTable : ScriptableObject
{
    [Header("Chest Weapon Count")]
    [SerializeField] private CountRangeWeightProfile chestWeaponCountProfile = new CountRangeWeightProfile();

    [Header("Chest Relic Count")]
    [SerializeField] private CountRangeWeightProfile chestRelicCountProfile = new CountRangeWeightProfile();

    [Header("Chest Consumable Count")]
    [SerializeField] private CountRangeWeightProfile chestConsumableCountProfile = new CountRangeWeightProfile
    {
        minCount = 0,
        maxCount = 0,
        weights = new List<DropCountOption>
        {
            new DropCountOption { count = 0, weight = 100 }
        }
    };

    [Header("Boss Chest Weapon Count")]
    [SerializeField] private CountRangeWeightProfile bossWeaponCountProfile = new CountRangeWeightProfile();

    [Header("Boss Chest Relic Count")]
    [SerializeField] private CountRangeWeightProfile bossRelicCountProfile = new CountRangeWeightProfile();

    [Header("Boss Relic Rarity Weights")]
    [SerializeField, Min(0)] private int bossCommonWeight = 60;
    [SerializeField, Min(0)] private int bossRareWeight = 30;
    [SerializeField, Min(0)] private int bossEpicWeight = 10;

    [Header("Relic Rarity Weights")]
    public int commonWeight = 60;
    public int rareWeight = 30;
    public int epicWeight = 10;
    public int legendaryWeight = 0;

    [Header("Mob Loot Weights")]
    public int mobNothingWeight = 65;
    public int mobWeaponWeight = 2;
    public int mobRelicWeight = 3;
    public int mobConsumableWeight = 15;
    public int mobFieldItemWeight = 15;

    [Header("Boss Reward")]
    public int bossStoneCount = 5;
    [SerializeField, Min(0)] private int bossFieldHealBaseCount;

    [FormerlySerializedAs("chestWeaponMinCount")]
    [SerializeField, HideInInspector] private int legacyChestWeaponMinCount = 1;
    [FormerlySerializedAs("chestWeaponMaxCount")]
    [SerializeField, HideInInspector] private int legacyChestWeaponMaxCount = 1;
    [FormerlySerializedAs("chestWeaponCounts")]
    [SerializeField, HideInInspector] private List<DropCountOption> legacyChestWeaponCounts = new List<DropCountOption>();

    [FormerlySerializedAs("chestRelicMinCount")]
    [SerializeField, HideInInspector] private int legacyChestRelicMinCount = 1;
    [FormerlySerializedAs("chestRelicMaxCount")]
    [SerializeField, HideInInspector] private int legacyChestRelicMaxCount = 1;
    [FormerlySerializedAs("chestRelicCounts")]
    [SerializeField, HideInInspector] private List<DropCountOption> legacyChestRelicCounts = new List<DropCountOption>();

    public CountRangeWeightProfile ChestWeaponCountProfile => chestWeaponCountProfile;
    public CountRangeWeightProfile ChestRelicCountProfile => chestRelicCountProfile;
    public CountRangeWeightProfile ChestConsumableCountProfile => chestConsumableCountProfile;
    public CountRangeWeightProfile BossWeaponCountProfile => bossWeaponCountProfile;
    public CountRangeWeightProfile BossRelicCountProfile => bossRelicCountProfile;
    public int BossCommonWeight => bossCommonWeight;
    public int BossRareWeight => bossRareWeight;
    public int BossEpicWeight => bossEpicWeight;
    public int BossFieldHealBaseCount => bossFieldHealBaseCount;

    private void OnValidate()
    {
        chestWeaponCountProfile ??= new CountRangeWeightProfile();
        chestRelicCountProfile ??= new CountRangeWeightProfile();
        chestConsumableCountProfile ??= new CountRangeWeightProfile();
        bossWeaponCountProfile ??= new CountRangeWeightProfile();
        bossRelicCountProfile ??= new CountRangeWeightProfile();

        chestWeaponCountProfile.TryInitializeFromLegacy(legacyChestWeaponMinCount, legacyChestWeaponMaxCount, legacyChestWeaponCounts);
        chestRelicCountProfile.TryInitializeFromLegacy(legacyChestRelicMinCount, legacyChestRelicMaxCount, legacyChestRelicCounts);
        chestConsumableCountProfile.EnsureDefaults(0, 0);
        bossWeaponCountProfile.EnsureDefaults(1, 1);
        bossRelicCountProfile.EnsureDefaults(1, 1);

        bossCommonWeight = Mathf.Max(0, bossCommonWeight);
        bossRareWeight = Mathf.Max(0, bossRareWeight);
        bossEpicWeight = Mathf.Max(0, bossEpicWeight);
        bossStoneCount = Mathf.Max(0, bossStoneCount);
        bossFieldHealBaseCount = Mathf.Max(0, bossFieldHealBaseCount);
    }
}

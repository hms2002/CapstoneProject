using UnityEngine;

// 1. 레어도 Enum 정의

[CreateAssetMenu(fileName = "RD_NewRelic", menuName = "Game/Relic Definition")]
public class RelicDefinition : ScriptableObject, IInventoryItemDefinition
{
    [Header("Identity")]
    public string relicId = "Relic.New";
    public string displayName = "New Relic";
    public Sprite icon;

    [Header("Drop Rules")]
    public ItemRarity rarity;

    [TextArea] public string description;

    [Header("Upgrade")]
    [Min(1)] public int maxLevel = 1;
    [Tooltip("이 유물을 획득/추가할 때 더해지는 강화량(기본 1). +2강 유물이라면 2로 설정.")]
    [Min(1)] public int dropLevel = 1;

    [Header("Runtime")]
    public RelicLogic logic;
    public ScriptableObject param; // 필요하면 데이터(SO) 연결

    // Keep retired definitions resolvable for existing inventories and saves.
    // Only acquisition sources and the encyclopedia use this exclusion.
    public static bool IsRetiredId(string id) => id is
        "RD_FeatherOrbit" or
        "RD_Stenographer" or
        "RD_OneDropOfSwiftness" or
        "RD_HunterMark" or
        "RD_RunawayShoes" or
        "RD_ChainCharm" or
        "RD_RunningLedger";

    public int ClampLevel(int level)
    {
        if (maxLevel < 1) maxLevel = 1;
        if (level < 1) level = 1;
        if (level > maxLevel) level = maxLevel;
        return level;
    }

    // IInventoryItemDefinition
    public InventoryItemKind Kind => InventoryItemKind.Relic;
    public string ItemId => relicId;
    public string DisplayName => GameText.Asset(this, "displayName", displayName);
    public Sprite Icon => icon;
}

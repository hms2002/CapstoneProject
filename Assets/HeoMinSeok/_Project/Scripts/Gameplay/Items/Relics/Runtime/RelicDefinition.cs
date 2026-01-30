using UnityEngine;

[CreateAssetMenu(fileName = "RD_NewRelic", menuName = "Game/Relic Definition")]
public class RelicDefinition : ScriptableObject, IInventoryItemDefinition
{
    [Header("Identity")]
    public string relicId = "Relic.New";
    public string displayName = "New Relic";
    public Sprite icon;

    [TextArea] public string description;

    [Header("Upgrade")]
    [Min(1)] public int maxLevel = 1;
    [Tooltip("이 유물을 획득/추가할 때 더해지는 강화량(기본 1). +2강 유물이라면 2로 설정.")]
    [Min(1)] public int dropLevel = 1;

    [Header("Runtime")]
    public RelicLogic logic;
    public ScriptableObject param; // 필요하면 데이터(SO) 연결

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
    public string DisplayName => displayName;
    public Sprite Icon => icon;
}

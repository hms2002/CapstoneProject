using UnityEngine;

// 1. 레어도 Enum 정의

[CreateAssetMenu(fileName = "RD_NewRelic", menuName = "Game/Relic Definition")]
public class RelicDefinition : ScriptableObject, IInventoryItemDefinition
{
    public string relicId = "Relic.New";
    public string displayName = "New Relic";
    public Sprite icon;

    [Header("Drop Rules")]
    public ItemRarity rarity;

    [TextArea] public string description;

    public RelicLogic logic;
    public ScriptableObject param; // 필요하면 데이터(SO) 연결
// IInventoryItemDefinition
public InventoryItemKind Kind => InventoryItemKind.Relic;
public string ItemId => relicId;
public string DisplayName => displayName;
public Sprite Icon => icon;

}

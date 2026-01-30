using UnityEngine;

public enum InventoryItemKind
{
    Weapon,
    Relic
}

public interface IInventoryItemDefinition
{
    InventoryItemKind Kind { get; }
    string ItemId { get; }
    string DisplayName { get; }
    Sprite Icon { get; }
}

public static class InventoryItemUtil
{
    public static IInventoryItemDefinition AsDef(this ScriptableObject so) => so as IInventoryItemDefinition;

    public static InventoryItemKind? KindOf(this ScriptableObject so)
    {
        var d = so as IInventoryItemDefinition;
        return d != null ? d.Kind : (InventoryItemKind?)null;
    }
}

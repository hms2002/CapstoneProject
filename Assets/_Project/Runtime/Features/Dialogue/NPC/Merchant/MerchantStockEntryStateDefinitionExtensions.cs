using UnityEngine;

/// <summary>
/// 책임 : Core 상점 재고 DTO를 현재 Feature 아이템 데이터베이스의 ScriptableObject 정의로 해석한다.
/// </summary>
public static class MerchantStockEntryStateDefinitionExtensions
{
    public static ScriptableObject ResolveDefinition(this MerchantStockEntryState state)
    {
        if (state == null || !state.HasItem || ItemManager.Instance == null)
            return null;

        return state.kind switch
        {
            InventoryItemKind.Weapon => ItemManager.Instance.GetWeaponData(state.itemId),
            InventoryItemKind.Relic => ItemManager.Instance.GetRelicData(state.itemId),
            InventoryItemKind.Consumable => ItemManager.Instance.GetConsumableData(state.itemId),
            _ => null
        };
    }
}

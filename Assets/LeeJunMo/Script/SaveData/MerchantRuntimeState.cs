using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MerchantStockEntryState
{
    public InventoryItemKind kind;
    public string itemId;
    public int price;
    public bool isSold;

    public bool HasItem => !string.IsNullOrWhiteSpace(itemId);

    public MerchantStockEntryState() { }

    public MerchantStockEntryState(InventoryItemKind kind, string itemId, int price)
    {
        this.kind = kind;
        this.itemId = itemId;
        this.price = Mathf.Max(0, price);
    }

    public static MerchantStockEntryState Empty()
    {
        return new MerchantStockEntryState
        {
            itemId = string.Empty,
            price = 0,
            isSold = false
        };
    }

    public ScriptableObject ResolveDefinition()
    {
        if (!HasItem || ItemManager.Instance == null)
            return null;

        return kind switch
        {
            InventoryItemKind.Weapon => ItemManager.Instance.GetWeaponData(itemId),
            InventoryItemKind.Relic => ItemManager.Instance.GetRelicData(itemId),
            InventoryItemKind.Consumable => ItemManager.Instance.GetConsumableData(itemId),
            _ => null
        };
    }
}

[Serializable]
public sealed class MerchantRuntimeState
{
    public string merchantId;
    public List<MerchantStockEntryState> slots = new List<MerchantStockEntryState>();

    public MerchantRuntimeState() { }

    public MerchantRuntimeState(string merchantId, List<MerchantStockEntryState> slots)
    {
        this.merchantId = merchantId;
        this.slots = slots ?? new List<MerchantStockEntryState>();
    }
}

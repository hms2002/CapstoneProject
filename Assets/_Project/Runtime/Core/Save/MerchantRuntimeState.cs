using System;
using System.Collections.Generic;

/// <summary>
/// 책임 : 상점 슬롯 하나의 런타임 재고 상태를 저장하는 직렬화 DTO다.
/// </summary>
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
        this.price = Math.Max(0, price);
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
}

/// <summary>
/// 책임 : 상인별 런타임 재고 슬롯과 갱신 사용 횟수를 저장하는 직렬화 DTO다.
/// </summary>
[Serializable]
public sealed class MerchantRuntimeState
{
    public string merchantId;
    public List<MerchantStockEntryState> slots = new List<MerchantStockEntryState>();
    public int refreshCountUsed;

    public MerchantRuntimeState() { }

    public MerchantRuntimeState(string merchantId, List<MerchantStockEntryState> slots)
    {
        this.merchantId = merchantId;
        this.slots = slots ?? new List<MerchantStockEntryState>();
    }
}

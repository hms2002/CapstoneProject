using UnityEngine;

public enum MerchantPurchaseResultType
{
    Success,
    InvalidRequest,
    SoldOut,
    MissingDefinition,
    NotEnoughCurrency,
    WeaponInventoryFull,
    RelicInventoryFull,
    ConsumableInventoryFull,
    RelicAlreadyMaxLevel,
    MissingSystems
}

public readonly struct MerchantPurchaseResult
{
    public MerchantPurchaseResultType Type { get; }
    public bool Succeeded => Type == MerchantPurchaseResultType.Success;

    public MerchantPurchaseResult(MerchantPurchaseResultType type)
    {
        Type = type;
    }
}

public sealed class MerchantPurchaseService
{
    /// <summary>
    /// 책임 :
    /// - 상점 구매 요청을 검증하고 실제 아이템 획득을 시도한다.
    /// - 통화 부족, 인벤토리 부족, 유물 최대 레벨 같은 실패 사유를 공통 결과 코드로 정리해 반환한다.
    /// </summary>
    public MerchantPurchaseResult TryPurchase(
        IPlayerInteractor player,
        MerchantStockEntryState stockEntry,
        ScriptableObject definition)
    {
        if (player is not Component playerComponent || stockEntry == null)
            return new MerchantPurchaseResult(MerchantPurchaseResultType.InvalidRequest);

        if (stockEntry.isSold)
            return new MerchantPurchaseResult(MerchantPurchaseResultType.SoldOut);

        if (definition == null)
            return new MerchantPurchaseResult(MerchantPurchaseResultType.MissingDefinition);

        if (CurrencyManager.Instance == null)
            return new MerchantPurchaseResult(MerchantPurchaseResultType.MissingSystems);

        MerchantPurchaseResultType acquireFailure = GetAcquireFailureType(playerComponent, definition);
        if (acquireFailure != MerchantPurchaseResultType.Success)
            return new MerchantPurchaseResult(acquireFailure);

        if (!CurrencyManager.Instance.SpendMagicStone(stockEntry.price))
            return new MerchantPurchaseResult(MerchantPurchaseResultType.NotEnoughCurrency);

        if (!TryAcquire(playerComponent, definition))
        {
            CurrencyManager.Instance.AddMagicStone(stockEntry.price);
            MerchantPurchaseResultType retryFailure = GetAcquireFailureType(playerComponent, definition);
            return new MerchantPurchaseResult(
                retryFailure != MerchantPurchaseResultType.Success
                    ? retryFailure
                    : GetFallbackInventoryFailureType(definition));
        }

        return new MerchantPurchaseResult(MerchantPurchaseResultType.Success);
    }

    private static bool CanAcquire(Component playerComponent, ScriptableObject definition)
    {
        return definition switch
        {
            WeaponDefinition weapon => CanAcquireWeapon(playerComponent, weapon),
            RelicDefinition relic => CanAcquireRelic(playerComponent, relic),
            ConsumableDefinition consumable => CanAcquireConsumable(playerComponent, consumable),
            _ => false
        };
    }

    /// <summary>
    /// 책임 :
    /// - 상점 구매 대상이 현재 어떤 사유로 획득 불가능한지 구체적으로 판정한다.
    /// - 구매 UI가 인벤토리 부족과 유물 최대 레벨을 정확히 구분하도록 돕는다.
    /// </summary>
    private static MerchantPurchaseResultType GetAcquireFailureType(Component playerComponent, ScriptableObject definition)
    {
        return definition switch
        {
            WeaponDefinition weapon => GetWeaponAcquireFailureType(playerComponent, weapon),
            RelicDefinition relic => GetRelicAcquireFailureType(playerComponent, relic),
            ConsumableDefinition consumable => CanAcquireConsumable(playerComponent, consumable)
                ? MerchantPurchaseResultType.Success
                : MerchantPurchaseResultType.ConsumableInventoryFull,
            _ => MerchantPurchaseResultType.InvalidRequest
        };
    }

    private static bool TryAcquire(Component playerComponent, ScriptableObject definition)
    {
        return definition switch
        {
            WeaponDefinition weapon => TryAcquireWeapon(playerComponent, weapon),
            RelicDefinition relic => TryAcquireRelic(playerComponent, relic),
            ConsumableDefinition consumable => TryAcquireConsumable(playerComponent, consumable),
            _ => false
        };
    }

    /// <summary>
    /// 책임 :
    /// - 재검증 타이밍에서 상세 실패 사유를 다시 얻지 못했을 때 정의 타입 기준의 기본 실패 코드를 제공한다.
    /// - 구매 흐름이 최소한 올바른 인벤토리 부족 경고를 유지하도록 마지막 fallback 역할을 맡는다.
    /// </summary>
    private static MerchantPurchaseResultType GetFallbackInventoryFailureType(ScriptableObject definition)
    {
        return definition switch
        {
            WeaponDefinition => MerchantPurchaseResultType.WeaponInventoryFull,
            RelicDefinition => MerchantPurchaseResultType.RelicInventoryFull,
            ConsumableDefinition => MerchantPurchaseResultType.ConsumableInventoryFull,
            _ => MerchantPurchaseResultType.InvalidRequest
        };
    }

    private static bool CanAcquireWeapon(Component playerComponent, WeaponDefinition weapon)
    {
        WeaponInventory2D inventory = playerComponent.GetComponent<WeaponInventory2D>();
        return inventory != null && inventory.CanAcquireWithoutReplacement(weapon);
    }

    /// <summary>
    /// 책임 :
    /// - 무기 구매 실패 사유를 WeaponInventory2D의 상세 결과로부터 MerchantPurchaseResultType으로 변환한다.
    /// - 무기 슬롯 부족과 잘못된 요청을 상점 계층에서 구분 가능하게 한다.
    /// </summary>
    private static MerchantPurchaseResultType GetWeaponAcquireFailureType(Component playerComponent, WeaponDefinition weapon)
    {
        WeaponInventory2D inventory = playerComponent.GetComponent<WeaponInventory2D>();
        if (inventory == null || weapon == null)
            return MerchantPurchaseResultType.InvalidRequest;

        WeaponInventory2D.AcquireResult result = inventory.TryAcquireWithoutReplacementDetailed(weapon);
        return result switch
        {
            WeaponInventory2D.AcquireResult.Success => MerchantPurchaseResultType.Success,
            WeaponInventory2D.AcquireResult.InventoryFull => MerchantPurchaseResultType.WeaponInventoryFull,
            WeaponInventory2D.AcquireResult.DuplicateRejected => MerchantPurchaseResultType.WeaponInventoryFull,
            _ => MerchantPurchaseResultType.InvalidRequest
        };
    }

    private static bool CanAcquireRelic(Component playerComponent, RelicDefinition relic)
    {
        RelicInventory inventory = playerComponent.GetComponent<RelicInventory>();
        if (inventory == null || relic == null)
            return false;

        if (inventory.TryGetRelicLevelById(relic.relicId, out int currentLevel))
        {
            int nextLevel = relic.ClampLevel(currentLevel + Mathf.Max(1, relic.dropLevel));
            return nextLevel > currentLevel;
        }

        return inventory.Count < inventory.Capacity;
    }

    /// <summary>
    /// 책임 :
    /// - 유물 구매 실패 사유를 RelicInventory의 상세 결과로부터 MerchantPurchaseResultType으로 변환한다.
    /// - 인벤토리 부족과 최대 레벨 도달을 상점 UI가 다른 피드백으로 처리할 수 있게 한다.
    /// </summary>
    private static MerchantPurchaseResultType GetRelicAcquireFailureType(Component playerComponent, RelicDefinition relic)
    {
        RelicInventory inventory = playerComponent.GetComponent<RelicInventory>();
        if (inventory == null || relic == null)
            return MerchantPurchaseResultType.InvalidRequest;

        RelicInventory.AcquireResult result = inventory.TryAcquireOrUpgradeDetailed(relic);
        return result switch
        {
            RelicInventory.AcquireResult.Success => MerchantPurchaseResultType.Success,
            RelicInventory.AcquireResult.InventoryFull => MerchantPurchaseResultType.RelicInventoryFull,
            RelicInventory.AcquireResult.AlreadyMaxLevel => MerchantPurchaseResultType.RelicAlreadyMaxLevel,
            _ => MerchantPurchaseResultType.InvalidRequest
        };
    }

    private static bool CanAcquireConsumable(Component playerComponent, ConsumableDefinition consumable)
    {
        if (consumable == null)
            return false;

        PlayerConsumableInventory inventory = PlayerConsumableInventory.GetOrAdd(playerComponent.transform);
        if (inventory == null)
            return false;

        for (int i = 0; i < inventory.SlotCount; i++)
        {
            if (inventory.GetConsumableInSlot(i) == null)
                return true;
        }

        return false;
    }

    private static bool TryAcquireWeapon(Component playerComponent, WeaponDefinition weapon)
    {
        WeaponInventory2D inventory = playerComponent.GetComponent<WeaponInventory2D>();
        return inventory != null && inventory.TryAcquireWithoutReplacement(weapon);
    }

    private static bool TryAcquireRelic(Component playerComponent, RelicDefinition relic)
    {
        RelicInventory inventory = playerComponent.GetComponent<RelicInventory>();
        return inventory != null && inventory.TryAcquireOrUpgrade(relic);
    }

    private static bool TryAcquireConsumable(Component playerComponent, ConsumableDefinition consumable)
    {
        PlayerConsumableInventory inventory = PlayerConsumableInventory.GetOrAdd(playerComponent.transform);
        return inventory != null && inventory.TryAcquire(consumable);
    }
}

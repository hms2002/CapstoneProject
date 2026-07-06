using UnityEngine;

/// <summary>
/// 책임 :
/// - 아이템 획득/이동 실패 결과를 공통 WarningPopupCode로 변환한다.
/// - UI 렌더러가 아니라 gameplay 인벤토리 결과 해석을 담당해 Features와 UI 양쪽이 같은 매핑을 공유하게 한다.
/// </summary>
public static class InventoryDeliveryWarningResolver
{
    public static WarningPopupCode FromItem(ScriptableObject item)
    {
        return item switch
        {
            WeaponDefinition => WarningPopupCode.WeaponInventoryFull,
            RelicDefinition => WarningPopupCode.RelicInventoryFull,
            ConsumableDefinition => WarningPopupCode.ConsumableInventoryFull,
            _ => WarningPopupCode.None
        };
    }

    public static WarningPopupCode FromRelicAcquireResult(RelicInventory.AcquireResult result)
    {
        return result switch
        {
            RelicInventory.AcquireResult.InventoryFull => WarningPopupCode.RelicInventoryFull,
            RelicInventory.AcquireResult.AlreadyMaxLevel => WarningPopupCode.RelicAlreadyMaxLevel,
            RelicInventory.AcquireResult.HealthTooLowForRelicChange => WarningPopupCode.RelicChangeWouldDefeatPlayer,
            _ => WarningPopupCode.None
        };
    }

    public static WarningPopupCode FromConsumableAcquireResult(PlayerConsumableInventory.AcquireResult result)
    {
        return result switch
        {
            PlayerConsumableInventory.AcquireResult.InventoryFull => WarningPopupCode.ConsumableInventoryFull,
            _ => WarningPopupCode.None
        };
    }
}

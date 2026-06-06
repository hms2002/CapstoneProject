/// <summary>
/// 책임 :
/// - 자주 재사용되는 경고 팝업 사유를 공통 코드로 정의한다.
/// - 각 기능 시스템은 문자열 하드코딩 대신 이 코드를 UIManager에 전달해 경고 표시를 요청한다.
/// </summary>
public enum WarningPopupCode
{
    None = 0,
    RelicInventoryFull,
    RelicAlreadyMaxLevel,
    WeaponInventoryFull,
    ConsumableInventoryFull,
    CannotDropHere,
    RelicChangeWouldDefeatPlayer,
    UpgradeNotEnoughMagicStone,
    UpgradeLocked,
    UpgradeUnavailable,
    LastWeaponCannotLeaveInventory
}

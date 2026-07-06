/// <summary>
/// 책임 :
/// - 자주 재사용되는 경고 팝업 사유를 Core 계약 코드로 정의한다.
/// - Gameplay/UI 호출자가 문자열 하드코딩 대신 이 코드를 WarningPopupPlayback에 전달하게 한다.
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

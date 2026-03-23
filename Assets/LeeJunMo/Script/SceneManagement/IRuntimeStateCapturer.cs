using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 무기별 개별 런타임 상태를 캡처한다.
/// </summary>
public interface IWeaponRuntimeStateCapturer
{
    void CaptureWeaponRuntimeStates(
        WeaponInventory2D weaponInventory,
        List<WeaponRuntimeState> output);
}

/// <summary>
/// 책임 : 유물별 개별 런타임 상태를 캡처한다.
/// </summary>
public interface IRelicRuntimeStateCapturer
{
    void CaptureRelicRuntimeStates(
        RelicInventory relicInventory,
        List<RelicRuntimeState> output);
}

/// <summary>
/// 책임 : 무기별 개별 런타임 상태를 복원한다.
/// </summary>
public interface IWeaponRuntimeStateRestorer
{
    void RestoreWeaponRuntimeState(
        WeaponInventory2D weaponInventory,
        WeaponRuntimeState state,
        IPlayerRuntimeResolver resolver);
}

/// <summary>
/// 책임 : 유물별 개별 런타임 상태를 복원한다.
/// </summary>
public interface IRelicRuntimeStateRestorer
{
    void RestoreRelicRuntimeState(
        RelicInventory relicInventory,
        RelicRuntimeState state,
        IPlayerRuntimeResolver resolver);
}
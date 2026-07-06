using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 씬 이동 시 플레이어의 장비 배치, GAS 런타임 상태,
/// 장비 개별 런타임 상태를 한 번에 저장/복원하기 위한 루트 DTO다.
/// </summary>
[Serializable]
public sealed class PlayerRuntimeState
{
    [Header("Equipment Layout")]
    public ConsumableInventoryState consumableInventory;
    public WeaponInventoryState weaponInventory;
    public RelicInventoryState relicInventory;

    [Header("GAS Runtime")]
    public List<AttributeRuntimeSnapshot> attributes = new();
    public List<ActiveGameplayEffectSnapshot> activeEffects = new();
    public List<ExplicitTagSnapshot> explicitTags = new();
    /// <summary>
    /// 책임 : 플레이어 고유 ability의 persistent state만 저장한다.
    /// 무기 소유 ability는 중복 저장을 피하기 위해 weaponRuntimeStates로 분리한다.
    /// </summary>
    public List<AbilityPersistentState> abilities = new();

    [Header("Equipment Runtime")]
    public List<WeaponRuntimeState> weaponRuntimeStates = new();
    public List<RelicRuntimeState> relicRuntimeStates = new();
}

/// <summary>
/// 책임 : Attribute 하나의 런타임 값을 저장/복원하기 위한 스냅샷 데이터다.
/// </summary>
[Serializable]
public sealed class AttributeRuntimeSnapshot
{
    public string attributeId;
    public float baseValue;
    public float currentValue;
}

/// <summary>
/// 책임 : 특정 무기 슬롯의 개별 런타임 상태를 저장한다.
/// 이제 무기 전용 stack / charges / unlock뿐 아니라 무기 ability persistent state JSON도 담을 수 있다.
/// </summary>
[Serializable]
public sealed class WeaponRuntimeState
{
    public int slotIndex;
    public string weaponId;
    public string stateType;
    public string json;
}

/// <summary>
/// 책임 : 특정 유물 슬롯의 개별 런타임 상태를 저장한다.
/// level은 배치 상태에도 있지만, 검증용으로 함께 들고 있어도 된다.
/// </summary>
[Serializable]
public sealed class RelicRuntimeState
{
    public int slotIndex;
    public string relicId;
    public int level;
    public string stateType;
    public string json;
}
/// <summary>
/// 책임 : 복원 가능한 유물 슬롯 하나의 배치 상태를 저장한다.
/// </summary>
[Serializable]
public sealed class RelicSlotState
{
    public string relicId;
    public int level;
}

/// <summary>
/// 책임 : 복원 가능한 무기 인벤토리 슬롯 배치와 활성 슬롯을 저장한다.
/// </summary>
[Serializable]
public sealed class WeaponInventoryState
{
    public string[] slotWeaponIds;
    public int activeSlotIndex;
}

/// <summary>
/// 책임 : 복원 가능한 소비 아이템 슬롯 하나의 배치 상태를 저장한다.
/// </summary>
[Serializable]
public sealed class ConsumableSlotState
{
    public string consumableId;
}

/// <summary>
/// 책임 : 복원 가능한 소비 아이템 인벤토리 슬롯 배열을 저장한다.
/// </summary>
[Serializable]
public sealed class ConsumableInventoryState
{
    public ConsumableSlotState[] slots;
}

/// <summary>
/// 책임 : 복원 가능한 유물 인벤토리 슬롯 배열을 저장한다.
/// </summary>
[Serializable]
public sealed class RelicInventoryState
{
    public RelicSlotState[] slots;
}

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
    public WeaponInventoryState weaponInventory;
    public RelicInventoryState relicInventory;

    [Header("GAS Runtime")]
    public List<AttributeRuntimeSnapshot> attributes = new();
    public List<ActiveGameplayEffectSnapshot> activeEffects = new();
    public List<ExplicitTagSnapshot> explicitTags = new();
    public List<AbilityRuntimeSnapshot> abilities = new();

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
/// 책임 : explicit tag 하나의 count를 저장/복원하기 위한 스냅샷 데이터다.
/// 반드시 explicit 기준으로만 사용한다.
/// </summary>
[Serializable]
public sealed class ExplicitTagSnapshot
{
    public string tagName;
    public int count;
}

/// <summary>
/// 책임 : 활성 GameplayEffect의 복원 가능한 최소 런타임 상태를 담는다.
/// 실제 spec 복원 방식은 프로젝트 구현에 맞게 확장 가능하게 둔다.
/// </summary>
[Serializable]
public sealed class ActiveGameplayEffectSnapshot
{
    public string effectId;
    public float remainingTime;
    public int stackCount;
}

/// <summary>
/// 책임 : ability별 런타임 상태를 저장/복원한다.
/// 남은 쿨다운과 현재 충전 수를 함께 보관해 씬 이동 후 실제 사용 가능 상태를 재현한다.
/// </summary>
[Serializable]
public sealed class AbilityRuntimeSnapshot
{
    public string abilityId;
    public float cooldownRemaining;
    public int chargesRemaining;
}

/// <summary>
/// 책임 : 특정 무기 슬롯의 개별 런타임 상태를 저장한다.
/// stack, 차지량, 내부 카운터 같은 장비 전용 상태 payload를 담는다.
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
[Serializable]
public sealed class RelicSlotState
{
    public string relicId;
    public int level;
}

[Serializable]
public sealed class WeaponInventoryState
{
    public string[] slotWeaponIds;
    public int activeSlotIndex;
}

[Serializable]
public sealed class RelicInventoryState
{
    public RelicSlotState[] slots;
}
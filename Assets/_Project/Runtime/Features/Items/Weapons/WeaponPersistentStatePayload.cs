using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 무기 인스턴스가 들고 다녀야 하는 영속 상태를 묶는다.
/// 현재는 무기에 연결된 Ability들의 지속 상태를 함께 저장/복원하는 용도로 사용한다.
/// </summary>
[Serializable]
public sealed class WeaponPersistentStatePayload
{
    public string weaponId;
    public List<AbilityPersistentState> abilities = new();
}
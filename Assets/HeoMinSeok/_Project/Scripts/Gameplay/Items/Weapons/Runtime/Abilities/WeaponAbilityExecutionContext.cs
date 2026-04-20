using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - executor가 실행 시작 시점에 필요한 무기, ASC, runtime state, initial target 같은 문맥을 한 번에 전달받게 한다.
/// - executor가 여기저기서 GetComponent와 캐스팅을 반복하지 않도록 공용 실행 입력 모델을 제공한다.
/// </summary>
public struct WeaponAbilityExecutionContext
{
    public AbilitySystem AbilitySystem;
    public WeaponDefinition Weapon;
    public WeaponAbilityLoadout Loadout;
    public WeaponAbilityRuntimeState RuntimeState;
    public AbilityDefinition Ability;
    public AbilitySpec Spec;
    public GameObject InitialTarget;
    public GameObject Owner;
    public Transform WeaponTransform;
    public WeaponEquipController EquipController;
}

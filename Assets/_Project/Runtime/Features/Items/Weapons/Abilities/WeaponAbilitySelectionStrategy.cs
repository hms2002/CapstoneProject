using UnityEngine;
using UnityGAS;
using System;

/// <summary>
/// 책임 :
/// - WeaponAbilityLoadout이 가진 공식 후보 AD 집합 안에서 어떤 AbilityDefinition을 선택할지 결정한다.
/// - 무기별 선택 규칙을 ScriptableObject 전략으로 분리해 selector 본체가 비대해지지 않게 만든다.
/// </summary>
public abstract class WeaponAbilitySelectionStrategy : ScriptableObject
{
    /// <summary>
    /// 책임 :
    /// - 이 전략이 어떤 WAL 타입을 기대하는지 editor/validation 계층에 알려준다.
    /// - authoring 단계에서 잘못된 loadout-전략 조합을 빠르게 경고할 수 있게 만든다.
    /// </summary>
    public virtual Type ExpectedLoadoutType => typeof(WeaponAbilityLoadout);

    /// <summary>
    /// 책임 :
    /// - 이 전략이 정상 동작하기 위해 무기 프리팹에 필요한 runtime state 타입을 노출한다.
    /// - WeaponDefinition editor가 프리팹 구성 누락을 경고할 수 있게 하는 metadata를 제공한다.
    /// </summary>
    public virtual Type ExpectedRuntimeStateType => null;

    /// <summary>
    /// 책임 :
    /// - 이 전략이 정상 동작하기 위해 무기 프리팹에 함께 있어야 하는 executor 타입을 editor/validation 계층에 노출한다.
    /// - 관계 상태 대기나 긴 실행 분기가 필요한 무기에서 프리팹 구성 누락을 미리 경고하게 만든다.
    /// </summary>
    public virtual Type ExpectedExecutorType => null;

    public bool SupportsLoadout(WeaponAbilityLoadout loadout)
    {
        return loadout != null && ExpectedLoadoutType.IsInstanceOfType(loadout);
    }

    public abstract bool TrySelectAbility(
        in WeaponSelectionContext context,
        WeaponAbilityLoadout loadout,
        out AbilityDefinition ability);
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 무기 전용 AbilityLoadout 자산이 따라야 할 최소 공통 계약을 정의한다.
/// - selector와 ownership binder가 구체 WAL 타입을 몰라도 기본 능력 조회와 공식 grant 집합 열거를 수행할 수 있게 만든다.
/// </summary>
public abstract class WeaponAbilityLoadout : ScriptableObject
{
    [Header("Selection Strategy")]
    [SerializeField] private WeaponAbilitySelectionStrategy selectionStrategy;

    public WeaponAbilitySelectionStrategy SelectionStrategy => selectionStrategy;

    /// <summary>
    /// 책임 :
    /// - 이 loadout이 정상 동작할 때 슬롯이 소유해야 하는 runtime data 타입을 editor/validation 계층에 알려준다.
    /// - 인벤토리 persistent state 팩토리가 전용 data를 빠뜨린 경우를 authoring 단계에서 빠르게 드러내게 한다.
    /// </summary>
    public virtual Type ExpectedRuntimeDataType => typeof(WeaponRuntimeData);

    /// <summary>
    /// 책임 :
    /// - 이 loadout이 시간 경과나 창 만료 규칙을 위해 요구하는 runtime processor 타입을 editor/validation 계층에 알려준다.
    /// - processor 팩토리 누락 때문에 비활성 슬롯 상태 변화가 멈추는 실수를 inspector 단계에서 먼저 경고하게 한다.
    /// </summary>
    public virtual Type ExpectedRuntimeProcessorType => null;

    public abstract AbilityDefinition GetDefaultAbility(WeaponAbilitySlot slot);

    public abstract IEnumerable<AbilityDefinition> EnumerateGrantedAbilities();

    /// <summary>
    /// 책임 :
    /// - WAL authoring 시점에 바로 확인할 수 있는 기본 검증 결과를 제공한다.
    /// - 전용 WAL이 자기 전략/필수 참조 조건을 추가 검증할 수 있는 확장 지점을 연다.
    /// </summary>
    public IEnumerable<string> GetValidationErrors()
    {
        if (selectionStrategy == null)
            yield return "Selection Strategy가 비어 있습니다.";
        else if (!selectionStrategy.SupportsLoadout(this))
            yield return $"{selectionStrategy.name} 전략은 {selectionStrategy.ExpectedLoadoutType.Name} 타입 WAL을 기대합니다.";

        foreach (string error in EnumerateCustomValidationErrors())
            yield return error;
    }

    protected virtual IEnumerable<string> EnumerateCustomValidationErrors()
    {
        yield break;
    }
}

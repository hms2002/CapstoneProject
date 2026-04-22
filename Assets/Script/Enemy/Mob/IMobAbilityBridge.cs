using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - AI/FSM 계층이 AbilitySystem과 TagSystem의 구체 구현을 직접 모르고도 능력 실행 상태를 조회하고 제어하게 한다.
/// - 보스, 일반 몬스터, BT가 공통으로 기대할 수 있는 최소 ASC 상호작용 계약을 제공한다.
/// - 현재 상태 때문에 능력 실행 커밋이 금지되어야 하는지도 얕게 노출한다.
/// </summary>
public interface IAIAbilityBridge
{
    bool IsAbilityExecutionBusy { get; }
    bool IsAbilityExecutionSuppressed { get; }
    bool TryStartAbility(AbilityDefinition ability, GameObject explicitTarget = null);
    void CancelActiveAbility(bool force);
    bool HasStateTag(GameplayTag tag);
}

/// <summary>
/// 책임 :
/// - 몬스터 전용 ability helper가 ASC/태그 시스템의 보조 유틸리티에 접근할 때 사용하는 한정된 계약을 제공한다.
/// - BT/FSM가 직접 알 필요 없는 쿨다운, 상태 태그 변경, 실행 컨텍스트 조회를 bridge 본체와 분리해 helper 전용 문맥으로 고정한다.
/// </summary>
public interface IMobAbilityHelperAccess
{
    float GetCooldownRemaining(AbilityDefinition ability);
    bool TrySetCooldownRemaining(AbilityDefinition ability, float seconds);
    bool TryAddStateTag(GameplayTag tag, int count = 1);
    bool TryRemoveStateTag(GameplayTag tag, int count = 1);
    bool TryGetAbilityExecutionContext(AbilityDefinition ability, out AbilitySystem system, out AbilitySpec spec);
}

/// <summary>
/// 책임 :
/// - 일반 몬스터 AI가 AbilitySystem 세부 구현을 직접 모르고도 능력 실행과 취소를 요청하게 한다.
/// - 패턴 runner의 실행 상태까지 포함한 공통 busy 상태를 제공해 UpdateAttack 판단을 단순화한다.
/// </summary>
public interface IMobAbilityBridge : IAIAbilityBridge
{
    bool TryBeginRunner(IMobPatternRunner runner);
    void EndRunner(IMobPatternRunner runner);
}

using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 일반 몬스터 AI가 AbilitySystem 세부 구현을 직접 모르고도 능력 실행과 취소를 요청하게 한다.
/// - 패턴 runner의 실행 상태까지 포함한 공통 busy 상태를 제공해 UpdateAttack 판단을 단순화한다.
/// </summary>
public interface IMobAbilityBridge
{
    bool IsAbilityExecutionBusy { get; }
    bool TryStartAbility(AbilityDefinition ability, GameObject explicitTarget = null);
    void CancelActiveAbility(bool force);
    bool TryBeginRunner(IMobPatternRunner runner);
    void EndRunner(IMobPatternRunner runner);
}

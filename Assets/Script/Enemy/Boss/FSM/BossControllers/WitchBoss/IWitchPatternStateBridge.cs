using UnityGAS.Sample;

/// <summary>
/// 책임 :
/// - 마녀 보스 전용 FSM state가 구체 Witch 구현 세부사항 대신 패턴 실행 계약만 보도록 중간 브리지를 제공한다.
/// - 추후 runner 도입 시 state는 이 계약만 유지하고, 실제 실행 경로는 Witch 내부에서 교체할 수 있게 한다.
/// </summary>
public interface IWitchPatternStateBridge
{
    bool TryBeginExtinguishPattern(AbilityLogic_WitchExtinguishCandle logic, float warningTimeSeconds, out float resolvedDurationSeconds);
    void CompleteExtinguishPattern();
    void CancelExtinguishPattern();
    bool TryBeginNormalAttack1Pattern(AbilityLogic_WitchNormalAttack1 logic, out float resolvedDurationSeconds);
    bool TryBeginRetreatPattern(AbilityLogic_WitchRetreatToCandle logic);
}

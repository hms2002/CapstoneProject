/// <summary>
/// 책임 :
/// - 몬스터별 전투 문법 계층이 현재 상태에서 실행할 공격 요청을 구성하는 공통 창구를 제공한다.
/// - FSM AttackState가 공격 선택과 문맥 구성은 몬스터 helper에 위임하고, 상태 생명주기만 관리하게 돕는다.
/// </summary>
public interface IMobAttackDecisionSource
{
    bool TryBuildAttackRequest(out MobAttackRequest request);
    void OnAttackStateEntered(MobAttackRequest request);
    void OnAttackStateExited(MobAttackRequest request, bool wasCancelled);
}

/// <summary>
/// 책임 :
/// - 몬스터별 공격 decision source가 공통 AttackState 대신 자기 전용 공격 상태를 선택할 수 있는 확장 지점을 제공한다.
/// - 공통 FSM 엔진은 유지한 채, 특수 몬스터만 몬스터별 상태 집합을 소유하게 돕는다.
/// </summary>
public interface IMobAttackStateResolver
{
    bool TryCreateAttackState(MobAttackRequest request, out IMobState attackState);
}

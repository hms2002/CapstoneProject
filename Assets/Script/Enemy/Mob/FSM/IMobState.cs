/// <summary>
/// 책임 :
/// - 일반 몬스터 FSM 상태가 공통 생명주기(Enter / Tick / Exit)를 동일한 형태로 구현하게 한다.
/// - 상태 전이 관리자와 상태 구현 사이의 최소 계약을 제공해 몬스터별 상태 클래스를 단순화한다.
/// </summary>
public interface IMobState
{
    void Enter(MobStateMachine stateMachine, MobAIContext context);
    void Tick(MobStateMachine stateMachine, MobAIContext context);
    void Exit(MobStateMachine stateMachine, MobAIContext context);
}


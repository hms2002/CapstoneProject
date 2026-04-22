/// <summary>
/// 책임 :
/// - 일반 몬스터의 비전투/대기 상태를 표현하고, 타깃 감지와 즉시 공격 가능 여부를 확인한다.
/// - 감지 대상이 생기면 Chase 또는 Attack으로 넘겨 상위 FSM의 첫 진입 관문 역할을 맡는다.
/// </summary>
public sealed class MobIdleState : IMobState
{
    public void Enter(MobStateMachine stateMachine, MobAIContext context)
    {
    }

    public void Tick(MobStateMachine stateMachine, MobAIContext context)
    {
        if (MobStateTransitionUtility.TryHandleStaggerTransition(stateMachine, context))
            return;

        if (!MobStateTransitionUtility.TryHandleAttackTransition(stateMachine, context))
        {
            if (context != null && context.CanUseChaseState())
                stateMachine.ChangeState(new MobChaseState(), context);
        }
    }

    public void Exit(MobStateMachine stateMachine, MobAIContext context)
    {
    }
}

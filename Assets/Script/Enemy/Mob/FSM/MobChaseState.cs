/// <summary>
/// 책임 :
/// - 일반 몬스터가 감지한 타깃을 추적하는 상태를 표현하고, 이동은 chase intent에 맡긴 채 전이만 관리한다.
/// - 공격 가능 여부를 우선 평가하고, 추적 대상이 사라지면 Idle로 되돌아간다.
/// </summary>
public sealed class MobChaseState : IMobState
{
    public void Enter(MobStateMachine stateMachine, MobAIContext context)
    {
        context?.ChaseIntent?.StartChase();
    }

    public void Tick(MobStateMachine stateMachine, MobAIContext context)
    {
        if (MobStateTransitionUtility.TryHandleStaggerTransition(stateMachine, context))
            return;

        if (MobStateTransitionUtility.TryHandleAttackTransition(stateMachine, context))
            return;

        if (context == null || !context.HasDetectedTarget())
            stateMachine.ChangeState(new MobIdleState(), context);
    }

    public void Exit(MobStateMachine stateMachine, MobAIContext context)
    {
        context?.ChaseIntent?.StopChase();
    }
}

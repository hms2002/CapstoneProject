/// <summary>
/// 책임 :
/// - helper가 준비한 공격 요청을 bridge를 통해 한 번 시작하고, 실행이 끝날 때까지 상태를 유지한다.
/// - 공격 종류와 세부 문맥은 몰라도 되며, AttackState 생명주기 동안 실행 시작/종료 훅만 decision source에 전달한다.
/// </summary>
public sealed class MobAttackState : IMobState
{
    private readonly MobAttackRequest request;
    private bool startSucceeded;
    private bool cancelledByLostTarget;

    public MobAttackState(MobAttackRequest request)
    {
        this.request = request;
    }

    public void Enter(MobStateMachine stateMachine, MobAIContext context)
    {
        if (context == null || context.AttackDecisionSource == null || context.AbilityBridge == null)
        {
            startSucceeded = false;
            return;
        }

        context.ChaseIntent?.StopChase();
        context.AttackDecisionSource.OnAttackStateEntered(request);
        startSucceeded = context.AbilityBridge.TryStartAbility(request.Ability, request.ExplicitTarget);

        if (!startSucceeded)
            stateMachine.ChangeState(MobStateTransitionUtility.CreatePostAttackState(context), context);
    }

    public void Tick(MobStateMachine stateMachine, MobAIContext context)
    {
        if (!startSucceeded)
            return;

        if (context == null || context.Owner == null || context.Owner.IsDead)
            return;

        if (context.IsInStaggerState())
        {
            stateMachine.ChangeState(new MobStaggerState(), context);
            return;
        }

        if (context.Owner.Target == null || !context.Owner.CanPerceiveTarget(context.Owner.Target))
        {
            cancelledByLostTarget = true;
            context.AbilityBridge?.CancelActiveAbility(true);
            stateMachine.ChangeState(MobStateTransitionUtility.CreatePostAttackState(context), context);
            return;
        }

        if (context.AbilityBridge != null && context.AbilityBridge.IsAbilityExecutionBusy)
            return;

        if (request.RecoverSeconds > 0f)
            stateMachine.ChangeState(new MobRecoverState(request.RecoverSeconds), context);
        else
            stateMachine.ChangeState(MobStateTransitionUtility.CreatePostAttackState(context), context);
    }

    public void Exit(MobStateMachine stateMachine, MobAIContext context)
    {
        context?.AttackDecisionSource?.OnAttackStateExited(request, !startSucceeded || cancelledByLostTarget);
    }
}

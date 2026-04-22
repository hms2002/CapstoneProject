using UnityEngine;

/// <summary>
/// 책임 :
/// - 일반 몬스터가 그로기/스태거 태그를 가진 동안 다른 판단을 멈추고 제압 상태를 유지한다.
/// - 실제 스태거 게이지와 그로기 효과 적용은 기존 전투 시스템이 담당하고, FSM은 태그 기반 전이만 소비한다.
/// </summary>
public sealed class MobStaggerState : IMobState
{
    public void Enter(MobStateMachine stateMachine, MobAIContext context)
    {
        if (context?.Owner != null)
            Debug.Log($"[MobStaggerState] Enter owner={context.Owner.name}");

        context?.PerformSuppressionCleanup();
    }

    public void Tick(MobStateMachine stateMachine, MobAIContext context)
    {
        if (context == null || context.Owner == null || context.Owner.IsDead)
            return;

        if (context.IsInStaggerState())
            return;

        stateMachine.ChangeState(MobStateTransitionUtility.CreatePostAttackState(context), context);
    }

    public void Exit(MobStateMachine stateMachine, MobAIContext context)
    {
        if (context?.Owner != null)
            Debug.Log($"[MobStaggerState] Exit owner={context.Owner.name}");
    }
}

/// <summary>
/// 책임 :
/// - 일반 몬스터가 사망한 뒤에도 FSM 관점에서 명시적인 터미널 상태를 유지하게 한다.
/// - 죽음 진입 시점의 공통 cleanup이 끝난 뒤 더 이상의 전투 판단이나 상태 전이가 일어나지 않도록 막는다.
/// </summary>
public sealed class MobDeathState : IMobState
{
    public void Enter(MobStateMachine stateMachine, MobAIContext context)
    {
        context?.PerformFailSafeCleanup();
    }

    public void Tick(MobStateMachine stateMachine, MobAIContext context)
    {
    }

    public void Exit(MobStateMachine stateMachine, MobAIContext context)
    {
    }
}

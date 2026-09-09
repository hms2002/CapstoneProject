using UnityEngine;

/// <summary>
/// 책임 :
/// - 일반 몬스터 FSM 상태들이 공통으로 쓰는 얕은 전이 규칙을 모아 중복 조건식을 줄인다.
/// - 공격 요청 생성 여부만 판정하고, 실제 공격 종류와 문맥 해석은 decision source에 계속 위임한다.
/// </summary>
public static class MobStateTransitionUtility
{
    public static bool TryHandleStaggerTransition(MobStateMachine stateMachine, MobAIContext context)
    {
        if (context == null || context.Owner == null || context.Owner.IsDead)
            return false;

        if (!context.IsInStaggerState())
            return false;

        Debug.Log($"[MobStateTransitionUtility] Groggy detected. owner={context.Owner.name}");
        stateMachine.ChangeState(new MobStaggerState(), context);
        return true;
    }

    public static bool TryHandleAttackTransition(MobStateMachine stateMachine, MobAIContext context)
    {
        if (context == null || context.Owner == null || context.Owner.IsDead)
            return false;

        if (!context.HasDetectedTarget())
            return false;

        if (context.AbilityBridge == null || context.AttackDecisionSource == null)
            return false;

        if (context.AbilityBridge.IsAbilityExecutionBusy)
            return false;

        if (!context.ConsumeAttackThinkBudget())
            return false;

        if (!context.AttackDecisionSource.TryBuildAttackRequest(out MobAttackRequest request))
            return false;

        if (!request.IsValid)
            return false;

        context.Owner.LogFsmDebug($"공격 요청 생성 성공. ability={(request.Ability != null ? request.Ability.name : "null")}, explicitTarget={(request.ExplicitTarget != null ? request.ExplicitTarget.name : "null")}");

        if (context.AttackDecisionSource is IMobAttackStateResolver resolver &&
            resolver.TryCreateAttackState(request, out IMobState customAttackState) &&
            customAttackState != null)
        {
            stateMachine.ChangeState(customAttackState, context);
            return true;
        }

        stateMachine.ChangeState(new MobAttackState(request), context);
        return true;
    }

    public static IMobState CreatePostAttackState(MobAIContext context)
    {
        if (context != null && context.CanUseChaseState())
            return new MobChaseState();

        return new MobIdleState();
    }

    /// <summary>
    /// 책임:
    /// - 공격 종료 후 몬스터별 후딜 정책에 따라 Recover 상태를 거칠지 결정한다.
    /// - recoverSeconds가 0인 공격도 Mob 공통 후딜 보너스를 받을 수 있게 후처리 진입점을 단일화한다.
    /// </summary>
    public static IMobState CreateRecoverOrPostAttackState(MobAIContext context, float recoverSeconds)
    {
        if (context != null &&
            context.Owner != null &&
            context.Owner.ShouldUsePostAttackRecoverState(recoverSeconds))
        {
            return new MobRecoverState(recoverSeconds);
        }

        if (recoverSeconds > 0f)
            return new MobRecoverState(recoverSeconds);

        return CreatePostAttackState(context);
    }
}

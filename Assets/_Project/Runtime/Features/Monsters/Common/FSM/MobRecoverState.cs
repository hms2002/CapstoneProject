using UnityEngine;

/// <summary>
/// 책임 :
/// - 공격 실행이 끝난 뒤 몬스터가 잠깐 다음 결정을 미루는 AI 후딜 상태를 표현한다.
/// - AbilitySystem의 recoveryTime과 별개로, 몬스터 전투 리듬용 회복 시간을 FSM 차원에서 관리한다.
/// </summary>
public sealed class MobRecoverState : IMobState
{
    private readonly float recoverSeconds;
    private float recoverEndTime;

    public MobRecoverState(float recoverSeconds)
    {
        this.recoverSeconds = Mathf.Max(0f, recoverSeconds);
    }

    public void Enter(MobStateMachine stateMachine, MobAIContext context)
    {
        UnityGAS.AbilitySystem abilitySystem = context != null && context.Owner != null
            ? context.Owner.GetComponent<UnityGAS.AbilitySystem>()
            : null;
        float scaledRecoverSeconds = UnityGAS.CombatTimingService.ScaleSeconds(
            abilitySystem,
            recoverSeconds,
            UnityGAS.CombatTimingSlot.AttackRecovery);
        recoverEndTime = Time.time + scaledRecoverSeconds;
    }

    public void Tick(MobStateMachine stateMachine, MobAIContext context)
    {
        if (context == null || context.Owner == null || context.Owner.IsDead)
            return;

        if (MobStateTransitionUtility.TryHandleStaggerTransition(stateMachine, context))
            return;

        if (Time.time < recoverEndTime)
            return;

        stateMachine.ChangeState(MobStateTransitionUtility.CreatePostAttackState(context), context);
    }

    public void Exit(MobStateMachine stateMachine, MobAIContext context)
    {
    }
}

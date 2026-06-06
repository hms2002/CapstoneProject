using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 무기/입력 계층이 AbilitySystem 구체 API를 직접 많이 알지 않도록 실행 경계를 감싼다.
/// - 현재 단계에서는 ability 실행 요청, 다음 실행 가능 시간 조회, gameplay event 전송만 노출한다.
/// - 이후 무기 전용 취소 정책이나 전용 trace/logging이 필요해져도 이 계층 안에서 확장할 수 있게 한다.
/// </summary>
public sealed class WeaponAbilityBridge
{
    private readonly AbilitySystem abilitySystem;
    private readonly WeaponExecutorRunner executorRunner;

    public WeaponAbilityBridge(AbilitySystem abilitySystem, WeaponExecutorRunner executorRunner = null)
    {
        this.abilitySystem = abilitySystem;
        this.executorRunner = executorRunner;
    }

    /// <summary>
    /// 책임 : 무기 입력 계층이 현재 ASC busy 상태를 조회하는 최소 창구를 제공한다.
    /// </summary>
    public bool IsBusy => abilitySystem != null && abilitySystem.IsBusy;

    /// <summary>
    /// 책임 : 선택된 weapon ability 실행을 ASC에 위임한다.
    /// </summary>
    public bool TryActivate(AbilityDefinition ability, GameObject explicitTarget = null)
    {
        if (ability == null || abilitySystem == null)
            return false;

        return abilitySystem.TryActivateAbility(ability, explicitTarget);
    }

    /// <summary>
    /// 책임 : 현재 ability가 다시 발동 가능해질 때까지의 남은 시간을 질의한다.
    /// 자동 공격 같은 반복 입력에서 재시도 타이밍 계산에 사용한다.
    /// </summary>
    public float GetNextActivationRemaining(AbilityDefinition ability)
    {
        if (ability == null || abilitySystem == null)
            return 0f;

        return abilitySystem.GetNextActivationRemaining(ability);
    }

    /// <summary>
    /// 책임 : 긴 실행이 필요한 무기 액션을 현재 runner를 통해 시작하는 최소 창구를 제공한다.
    /// </summary>
    public void StartExecutor(IWeaponAbilityExecutor executor, in WeaponAbilityExecutionContext context)
    {
        executorRunner?.StartExecutor(executor, context);
    }

    /// <summary>
    /// 책임 : 현재 활성 무기 executor를 정상 취소 경로로 종료시킨다.
    /// </summary>
    public void CancelActiveExecutor()
    {
        executorRunner?.CancelActiveExecutor();
    }

    /// <summary>
    /// 책임 : 현재 활성 무기 executor를 강제 종료 경로로 정리한다.
    /// </summary>
    public void ForceStopActiveExecutor()
    {
        ForceStopActiveExecutor(WeaponExecutorEndReason.Forced);
    }

    /// <summary>
    /// 책임 : 현재 활성 무기 executor를 종료 사유와 함께 강제 종료 경로로 정리한다.
    /// </summary>
    public void ForceStopActiveExecutor(WeaponExecutorEndReason reason)
    {
        executorRunner?.ForceStopActiveExecutor(reason);
    }

    /// <summary>
    /// 책임 : 현재 긴 실행 executor가 살아 있는지 무기 입력 계층이 조회할 수 있게 한다.
    /// </summary>
    public bool HasActiveExecutor => executorRunner != null && executorRunner.HasActiveExecutor;

    /// <summary>
    /// 책임 : 무기 입력과 연계된 gameplay event를 ASC 이벤트 채널로 전달한다.
    /// </summary>
    public void SendGameplayEvent(GameplayTag tag)
    {
        if (tag == null || abilitySystem == null)
            return;

        abilitySystem.SendGameplayEvent(tag);
    }
}

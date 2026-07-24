using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 긴 실행 능력이 공통 생명주기(Begin, Cancel, ForceStop, Complete, Cleanup)를 같은 규약으로 따르게 하는 베이스를 제공한다.
/// - cleanup 호출 책임을 베이스가 강제해 개별 executor가 종료 경로마다 정리를 빼먹지 않게 만든다.
/// </summary>
public abstract class WeaponAbilityExecutor : MonoBehaviour, IWeaponAbilityExecutor
{
    private WeaponAbilityExecutionContext cachedContext;

    public bool IsRunning { get; private set; }

    public event Action<IWeaponAbilityExecutor, WeaponExecutorEndReason> ExecutionEnded;

    protected WeaponAbilityExecutionContext Context => cachedContext;

    public void Begin(in WeaponAbilityExecutionContext context)
    {
        if (IsRunning)
            ForceStop();

        cachedContext = context;
        IsRunning = true;
        OnBegin(context);
    }

    public void Cancel()
    {
        if (!IsRunning)
            return;

        OnCancelRequested();
        FinalizeExecution(WeaponExecutorEndReason.Cancelled);
    }

    public void ForceStop(WeaponExecutorEndReason reason = WeaponExecutorEndReason.Forced)
    {
        if (!IsRunning)
            return;

        OnForceStopRequested(reason);
        FinalizeExecution(reason);
    }

    public virtual void HandleGameplayEvent(GameplayTag tag, in AbilityEventData data)
    {
    }

    /// <summary>
    /// 책임 :
    /// - 정상 완료 경로가 끝났을 때 베이스가 정리와 종료 이벤트 발행을 동일한 방식으로 처리하게 한다.
    /// - 구현체는 Complete만 호출하면 이후 Cleanup과 runner 정리까지 자동으로 따라오게 된다.
    /// </summary>
    protected void Complete()
    {
        if (!IsRunning)
            return;

        OnComplete();
        FinalizeExecution(WeaponExecutorEndReason.Completed);
    }

    protected abstract void OnBegin(in WeaponAbilityExecutionContext context);

    protected virtual void OnCancelRequested()
    {
    }

    protected virtual void OnForceStopRequested(WeaponExecutorEndReason reason)
    {
    }

    protected virtual void OnComplete()
    {
    }

    protected virtual void OnBeforeCleanup(WeaponExecutorEndReason reason)
    {
    }

    protected virtual void OnAfterCleanup(WeaponExecutorEndReason reason)
    {
    }

    /// <summary>
    /// 책임 :
    /// - 취소/강제 종료/정상 완료 어느 경로로 끝나더라도 lingering hitbox, 임시 참조, 연출 상태를 정리하는 훅을 제공한다.
    /// - 외부 공개 API가 아니라 베이스 내부 생명주기에서만 호출되어 종료 규칙이 흐트러지지 않게 만든다.
    /// </summary>
    protected virtual void Cleanup(WeaponExecutorEndReason reason)
    {
    }

    private void FinalizeExecution(WeaponExecutorEndReason reason)
    {
        if (!IsRunning)
            return;

        IsRunning = false;

        try
        {
            OnBeforeCleanup(reason);
            Cleanup(reason);
            OnAfterCleanup(reason);
        }
        finally
        {
            ExecutionEnded?.Invoke(this, reason);
        }
    }
}

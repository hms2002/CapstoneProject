using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 현재 활성 무기 executor 하나를 소유하고 시작/취소/강제 종료/종료 감시를 중앙에서 관리한다.
/// - relay가 전달한 gameplay event를 현재 활성 executor에만 흘려 보내 executor가 직접 ASC를 구독하지 않게 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WeaponExecutorRunner : MonoBehaviour, IAbilityGameplayEventListener
{
    [SerializeField] private AbilityGameplayEventRelay gameplayEventRelay;

    private IWeaponAbilityExecutor activeExecutor;

    public bool HasActiveExecutor => activeExecutor != null && activeExecutor.IsRunning;

    private void Awake()
    {
        if (gameplayEventRelay == null)
            gameplayEventRelay = GetComponent<AbilityGameplayEventRelay>();
    }

    private void OnEnable()
    {
        gameplayEventRelay?.Register(this);
    }

    private void OnDisable()
    {
        gameplayEventRelay?.Unregister(this);
        ForceStopActiveExecutor(WeaponExecutorEndReason.OwnerDisabled);
    }

    public void StartExecutor(IWeaponAbilityExecutor executor, in WeaponAbilityExecutionContext context)
    {
        if (executor == null)
            return;

        if (ReferenceEquals(activeExecutor, executor) && activeExecutor.IsRunning)
            activeExecutor.ForceStop(WeaponExecutorEndReason.Forced);
        else
            ForceStopActiveExecutor(WeaponExecutorEndReason.Forced);

        activeExecutor = executor;
        activeExecutor.ExecutionEnded += HandleExecutionEnded;
        activeExecutor.Begin(context);
    }

    public void CancelActiveExecutor()
    {
        if (activeExecutor == null)
            return;

        IWeaponAbilityExecutor executor = activeExecutor;
        ClearActiveExecutor(executor);
        executor.Cancel();
    }

    public void ForceStopActiveExecutor()
    {
        ForceStopActiveExecutor(WeaponExecutorEndReason.Forced);
    }

    public void ForceStopActiveExecutor(WeaponExecutorEndReason reason)
    {
        if (activeExecutor == null)
            return;

        IWeaponAbilityExecutor executor = activeExecutor;
        ClearActiveExecutor(executor);
        executor.ForceStop(reason);
    }

    public T GetActiveExecutor<T>() where T : class, IWeaponAbilityExecutor
    {
        return activeExecutor as T;
    }

    public void HandleGameplayEvent(GameplayTag tag, in AbilityEventData data)
    {
        if (activeExecutor == null)
            return;

        if (!activeExecutor.IsRunning)
        {
            ClearActiveExecutor(activeExecutor);
            return;
        }

        activeExecutor.HandleGameplayEvent(tag, data);
    }

    private void HandleExecutionEnded(IWeaponAbilityExecutor executor, WeaponExecutorEndReason reason)
    {
        if (executor == null)
            return;

        ClearActiveExecutor(executor);
    }

    private void ClearActiveExecutor(IWeaponAbilityExecutor executor)
    {
        if (executor == null)
            return;

        executor.ExecutionEnded -= HandleExecutionEnded;

        if (ReferenceEquals(activeExecutor, executor))
            activeExecutor = null;
    }
}

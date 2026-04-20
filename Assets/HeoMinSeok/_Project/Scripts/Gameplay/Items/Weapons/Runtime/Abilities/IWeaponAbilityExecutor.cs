using System;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 무기 능력의 긴 실행 구간을 시작/취소/강제 종료/이벤트 반응으로 운영하는 공용 계약을 정의한다.
/// - selector/runtime state가 아닌 "시간에 걸쳐 진행되는 액션"을 runner가 같은 방식으로 다룰 수 있게 만든다.
/// </summary>
public interface IWeaponAbilityExecutor
{
    bool IsRunning { get; }

    event Action<IWeaponAbilityExecutor, WeaponExecutorEndReason> ExecutionEnded;

    void Begin(in WeaponAbilityExecutionContext context);

    void Cancel();

    void ForceStop(WeaponExecutorEndReason reason = WeaponExecutorEndReason.Forced);

    void HandleGameplayEvent(GameplayTag tag, in AbilityEventData data);
}

/// <summary>
/// 책임 :
/// - 무기 executor가 어떤 이유로 종료됐는지를 공통 enum으로 고정한다.
/// - cleanup, 디버그, 후속 상태 전이에서 종료 맥락을 문자열 대신 안전한 값으로 공유하게 만든다.
/// </summary>
public enum WeaponExecutorEndReason
{
    Completed = 0,
    Cancelled = 1,
    Forced = 2,
    Timeout = 3,
    TargetLost = 4,
    WeaponSwapped = 5,
    SceneChanged = 6,
    OwnerDisabled = 7
}

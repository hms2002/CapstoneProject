/// <summary>
/// 책임 : 런 종료 원인을 저장/전환/보상/대사 조건에서 공유하는 안정 enum으로 정의한다.
/// </summary>
public enum RunEndReason
{
    None,
    Victory,
    Defeat,
    TimeOver,
    Abort
}

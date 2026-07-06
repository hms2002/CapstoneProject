/// <summary>
/// 책임 : 씬 포털과 런 진행 시스템이 공유하는 전환 종류를 안정 enum으로 정의한다.
/// </summary>
public enum TransitionType
{
    None,
    HubToRunStart,
    CorridorToCorridor,
    CorridorToBoss,
    BossToCorridor,
    ReturnToHubAfterRun,
    DialogueCinematicScene
}

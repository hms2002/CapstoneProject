/// <summary>
/// 책임 :
/// - 상태 HUD 엔트리가 어떤 도메인 그룹에 속하는지 표현한다.
/// - 정렬, 레이아웃, 필터링 정책을 상태 소유 계층과 분리해 HUD 계층이 표시 규칙만 결정하게 만든다.
/// </summary>
public enum StatusHudGroup
{
    Weapon = 0,
    Buff = 1,
    Debuff = 2,
    Relic = 3,
    Interaction = 4
}

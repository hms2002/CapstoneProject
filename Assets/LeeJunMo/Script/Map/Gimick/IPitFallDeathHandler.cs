/// <summary>
/// 책임:
/// - PitFallReaction2D가 낙하 완료 후 대상별 구덩이 사망/후처리를 위임할 수 있는 선택적 확장점이다.
/// - 분열 억제처럼 일반 사망과 다른 규칙이 필요한 전투 오브젝트가 자기 규칙을 직접 처리하게 한다.
/// </summary>
public interface IPitFallDeathHandler
{
    void HandlePitFallDeath(PitFallContext context);
}

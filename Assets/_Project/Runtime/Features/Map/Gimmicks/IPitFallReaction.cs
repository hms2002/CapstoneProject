/// <summary>
/// 책임:
/// - HoleTrap 공통 낙하 파이프라인에서 대상별 시작/완료 후처리를 위임받는다.
/// - 낙하 연출과 피해 적용 순서는 PitFallExecutor가 유지하고, 대상 고유 cleanup만 구현체가 결정하게 한다.
/// </summary>
public interface IPitFallReaction
{
    bool CanReactToPitFall(HoleTrap trap);
    void OnPitFallStarted(PitFallContext context);
    void OnPitFallCompleted(PitFallContext context);
    bool UseDefaultRespawn { get; }
    bool RemoveFallingEffectOnComplete { get; }
}

/// <summary>
/// 현재 스테이지에서 런 타이머를 감소시켜야 하는지 결정합니다.
/// 전투 준비 구간이나 특수 이벤트 구간에서 타이머를 멈추는 정책을 추가하려면
/// 이 인터페이스를 구현하여 RunTimeLimitSystem에 주입하세요.
/// </summary>
public interface IStageTimerPolicy
{
    bool ShouldTick();
}

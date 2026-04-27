/// <summary>
/// 책임:
/// 원뿔/부채꼴 패턴의 판정 정보와 독립적으로 시각 연출 시작/종료 요청을 받는다.
/// </summary>
public interface IConePatternVisual2D
{
    void Play(ConePatternVisualSpec2D spec);
    void Stop();
}

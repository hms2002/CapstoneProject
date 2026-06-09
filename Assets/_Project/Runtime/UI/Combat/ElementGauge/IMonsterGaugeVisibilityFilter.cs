public interface IMonsterGaugeVisibilityFilter
{
    // 이 인터페이스의 책임:
    // 몬스터별 특수 규칙에 따라 속성 게이지 UI를 표시해도 되는지 판정한다.

    bool ShouldShowGauge();
}

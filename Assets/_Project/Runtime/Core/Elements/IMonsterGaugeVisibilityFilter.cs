/// <summary>
/// 책임 :
/// - 몬스터별 특수 규칙에 따라 속성 게이지 UI를 표시해도 되는지 판정하는 계약을 정의한다.
/// - UI 게이지 렌더러가 concrete monster 타입을 몰라도 대상 컴포넌트에 표시 허용 여부를 질의하게 한다.
/// </summary>
public interface IMonsterGaugeVisibilityFilter
{
    bool ShouldShowGauge();
}

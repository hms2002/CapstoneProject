/// <summary>
/// 책임 :
/// - RelicLogic이 현재 프리뷰 레벨 기준으로 완성한 툴팁 텍스트를 담는다.
/// - RelicDetailView는 이 DTO를 그대로 출력하고, 로직별 수치 해석 책임은 갖지 않는다.
/// </summary>
public sealed class RelicTooltipData
{
    public string effectText;
}

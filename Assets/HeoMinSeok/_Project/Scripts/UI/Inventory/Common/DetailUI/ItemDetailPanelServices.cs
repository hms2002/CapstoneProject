using System;

/// <summary>
/// 책임 :
/// - 상세 뷰가 공용 패널 기능을 직접 참조하지 않고도 필요한 콜백만 사용할 수 있게 한다.
/// - 텍스트 포맷, 용어집 표시, 헤더 레벨 suffix 갱신 같은 UI 서비스 진입점을 묶어 전달한다.
/// </summary>
public sealed class ItemDetailPanelServices
{
    public GlossaryDatabase glossary;
    public Func<string, string> formatText;
    public Action<string> showGlossary;
    public Action<string> setHeaderLevelText;
}

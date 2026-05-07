/// <summary>
/// 보스 HUD가 분리형 보스 체력 표시 정보를 읽기 위한 계약입니다.
/// </summary>
public interface IBossSplitHealthPresentation
{
    bool ShowSplitHealthPresentation { get; }
    string SplitHealthLeftLabel { get; }
    string SplitHealthRightLabel { get; }
}

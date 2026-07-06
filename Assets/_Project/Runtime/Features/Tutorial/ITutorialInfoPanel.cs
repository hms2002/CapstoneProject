/// <summary>
/// 책임 : 튜토리얼 게임플레이 흐름이 튜토리얼 안내 UI 패널을 열고 닫힘 상태를 확인하는 계약을 제공한다.
/// </summary>
public interface ITutorialInfoPanel
{
    bool IsOpen { get; }

    bool Show(TutorialInfoRequest request);
}

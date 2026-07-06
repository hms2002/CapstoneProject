/// <summary>
/// 책임 : UI 스택/입력 잠금 계층이 구체 화면 타입 없이 화면 열기, 닫기, 활성 상태를 다루게 한다.
/// </summary>
public interface IUIView
{
    void OpenUI();
    void CloseUI();
    bool IsActive { get; }
}

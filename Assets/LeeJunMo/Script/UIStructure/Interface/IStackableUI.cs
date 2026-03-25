// 2. ESC로 닫히는 '팝업(스택) UI' 전용 인터페이스
public interface IStackableUI : IUIView
{
    // true면 ESC로 닫힘, false면 ESC를 무시함 (예: 강화 연출 중, 보상 선택 강제 등)
    bool CanCloseOnEscape { get; }
}
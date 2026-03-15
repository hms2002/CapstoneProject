// 1. 모든 UI가 가져야 할 가장 기본 뼈대
public interface IUIView
{
    void OpenUI();
    void CloseUI();
    bool IsActive { get; }
}
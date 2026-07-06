/// <summary>
/// 책임 : UI 스택이 즉시 닫기 전에 화면별 닫기 연출 또는 차단 처리를 요청하게 한다.
/// </summary>
public interface ICloseRequestHandler
{
    bool TryHandleCloseRequest();
}

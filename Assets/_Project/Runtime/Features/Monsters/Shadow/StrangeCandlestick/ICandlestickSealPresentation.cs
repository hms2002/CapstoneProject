/// <summary>
/// 이 인터페이스의 책임:
/// 촛대 봉인 상태 변화에 맞춰 남은 스택 표식, 해제 연출, 해제 사운드 같은 프레젠테이션을 갱신하는 공용 계약을 제공한다.
/// CandlestickSeal이 구체 렌더링 방식이나 연출 자산을 몰라도 되게 만들어 이후 연출 교체 비용을 낮춘다.
/// </summary>
public interface ICandlestickSealPresentation
{
    void ShowSeal(int currentStacks, int maxStacks);
    void UpdateSealStacks(int currentStacks, int maxStacks);
    void PlaySealBroken();
    void HideSeal();
}

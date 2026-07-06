/// <summary>
/// 책임 : Gameplay 피격 반응 코드가 구체 스프라이트 플래시 구현 없이 피격 플래시 재생/중지를 요청하게 하는 계약이다.
/// </summary>
public interface IHitFlashController2D
{
    void PlayFlash();
    void StopFlash();
}

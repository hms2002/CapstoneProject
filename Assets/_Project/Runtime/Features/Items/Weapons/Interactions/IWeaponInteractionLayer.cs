/// <summary>
/// 책임 :
/// - 장착 중 live runtime state가 무기 간 상호작용 사실을 알릴 수 있는 추상 경계를 정의한다.
/// - runtime state가 pair rule, command routing, 기타 상호작용 구현 세부를 몰라도 되게 만드는 인터페이스다.
/// </summary>
public interface IWeaponInteractionLayer
{
    void NotifyAbilityActivated(in WeaponInteractionContext context);
}

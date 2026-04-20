/// <summary>
/// 책임 :
/// - 현재 활성 무기 인스턴스가 제공하는 WeaponAbilityRuntimeState를 외부 계층이 조회할 수 있는 최소 계약을 정의한다.
/// - 선택/입력 계층이 장착 비주얼 컨트롤러의 내부 캐시나 프리팹 구조를 직접 알지 않도록 경계를 만든다.
/// </summary>
public interface IWeaponRuntimeStateProvider
{
    WeaponAbilityRuntimeState GetCurrentWeaponRuntimeState();
}

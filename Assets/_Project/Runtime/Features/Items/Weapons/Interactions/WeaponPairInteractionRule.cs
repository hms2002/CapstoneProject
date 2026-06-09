/// <summary>
/// 책임 :
/// - 두 무기 조합의 전투 문법을 한 곳에 모아 해석하는 pair interaction rule의 공통 계약을 정의한다.
/// - coordinator가 모든 조합 세부를 직접 알지 않게 하고, 조합별 상호작용 의미 해석을 전용 클래스로 분리한다.
/// </summary>
public abstract class WeaponPairInteractionRule
{
    /// <summary>
    /// 책임 :
    /// - 현재 source/other 무기 조합이 이 rule의 적용 대상인지 빠르게 판정한다.
    /// - interaction layer가 모든 조합 규칙을 무조건 실행해보지 않고, 해당 pair에 맞는 rule만 좁혀서 해석하게 만든다.
    /// </summary>
    public abstract bool SupportsPair(WeaponDefinition sourceWeapon, WeaponDefinition otherWeapon);

    public abstract bool TryHandleAbilityActivated(
        in WeaponInteractionContext context,
        WeaponRuntimeCoordinator coordinator);
}

using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 무기 고유 런타임 상태(자세, 차지, 연계 정보 등)의 소유 경계를 정의한다.
/// - 현재 단계에서는 입력 슬롯별 ability 선택을 선택적으로 override할 수 있는 훅만 제공한다.
/// - GAS가 무기 세부 상태를 직접 알지 않도록, 무기 상태는 이 계층에서만 해석되게 만든다.
/// </summary>
public abstract class WeaponAbilityRuntimeState : MonoBehaviour
{
    /// <summary>
    /// 책임 :
    /// - 장착 무기가 바뀔 때 무기 상태가 초기화/이관/정리되어야 하는 지점을 제공한다.
    /// - 기본 구현은 아무 작업도 하지 않으며, 복잡한 무기만 override 한다.
    /// </summary>
    public virtual void HandleEquippedWeaponChanged(WeaponDefinition previousWeapon, WeaponDefinition newWeapon)
    {
    }

    /// <summary>
    /// 책임 :
    /// - 현재 무기 상태를 기준으로 입력 슬롯이 실행할 ability를 바꿔야 할 때 선택 결과를 돌려준다.
    /// - false를 반환하면 상위 선택기는 WeaponDefinition의 기본 슬롯 ability를 그대로 사용한다.
    /// </summary>
    public virtual bool TrySelectAbility(
        WeaponDefinition weapon,
        WeaponAbilitySlot slot,
        out AbilityDefinition ability)
    {
        ability = null;
        return false;
    }

    /// <summary>
    /// 책임 :
    /// - 무기 입력이 실제로 성공 발동된 뒤에만 필요한 런타임 상태 전이를 수행한다.
    /// - 선택 결과를 바꾸는 토글, 콤보 인덱스 증가, 차지 소비처럼 "성공한 실행 이후"에만 반영해야 하는 상태를 갱신한다.
    /// </summary>
    public virtual void HandleAbilityActivated(
        WeaponDefinition weapon,
        WeaponAbilitySlot slot,
        AbilityDefinition activatedAbility)
    {
    }

    /// <summary>
    /// 책임 :
    /// - 현재 장착 무기에 연관된 GameplayEvent를 무기 런타임 상태가 소비할 수 있게 하는 공식 훅을 제공한다.
    /// - 적중 확정처럼 실행 후 발생하는 전투 결과를 무기 상태에 기록할 때 ability logic 직접 구독을 피하게 만든다.
    /// </summary>
    public virtual void HandleGameplayEvent(
        WeaponDefinition weapon,
        GameplayTag tag,
        in AbilityEventData data)
    {
    }
}

using UnityGAS;

/// <summary>
/// 플레이어의 런타임 저장/복원에 필요한 핵심 시스템 컴포넌트들을 묶어 전달하기 위한 컨텍스트
/// </summary>
public struct PlayerSystemContext
{
    public PlayerConsumableInventory consumableInventory;
    public WeaponInventory2D weaponInventory;
    public RelicInventory relicInventory;
    public AttributeSet attributeSet;
    public GameplayEffectRunner effectRunner;
    public TagSystem tagSystem;
    public AbilitySystem abilitySystem;
}
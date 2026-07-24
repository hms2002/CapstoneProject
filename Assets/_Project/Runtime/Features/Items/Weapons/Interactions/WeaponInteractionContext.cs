using UnityGAS;

/// <summary>
/// 책임 :
/// - 무기 간 상호작용 계층이 현재 무기와 반대 슬롯 무기의 상태를 함께 해석할 때 읽을 최소 문맥을 묶는다.
/// - runtime state가 pair rule의 구체 구현을 몰라도 필요한 사실만 interaction layer에 전달하게 만든다.
/// </summary>
public readonly struct WeaponInteractionContext
{
    public WeaponInventory2D Inventory { get; }
    public WeaponDefinition SourceWeapon { get; }
    public int SourceSlotIndex { get; }
    public WeaponAbilitySlot SourceSlot { get; }
    public WeaponAbilityRuntimeState SourceRuntimeState { get; }
    public WeaponRuntimeData SourceRuntimeData { get; }
    public AbilityDefinition ActivatedAbility { get; }
    public WeaponDefinition OtherWeapon { get; }
    public int OtherSlotIndex { get; }
    public WeaponRuntimeData OtherRuntimeData { get; }

    public WeaponInteractionContext(
        WeaponInventory2D inventory,
        WeaponDefinition sourceWeapon,
        int sourceSlotIndex,
        WeaponAbilitySlot sourceSlot,
        WeaponAbilityRuntimeState sourceRuntimeState,
        WeaponRuntimeData sourceRuntimeData,
        AbilityDefinition activatedAbility,
        WeaponDefinition otherWeapon,
        int otherSlotIndex,
        WeaponRuntimeData otherRuntimeData)
    {
        Inventory = inventory;
        SourceWeapon = sourceWeapon;
        SourceSlotIndex = sourceSlotIndex;
        SourceSlot = sourceSlot;
        SourceRuntimeState = sourceRuntimeState;
        SourceRuntimeData = sourceRuntimeData;
        ActivatedAbility = activatedAbility;
        OtherWeapon = otherWeapon;
        OtherSlotIndex = otherSlotIndex;
        OtherRuntimeData = otherRuntimeData;
    }
}

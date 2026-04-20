using UnityGAS;

/// <summary>
/// 책임 :
/// - 무기 선택 전략이 AbilityDefinition을 결정할 때 읽을 최소 문맥을 묶는다.
/// - 현재 슬롯과 반대 슬롯의 무기/런타임 상태를 함께 전달해 쌍무기 상호참조 선택 규칙도 selector 바깥 탐색 없이 구현하게 한다.
/// </summary>
public readonly struct WeaponSelectionContext
{
    public WeaponDefinition Weapon { get; }
    public int SlotIndex { get; }
    public WeaponAbilitySlot Slot { get; }
    public WeaponAbilityRuntimeState RuntimeState { get; }
    public WeaponRuntimeData RuntimeData { get; }
    public WeaponDefinition OtherWeapon { get; }
    public int OtherSlotIndex { get; }
    public WeaponRuntimeData OtherRuntimeData { get; }

    public WeaponSelectionContext(
        WeaponDefinition weapon,
        int slotIndex,
        WeaponAbilitySlot slot,
        WeaponAbilityRuntimeState runtimeState,
        WeaponRuntimeData runtimeData,
        WeaponDefinition otherWeapon,
        int otherSlotIndex,
        WeaponRuntimeData otherRuntimeData)
    {
        Weapon = weapon;
        SlotIndex = slotIndex;
        Slot = slot;
        RuntimeState = runtimeState;
        RuntimeData = runtimeData;
        OtherWeapon = otherWeapon;
        OtherSlotIndex = otherSlotIndex;
        OtherRuntimeData = otherRuntimeData;
    }
}

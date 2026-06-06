using UnityGAS;

/// <summary>
/// 책임 :
/// - 무기 runtime processor가 시간 경과 규칙을 적용할 때 읽을 현재 슬롯/반대 슬롯 문맥을 묶는다.
/// - processor가 인벤토리 내부 배열 구조를 직접 탐색하지 않고도 쌍무기 상태 변화 규칙을 계산하게 한다.
/// </summary>
public readonly struct WeaponRuntimeProcessContext
{
    public WeaponInventory2D Inventory { get; }
    public WeaponDefinition Weapon { get; }
    public int SlotIndex { get; }
    public WeaponRuntimeData RuntimeData { get; }
    public WeaponDefinition OtherWeapon { get; }
    public int OtherSlotIndex { get; }
    public WeaponRuntimeData OtherRuntimeData { get; }

    public WeaponRuntimeProcessContext(
        WeaponInventory2D inventory,
        WeaponDefinition weapon,
        int slotIndex,
        WeaponRuntimeData runtimeData,
        WeaponDefinition otherWeapon,
        int otherSlotIndex,
        WeaponRuntimeData otherRuntimeData)
    {
        Inventory = inventory;
        Weapon = weapon;
        SlotIndex = slotIndex;
        RuntimeData = runtimeData;
        OtherWeapon = otherWeapon;
        OtherSlotIndex = otherSlotIndex;
        OtherRuntimeData = otherRuntimeData;
    }
}

using System;

/// <summary>
/// 책임 :
/// - 인벤토리가 소유한 슬롯별 WeaponRuntimeData와 WeaponRuntimeProcessor를 묶어 전체 런타임 갱신을 조정한다.
/// - 비활성 무기를 포함한 모든 슬롯 상태에 동일한 시간 경과 규칙을 공급해 persistent state가 프리팹 생명주기와 분리되게 만든다.
/// </summary>
public sealed class WeaponRuntimeCoordinator
{
    private readonly WeaponInventory2D inventory;
    private WeaponRuntimeProcessor[] processors;

    public WeaponRuntimeCoordinator(WeaponInventory2D inventory)
    {
        this.inventory = inventory;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 슬롯 배치에 맞는 runtime processor 배열을 다시 구성한다.
    /// - 슬롯에 놓인 무기나 runtime data가 바뀐 뒤에도 processor가 같은 인덱스 규칙으로 새 상태를 바라보게 만든다.
    /// </summary>
    public void Rebuild(WeaponDefinition[] slots, WeaponRuntimeData[] runtimeSlots)
    {
        if (slots == null || runtimeSlots == null)
        {
            processors = null;
            return;
        }

        if (processors == null || processors.Length != slots.Length)
            processors = new WeaponRuntimeProcessor[slots.Length];

        for (int i = 0; i < slots.Length; i++)
            processors[i] = WeaponRuntimeProcessorFactory.CreateForWeapon(slots[i], runtimeSlots[i]);
    }

    /// <summary>
    /// 책임 :
    /// - 지정 슬롯에 연결된 runtime processor를 조회한다.
    /// - 외부 계층이 coordinator 내부 배열을 직접 만지지 않고도 현재 processor 구성을 확인하게 한다.
    /// </summary>
    public WeaponRuntimeProcessor GetProcessorInSlot(int slotIndex)
    {
        if (processors == null || slotIndex < 0 || slotIndex >= processors.Length)
            return null;

        return processors[slotIndex];
    }

    /// <summary>
    /// 책임 :
    /// - 외부 상호작용 계층이 특정 슬롯 runtime data에 안전하게 변경을 반영할 수 있는 공식 창구를 제공한다.
    /// - inventory 바깥 코드가 슬롯 배열을 직접 만지지 않고도 "올바른 owner 슬롯"에만 상태 변경을 적용하게 만든다.
    /// </summary>
    public bool TryMutateRuntimeData<TData>(int slotIndex, Action<TData> mutation)
        where TData : WeaponRuntimeData
    {
        if (inventory == null || mutation == null)
            return false;

        TData data = inventory.GetRuntimeDataInSlot(slotIndex) as TData;
        if (data == null)
            return false;

        mutation(data);
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 인벤토리 슬롯 전부에 대해 runtime processor Tick을 실행한다.
    /// - 비활성 무기 상태도 같은 프레임 시간 기준으로 감쇠/만료되게 만들어 상태 소유자가 truly inventory-owned data가 되게 한다.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (inventory == null || processors == null || deltaTime <= 0f)
            return;

        for (int i = 0; i < processors.Length; i++)
        {
            WeaponRuntimeProcessor processor = processors[i];
            WeaponDefinition weapon = inventory.GetWeaponInSlot(i);
            WeaponRuntimeData runtimeData = inventory.GetRuntimeDataInSlot(i);
            if (processor == null || weapon == null || runtimeData == null)
                continue;

            int otherSlotIndex = inventory.GetOtherSlotIndex(i);
            WeaponDefinition otherWeapon = inventory.GetWeaponInSlot(otherSlotIndex);
            WeaponRuntimeData otherRuntimeData = inventory.GetRuntimeDataInSlot(otherSlotIndex);

            var context = new WeaponRuntimeProcessContext(
                inventory,
                weapon,
                i,
                runtimeData,
                otherWeapon,
                otherSlotIndex,
                otherRuntimeData);

            processor.Tick(context, deltaTime);
        }
    }
}

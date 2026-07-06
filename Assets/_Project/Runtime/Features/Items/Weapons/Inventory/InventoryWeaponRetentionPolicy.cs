using UnityEngine;

/// <summary>
/// 책임 :
/// - 플레이어가 마지막 남은 무기를 인벤토리 밖으로 잃지 않도록 컨테이너 이동/드롭 정책을 판정한다.
/// - UI 입력과 gameplay inventory adapter가 같은 보존 규칙을 공유하게 한다.
/// </summary>
public static class InventoryWeaponRetentionPolicy
{
    public static bool WouldRemoveLastPlayerWeapon(
        IItemContainer source,
        int sourceIndex,
        IItemContainer target = null,
        int targetIndex = -1)
    {
        if (!IsPlayerWeaponContainer(source))
            return false;

        if (!IsValidIndex(source, sourceIndex))
            return false;

        if (source.Get(sourceIndex) is not WeaponDefinition)
            return false;

        if (target != null && ReferenceEquals(source, target))
            return false;

        if (target != null && IsValidIndex(target, targetIndex) && target.Get(targetIndex) is WeaponDefinition)
            return false;

        return CountWeapons(source) <= 1;
    }

    public static bool IsPlayerWeaponContainer(IItemContainer container)
    {
        return container != null && ReferenceEquals(container, ItemContainerGroupRegistry.WeaponEquip);
    }

    private static int CountWeapons(IItemContainer container)
    {
        if (container == null)
            return 0;

        int count = 0;
        for (int i = 0; i < container.SlotCount; i++)
        {
            if (container.Get(i) is WeaponDefinition)
                count++;
        }

        return count;
    }

    private static bool IsValidIndex(IItemContainer container, int index)
    {
        return container != null && index >= 0 && index < container.SlotCount;
    }
}

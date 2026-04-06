/// <summary>
/// 책임 : 인벤토리 UI가 빠른 이동 대상 컨테이너들을 공유할 수 있도록 현재 활성 그룹을 보관한다.
/// </summary>
public static class ItemContainerGroupRegistry
{
    private static IItemContainer chest;
    private static IItemContainer consumableEquip;
    private static IItemContainer weaponEquip;
    private static IItemContainer relicEquip;

    public static void SetGroup(
        IItemContainer chestContainer,
        IItemContainer consumableContainer,
        IItemContainer weaponContainer,
        IItemContainer relicContainer)
    {
        chest = chestContainer;
        consumableEquip = consumableContainer;
        weaponEquip = weaponContainer;
        relicEquip = relicContainer;
    }

    public static void Clear()
    {
        chest = null;
        consumableEquip = null;
        weaponEquip = null;
        relicEquip = null;
    }

    public static IItemContainer Chest => chest;
    public static IItemContainer ConsumableEquip => consumableEquip;
    public static IItemContainer WeaponEquip => weaponEquip;
    public static IItemContainer RelicEquip => relicEquip;
}

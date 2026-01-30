public static class ItemContainerGroupRegistry
{
    private static IItemContainer chest;
    private static IItemContainer weaponEquip;
    private static IItemContainer relicEquip;

    public static void SetGroup(IItemContainer chestContainer, IItemContainer weaponContainer, IItemContainer relicContainer)
    {
        chest = chestContainer;
        weaponEquip = weaponContainer;
        relicEquip = relicContainer;
    }

    public static void Clear()
    {
        chest = null;
        weaponEquip = null;
        relicEquip = null;
    }

    public static IItemContainer Chest => chest;
    public static IItemContainer WeaponEquip => weaponEquip;
    public static IItemContainer RelicEquip => relicEquip;
}

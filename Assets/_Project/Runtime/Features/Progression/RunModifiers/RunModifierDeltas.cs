[System.Serializable]
public struct GraveRunModifierDelta
{
    public int weaponGraveMinBonus;
    public int weaponGraveMaxBonus;
    public int relicGraveMinBonus;
    public int relicGraveMaxBonus;
    public int weaponDropMinBonus;
    public int weaponDropMaxBonus;
    public int relicDropMinBonus;
    public int relicDropMaxBonus;
    public float extraRareChance;
    public float extraEpicChance;

    public static GraveRunModifierDelta FromSave(RunModifierSaveData data)
    {
        if (data == null)
            return default;

        return new GraveRunModifierDelta
        {
            weaponGraveMinBonus = data.weaponGraveMinBonus,
            weaponGraveMaxBonus = data.weaponGraveMaxBonus != 0 ? data.weaponGraveMaxBonus : data.extraWeaponGraveCount,
            relicGraveMinBonus = data.relicGraveMinBonus,
            relicGraveMaxBonus = data.relicGraveMaxBonus != 0 ? data.relicGraveMaxBonus : data.extraRelicGraveCount,
            weaponDropMinBonus = data.weaponDropMinBonus,
            weaponDropMaxBonus = data.weaponDropMaxBonus != 0 ? data.weaponDropMaxBonus : data.extraWeaponDropCount,
            relicDropMinBonus = data.relicDropMinBonus,
            relicDropMaxBonus = data.relicDropMaxBonus != 0 ? data.relicDropMaxBonus : data.extraRelicDropCount,
            extraRareChance = data.extraRareChance,
            extraEpicChance = data.extraEpicChance
        };
    }

    public void Add(GraveRunModifierDelta other)
    {
        weaponGraveMinBonus += other.weaponGraveMinBonus;
        weaponGraveMaxBonus += other.weaponGraveMaxBonus;
        relicGraveMinBonus += other.relicGraveMinBonus;
        relicGraveMaxBonus += other.relicGraveMaxBonus;
        weaponDropMinBonus += other.weaponDropMinBonus;
        weaponDropMaxBonus += other.weaponDropMaxBonus;
        relicDropMinBonus += other.relicDropMinBonus;
        relicDropMaxBonus += other.relicDropMaxBonus;
        extraRareChance += other.extraRareChance;
        extraEpicChance += other.extraEpicChance;
    }
}

[System.Serializable]
public struct ChestRunModifierDelta
{
    public int chestWeaponMinBonus;
    public int chestWeaponMaxBonus;
    public int chestRelicMinBonus;
    public int chestRelicMaxBonus;
    public int chestRefreshCount;
    public float relicLevelBonusChance;

    public static ChestRunModifierDelta FromSave(RunModifierSaveData data)
    {
        if (data == null)
            return default;

        return new ChestRunModifierDelta
        {
            chestWeaponMinBonus = data.chestWeaponMinBonus,
            chestWeaponMaxBonus = data.chestWeaponMaxBonus,
            chestRelicMinBonus = data.chestRelicMinBonus,
            chestRelicMaxBonus = data.chestRelicMaxBonus
        };
    }

    public void Add(ChestRunModifierDelta other)
    {
        chestWeaponMinBonus += other.chestWeaponMinBonus;
        chestWeaponMaxBonus += other.chestWeaponMaxBonus;
        chestRelicMinBonus += other.chestRelicMinBonus;
        chestRelicMaxBonus += other.chestRelicMaxBonus;
        chestRefreshCount += other.chestRefreshCount;
        relicLevelBonusChance += other.relicLevelBonusChance;
    }
}

[System.Serializable]
public struct ShopRunModifierDelta
{
    public bool shopEnabled;
    public int shopSlotBonus;
    public float discountRate;
    public int shopRefreshCount;

    public void Add(ShopRunModifierDelta other)
    {
        shopEnabled |= other.shopEnabled;
        shopSlotBonus += other.shopSlotBonus;
        discountRate += other.discountRate;
        shopRefreshCount += other.shopRefreshCount;
    }
}

[System.Serializable]
public struct BossRunModifierDelta
{
    public int bossFieldHealPickupBonus;
    public int bossMagicStoneBonus;
    public int bossChestWeaponMinBonus;
    public int bossChestWeaponMaxBonus;
    public int bossChestRelicMinBonus;
    public int bossChestRelicMaxBonus;

    public ChestRunModifierDelta ToChestModifierDelta()
    {
        return new ChestRunModifierDelta
        {
            chestWeaponMinBonus = bossChestWeaponMinBonus,
            chestWeaponMaxBonus = bossChestWeaponMaxBonus,
            chestRelicMinBonus = bossChestRelicMinBonus,
            chestRelicMaxBonus = bossChestRelicMaxBonus
        };
    }

    public void Add(BossRunModifierDelta other)
    {
        bossFieldHealPickupBonus += other.bossFieldHealPickupBonus;
        bossMagicStoneBonus += other.bossMagicStoneBonus;
        bossChestWeaponMinBonus += other.bossChestWeaponMinBonus;
        bossChestWeaponMaxBonus += other.bossChestWeaponMaxBonus;
        bossChestRelicMinBonus += other.bossChestRelicMinBonus;
        bossChestRelicMaxBonus += other.bossChestRelicMaxBonus;
    }
}

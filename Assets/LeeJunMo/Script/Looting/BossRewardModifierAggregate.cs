using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct BossRewardModifierAggregate
{
    private List<BossSpecificLoot> bonusLoots;

    public ChestRunModifierDelta ChestModifierDelta { get; private set; }
    public int MagicStoneBonus { get; private set; }
    public int FieldHealPickupBonus { get; private set; }
    public IReadOnlyList<BossSpecificLoot> BonusLoots => bonusLoots;

    public static BossRewardModifierAggregate FromBossRunModifierDelta(BossRunModifierDelta delta)
    {
        var aggregate = new BossRewardModifierAggregate
        {
            ChestModifierDelta = delta.ToChestModifierDelta(),
            MagicStoneBonus = Mathf.Max(0, delta.bossMagicStoneBonus),
            FieldHealPickupBonus = Mathf.Max(0, delta.bossFieldHealPickupBonus)
        };

        return aggregate;
    }

    public void Add(BossRewardModifierAggregate other)
    {
        ChestRunModifierDelta chestDelta = ChestModifierDelta;
        chestDelta.Add(other.ChestModifierDelta);
        ChestModifierDelta = chestDelta;

        MagicStoneBonus += Mathf.Max(0, other.MagicStoneBonus);
        FieldHealPickupBonus += Mathf.Max(0, other.FieldHealPickupBonus);
        AddBonusLoots(other.BonusLoots);
    }

    public void AddChestDelta(ChestRunModifierDelta delta)
    {
        ChestRunModifierDelta chestDelta = ChestModifierDelta;
        chestDelta.Add(delta);
        ChestModifierDelta = chestDelta;
    }

    public void AddMagicStoneBonus(int amount)
    {
        MagicStoneBonus += Mathf.Max(0, amount);
    }

    public void AddFieldHealPickupBonus(int amount)
    {
        FieldHealPickupBonus += Mathf.Max(0, amount);
    }

    public void AddBonusLoots(IReadOnlyList<BossSpecificLoot> entries)
    {
        if (entries == null || entries.Count == 0)
            return;

        bonusLoots ??= new List<BossSpecificLoot>();
        for (int i = 0; i < entries.Count; i++)
        {
            BossSpecificLoot entry = entries[i];
            if (entry.item != null && entry.dropChance > 0)
                bonusLoots.Add(entry);
        }
    }

    public BossRunModifierDelta ToBossRunModifierDelta()
    {
        return new BossRunModifierDelta
        {
            bossFieldHealPickupBonus = FieldHealPickupBonus,
            bossMagicStoneBonus = MagicStoneBonus,
            bossChestWeaponMinBonus = ChestModifierDelta.chestWeaponMinBonus,
            bossChestWeaponMaxBonus = ChestModifierDelta.chestWeaponMaxBonus,
            bossChestRelicMinBonus = ChestModifierDelta.chestRelicMinBonus,
            bossChestRelicMaxBonus = ChestModifierDelta.chestRelicMaxBonus
        };
    }
}

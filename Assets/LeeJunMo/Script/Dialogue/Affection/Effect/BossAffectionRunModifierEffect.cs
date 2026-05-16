using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BossAffectionRunModifierEffect", menuName = "Affection/Effect/Boss Run Modifier")]
public sealed class BossAffectionRunModifierEffect : AffectionEffect
{
    [Header("Boss Bonus Drops")]
    [SerializeField, Min(0)] private int bossMagicStoneBonus;
    [SerializeField] private List<BossSpecificLoot> bossBonusLoots = new List<BossSpecificLoot>();

    [Header("Boss Field Heal Drop")]
    [SerializeField, Min(0)] private int bossFieldHealPickupBonus;

    [Header("Boss Chest Weapon Count Bonus")]
    [SerializeField] private int bossChestWeaponMinBonus;
    [SerializeField] private int bossChestWeaponMaxBonus;

    [Header("Boss Chest Relic Count Bonus")]
    [SerializeField] private int bossChestRelicMinBonus;
    [SerializeField] private int bossChestRelicMaxBonus;

    public BossRunModifierDelta Delta => new BossRunModifierDelta
    {
        bossFieldHealPickupBonus = bossFieldHealPickupBonus,
        bossMagicStoneBonus = bossMagicStoneBonus,
        bossChestWeaponMinBonus = bossChestWeaponMinBonus,
        bossChestWeaponMaxBonus = bossChestWeaponMaxBonus,
        bossChestRelicMinBonus = bossChestRelicMinBonus,
        bossChestRelicMaxBonus = bossChestRelicMaxBonus
    };

    public BossRewardModifierAggregate ModifierAggregate
    {
        get
        {
            var aggregate = new BossRewardModifierAggregate();
            aggregate.Add(BossRewardModifierAggregate.FromBossRunModifierDelta(Delta));
            aggregate.AddBonusLoots(bossBonusLoots);
            return aggregate;
        }
    }

    public override void Execute()
    {
        RunModifierService.Instance?.RebuildFromPurchasedUpgrades();
    }
}

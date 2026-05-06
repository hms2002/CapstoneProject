using UnityEngine;

[CreateAssetMenu(fileName = "BossAffectionRunModifierEffect", menuName = "Affection/Effect/Boss Run Modifier")]
public sealed class BossAffectionRunModifierEffect : AffectionEffect
{
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
        bossChestWeaponMinBonus = bossChestWeaponMinBonus,
        bossChestWeaponMaxBonus = bossChestWeaponMaxBonus,
        bossChestRelicMinBonus = bossChestRelicMinBonus,
        bossChestRelicMaxBonus = bossChestRelicMaxBonus
    };

    public override void Execute()
    {
        RunModifierService.Instance?.RebuildFromPurchasedUpgrades();
    }
}

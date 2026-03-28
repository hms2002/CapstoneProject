using UnityEngine;

[CreateAssetMenu(fileName = "ChestRunModifierEffect", menuName = "Upgrade/Effect/Chest Run Modifier")]
public class ChestRunModifierUpgradeEffect : RunModifierUpgradeEffectSO
{
    [Header("Chest Weapon Count Bonus")]
    [SerializeField] private int chestWeaponMinBonus;
    [SerializeField] private int chestWeaponMaxBonus;

    [Header("Chest Relic Count Bonus")]
    [SerializeField] private int chestRelicMinBonus;
    [SerializeField] private int chestRelicMaxBonus;

    protected override void ApplyModifier()
    {
        if (RunModifierService.Instance == null)
            return;

        ChestRunModifierDelta delta = new ChestRunModifierDelta
        {
            chestWeaponMinBonus = chestWeaponMinBonus,
            chestWeaponMaxBonus = chestWeaponMaxBonus,
            chestRelicMinBonus = chestRelicMinBonus,
            chestRelicMaxBonus = chestRelicMaxBonus
        };

        RunModifierService.Instance.AddChestModifier(delta);
    }
}

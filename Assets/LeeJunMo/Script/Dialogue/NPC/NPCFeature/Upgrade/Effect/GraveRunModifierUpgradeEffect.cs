using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "GraveRunModifierEffect", menuName = "Upgrade/Effect/Grave Run Modifier")]
public class GraveRunModifierUpgradeEffect : RunModifierUpgradeEffectSO
{
    [Header("Weapon Grave Count Bonus")]
    [SerializeField] private int weaponGraveMinBonus;
    [SerializeField, FormerlySerializedAs("extraWeaponGraveCount")] private int weaponGraveMaxBonus;

    [Header("Relic Grave Count Bonus")]
    [SerializeField] private int relicGraveMinBonus;
    [SerializeField, FormerlySerializedAs("extraRelicGraveCount")] private int relicGraveMaxBonus;

    [Header("Weapon Drop Count Bonus")]
    [SerializeField] private int weaponDropMinBonus;
    [SerializeField, FormerlySerializedAs("extraWeaponDropCount")] private int weaponDropMaxBonus;

    [Header("Relic Drop Count Bonus")]
    [SerializeField] private int relicDropMinBonus;
    [SerializeField, FormerlySerializedAs("extraRelicDropCount")] private int relicDropMaxBonus;

    [Header("Rarity Bonus")]
    [SerializeField] private float extraRareChance;
    [SerializeField] private float extraEpicChance;

    protected override void ApplyModifier()
    {
        if (RunModifierService.Instance == null)
            return;

        GraveRunModifierDelta delta = new GraveRunModifierDelta
        {
            weaponGraveMinBonus = weaponGraveMinBonus,
            weaponGraveMaxBonus = weaponGraveMaxBonus,
            relicGraveMinBonus = relicGraveMinBonus,
            relicGraveMaxBonus = relicGraveMaxBonus,
            weaponDropMinBonus = weaponDropMinBonus,
            weaponDropMaxBonus = weaponDropMaxBonus,
            relicDropMinBonus = relicDropMinBonus,
            relicDropMaxBonus = relicDropMaxBonus,
            extraRareChance = extraRareChance,
            extraEpicChance = extraEpicChance
        };

        RunModifierService.Instance.AddGraveModifier(delta);
    }
}

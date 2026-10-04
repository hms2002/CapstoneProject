using UnityEngine;

/// <summary>Weapon logic reads inventory gates; this adapter supplies the authored effect description.</summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Weapon Exclusive")]
public sealed class RelicLogic_WeaponExclusive : RelicLogic
{
    public override void OnEquipped(RelicContext ctx) { }
    public override void OnUnequipped(RelicContext ctx) { }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        string text = definition != null ? GameText.Asset(definition, "description", definition.description) : string.Empty;
        if (definition != null && definition.relicId == WeaponExclusiveRelics.OddIronMagazine)
            text += GameText.Format("code.reliclogic_weaponexclusive.07b203d7b2", "\n● 남은 장전 횟수: {0}회", RelicTooltipFormatter.FormatUnsignedValueToken(definition.ClampLevel(previewLevel), false));
        return new RelicTooltipData { effectText = text };
    }
}

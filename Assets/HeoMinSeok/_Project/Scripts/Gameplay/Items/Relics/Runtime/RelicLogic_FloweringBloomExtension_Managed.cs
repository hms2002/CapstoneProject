using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Flowering Bloom Extension (Managed)")]
public sealed class RelicLogic_FloweringBloomExtension_Managed : RelicLogic
{
    protected override string DefaultEffectTemplate => "개화 전용 유물\n개화 중 적 처치 시 개화 지속 시간이 {extension_seconds} 증가";

    [Tooltip("Flowering runtime checks this tag before applying kill duration extension.")]
    public GameplayTag grantedTag;

    [Tooltip("Tooltip value only. FloweringBloomData owns the actual extension amount.")]
    public float extensionSeconds = 1f;

    public override void OnEquipped(RelicContext ctx)
    {
        ApplyGrantedTag(ctx, add: true);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        ApplyGrantedTag(ctx, add: false);
    }

    public override void OnRestoreAttached(RelicContext ctx)
    {
        ApplyGrantedTag(ctx, add: true);
    }

    public override void OnRestoreDetached(RelicContext ctx)
    {
        ApplyGrantedTag(ctx, add: false);
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return BuildTemplatedTooltip(
            "개화 전용 유물\n개화 중 적 처치 시 개화 지속 시간이 {extension_seconds} 증가",
            new Dictionary<string, string>
            {
                ["extension_seconds"] = RelicTooltipFormatter.FormatSeconds(extensionSeconds),
            });
    }

    private void ApplyGrantedTag(RelicContext ctx, bool add)
    {
        if (ctx.tagSystem == null || grantedTag == null)
            return;

        if (add)
            ctx.tagSystem.AddTag(grantedTag);
        else
            ctx.tagSystem.RemoveTag(grantedTag);
    }
}

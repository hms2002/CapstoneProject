using UnityEngine;

[CreateAssetMenu(menuName = "Game/Relic Logic/Potion Recovery")]
public sealed class RelicLogic_PotionRecovery : RelicLogic
{
    [SerializeField] private ConsumableDefinition potion;
    [SerializeField, Min(1)] private int recoveryAmount = 2;

    protected override string DefaultEffectTemplate => "포션의 회복량이 1에서 2로 증가";

    public override void OnEquipped(RelicContext ctx) => Attach(ctx);
    public override void OnRestoreAttached(RelicContext ctx) => Attach(ctx);
    public override void OnUnequipped(RelicContext ctx) => Detach(ctx);
    public override void OnRestoreDetached(RelicContext ctx) => Detach(ctx);

    private void Attach(RelicContext ctx)
    {
        ctx.Get<PlayerConsumableInventory>()?.SetMinimumRestoreAmount(ctx.token, potion, recoveryAmount);
    }

    private void Detach(RelicContext ctx)
    {
        ctx.Get<PlayerConsumableInventory>()?.RemoveRestoreOverride(ctx.token);
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return new RelicTooltipData
        {
            effectText = $"포션의 회복량이 {(potion != null ? potion.RestoreAmount : 1)}에서 {recoveryAmount}로 증가"
        };
    }
}

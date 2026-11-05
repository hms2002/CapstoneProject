using UnityEngine;
using UnityGAS;

/// <summary>그림자 혼불의 작성 데이터와 token 소유 처치 이벤트 연결을 담당한다.</summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Shadow Wisp")]
public sealed class RelicLogic_ShadowWisp : RelicLogic
{
    public ChloeLanternProjectile2D projectilePrefab;
    public GameplayEffect damageEffect;
    public LayerMask targetLayers;
    public LayerMask wallLayers;
    [Min(0.1f)] public float projectileSpeedMultiplier = 1.4f;
    [Min(0.1f)] public float projectileLifetime = 5f;
    [Min(1f)] public float homingTurnRate = 1080f;

    public override void OnEquipped(RelicContext ctx) => Attach(ctx);
    public override void OnRestoreAttached(RelicContext ctx) => Attach(ctx);
    public override void OnRestoreDetached(RelicContext ctx) => OnUnequipped(ctx);

    public override void OnUnequipped(RelicContext ctx)
        => ctx.Get<RelicProcManager>()?.UnregisterAll(ctx.token);

    private void Attach(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null || ctx.abilitySystem == null ||
            ctx.abilitySystem.KillConfirmedTag == null || projectilePrefab == null || damageEffect == null)
            return;

        RelicProcManager manager = ctx.Get<RelicProcManager>();
        if (manager == null) manager = ctx.owner.AddComponent<RelicProcManager>();
        manager.UnregisterAll(ctx.token);
        ShadowWispController controller = ctx.Get<ShadowWispController>();
        if (controller == null) controller = ctx.owner.AddComponent<ShadowWispController>();
        controller.Attach(ctx.token, ctx.abilitySystem, this);
        manager.Register(new KillProc(ctx, controller));
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return new RelicTooltipData
        {
            effectText = GameText.Get("relic.shadow_wisp.effect",
                "● 적 처치 시 그 자리에서 다른 적을 추적하는 혼불 {{val:1발}} 생성\n" +
                "● 화면 안에서 벽에 가리지 않은 가장 가까운 적을 조준\n" +
                "● 생성 시점 공격력의 {{val:100%}} 피해 · 치명타와 속성 효과 없음\n" +
                "● 혼불의 처치로는 재발동하지 않음")
        };
    }

    private sealed class KillProc : IRelicProc
    {
        private readonly RelicContext context;
        private readonly ShadowWispController controller;
        public Object Token => context.token;

        public KillProc(RelicContext context, ShadowWispController controller)
        {
            this.context = context;
            this.controller = controller;
        }

        public void Handle(GameplayTag tag, AbilityEventData data)
        {
            if (context.abilitySystem != null && tag == context.abilitySystem.KillConfirmedTag &&
                data.Instigator == context.owner && controller != null)
                controller.OnOwnerKill(Token, data);
        }

        public void Tick(float deltaTime) { }
        public void Dispose()
        {
            if (controller != null) controller.Detach(Token);
        }
    }
}

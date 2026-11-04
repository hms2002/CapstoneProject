using System;
using UnityEngine;
using UnityGAS;

/// <summary>병 속의 슬라임의 token 소유 방어 판정과 장착/복원 수명을 연결한다.</summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Slime Bottle")]
public sealed class RelicLogic_SlimeBottle : RelicLogic
{
    [Range(0f, 1f)] public float blockChance = 0.1f;

    public override void OnEquipped(RelicContext ctx) => Attach(ctx);
    public override void OnRestoreAttached(RelicContext ctx) => Attach(ctx);
    public override void OnRestoreDetached(RelicContext ctx) => OnUnequipped(ctx);

    public override void OnUnequipped(RelicContext ctx)
        => ctx.Get<RelicProcManager>()?.UnregisterAll(ctx.token);

    private void Attach(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null || !ctx.owner.CompareTag("Player")) return;
        RelicProcManager manager = ctx.Get<RelicProcManager>();
        if (manager == null) manager = ctx.owner.AddComponent<RelicProcManager>();
        manager.UnregisterAll(ctx.token);
        manager.Register(new SlimeBottleProc(ctx, Mathf.Clamp01(blockChance)));
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return new RelicTooltipData
        {
            effectText = GameText.Format("relic.slime_bottle.effect",
                "● 회피와 별개로 {{val:{0:0.#}%}} 확률로 피해 방어\n" +
                "● 적의 공격·장판·낙사 피해에 적용 · 지속 피해 제외\n" +
                "● 방어한 타격의 넉백·경직·속성 누적도 차단", Mathf.Clamp01(blockChance) * 100f)
        };
    }

    private sealed class SlimeBottleProc : IRelicProc
    {
        private readonly RelicContext context;
        private readonly float chance;
        private IDisposable registration;
        private bool disposed;

        public UnityEngine.Object Token => context.token;

        public SlimeBottleProc(RelicContext context, float chance)
        {
            this.context = context;
            this.chance = chance;
            registration = CombatHitPrevention.Register(context.owner, TryPrevent);
        }

        private bool TryPrevent()
        {
            if (disposed || Token == null || context.owner == null || !context.owner.activeInHierarchy ||
                CombatTargetDeathUtility.IsPlayerDeathSequenceRunning(context.owner.transform) ||
                chance <= 0f || UnityEngine.Random.value >= chance)
                return false;

            DamagePopupPlayback.Show(DamagePopupRequest.Text("Slime!", context.owner.transform.position, isPlayerTarget: true));
            return true;
        }

        public void Handle(GameplayTag tag, AbilityEventData data) { }
        public void Tick(float deltaTime) { }

        public void Dispose()
        {
            disposed = true;
            registration?.Dispose();
            registration = null;
        }
    }
}

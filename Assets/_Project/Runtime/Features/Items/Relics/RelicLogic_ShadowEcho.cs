using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>그림자 잔향의 대시 연결, 범위/피해 수치와 작성된 프리팹 참조를 소유한다.</summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Shadow Echo")]
public sealed class RelicLogic_ShadowEcho : RelicLogic
{
    public static readonly SoundRef ExplosionSound = SoundRef.FromKey("sound_goblinGunner_GunShot");

    public AbilityDefinition dashAbility;
    public ShadowEchoActor echoPrefab;
    public GameplayEffect damageEffect;
    public LayerMask targetLayers;
    [Min(0.05f)] public float delay = 0.7f;
    [Min(0.1f)] public float radius = 1.5f;
    [Range(0.1f, 1f)] public float circleYScale = 0.7f;
    [Min(0.05f)] public float burstDuration = 0.35f;

    public override void OnEquipped(RelicContext ctx) => Attach(ctx);
    public override void OnRestoreAttached(RelicContext ctx) => Attach(ctx);
    public override void OnRestoreDetached(RelicContext ctx) => OnUnequipped(ctx);

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner != null) ctx.owner.GetComponent<ShadowEchoController>()?.Detach(ctx.token);
    }

    private void Attach(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null || ctx.abilitySystem == null ||
            dashAbility == null || echoPrefab == null || damageEffect == null) return;
        ShadowEchoController controller = ctx.Get<ShadowEchoController>();
        if (controller == null) controller = ctx.owner.AddComponent<ShadowEchoController>();
        controller.Attach(ctx.token, ctx.abilitySystem, this);
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return new RelicTooltipData
        {
            effectText = GameText.Format("relic.shadow_echo.effect",
                "● 대시 출발점에 잔향을 남기고 {{val:{0:0.##}초}} 후 범위 폭발\n" +
                "● 생성 시점 공격력의 {{val:100%}} 피해 · 치명타와 속성 효과 없음\n" +
                "● 가로 반경 {{val:{1:0.##}}} · 표시된 범위의 적을 한 번 공격", delay, radius)
        };
    }
}

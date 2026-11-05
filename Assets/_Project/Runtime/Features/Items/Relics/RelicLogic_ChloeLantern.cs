using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>클로에의 등불의 작성 데이터와 장착/복원 연결을 소유한다.</summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Chloe Lantern")]
public sealed class RelicLogic_ChloeLantern : RelicLogic
{
    public static readonly SoundRef ShotSound = SoundRef.FromKey("sound_goblinGunner_GunShot");

    public GameObject summonPrefab;
    public ChloeLanternProjectile2D projectilePrefab;
    public GameplayEffect damageEffect;
    public LayerMask targetLayers;
    public LayerMask wallLayers;
    [Min(0.1f)] public float fireInterval = 2f;
    [Min(0.1f)] public float orbitRadius = 1.2f;
    public Vector2 orbitCenterLocalOffset = new(0f, 0.55f);
    public float angularSpeedDegPerSec = 180f;
    [Min(0.1f)] public float projectileSpeedMultiplier = 1.4f;
    [Min(0.1f)] public float projectileLifetime = 5f;
    [Min(1f)] public float homingTurnRate = 1080f;

    public override void OnEquipped(RelicContext ctx) => Attach(ctx);
    public override void OnRestoreAttached(RelicContext ctx) => Attach(ctx);
    public override void OnRestoreDetached(RelicContext ctx) => OnUnequipped(ctx);

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner != null)
            ctx.owner.GetComponent<ChloeLanternOrbitController>()?.Detach(ctx.token);
    }

    private void Attach(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.abilitySystem == null || ctx.token == null ||
            summonPrefab == null || projectilePrefab == null || damageEffect == null)
            return;

        var controller = ctx.owner.GetComponent<ChloeLanternOrbitController>();
        if (controller == null)
            controller = ctx.owner.AddComponent<ChloeLanternOrbitController>();
        controller.Attach(ctx.token, ctx.abilitySystem, this);
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return new RelicTooltipData
        {
            effectText = GameText.Format("relic.chloe_lantern.effect",
                "● 소환수가 주위를 공전하며 {{val:{0:0.##}초}}마다 시야 안의 적에게 유도탄 발사\n" +
                "● 벽에 가리지 않은 가장 가까운 적을 조준\n" +
                "● 발사 시점 공격력의 {{val:100%}} 피해\n" +
                "● 치명타와 속성 효과 없음 · 첫 적이나 벽에 닿으면 소멸", fireInterval)
        };
    }
}

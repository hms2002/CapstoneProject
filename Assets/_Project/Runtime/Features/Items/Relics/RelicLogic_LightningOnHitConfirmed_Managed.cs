using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 피격 확정 이벤트를 감지해 주변 적에게 번개 추가타를 발생시키는 유물 로직이다.
/// 일반 장착과 복원 장착 모두 proc 등록은 필요하지만, 복원 시 즉시 피해를 새로 발생시키지는 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Lightning On Hit Confirmed (Managed)")]
public class RelicLogic_LightningOnHitConfirmed_Managed : RelicLogic
{
    protected override string DefaultEffectTemplate => "● 적중 확정 시 주변 적에게 번개 추가타\n● 번개 피해 {lightning_damage}\n● 범위 {radius}\n{cooldown_line}";

    [Header("Trigger")]
    public GameplayTag triggerTag;

    [Header("Damage")]
    public GE_Damage_Spec damageEffect;
    public AttributeDefinition attackPlusAttribute;

    [Tooltip("레벨 1 기준 데미지. baseDamageByLevel이 비어있으면 baseDamage * level 로 선형 강화합니다.")]
    public float baseDamage = 50f;

    [Tooltip("레벨별 데미지 테이블(레벨1=0번째). 비어있으면 baseDamage * level 로 계산.")]
    public List<float> baseDamageByLevel;

    public float radius = 4f;
    public LayerMask enemyMask;

    [Header("VFX")]
    public LightningStrikeVfx lightningPrefab;

    [Header("Cooldown")]
    public float cooldownSeconds = 0f;

    private float EvalDamage(int level)
    {
        return RelicTooltipFormatter.EvaluateLeveledValue(baseDamage, baseDamageByLevel, level);
    }

    public override void OnEquipped(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) return;

        mgr.UnregisterAll(ctx.token);
    }

    public override void OnRestoreAttached(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    private void RegisterProc(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) mgr = ctx.owner.AddComponent<RelicProcManager>();

        int level = ctx.level > 0 ? ctx.level : 1;
        float dmg = EvalDamage(level);

        var proc = new LightningStrikeProc2D(
            ctx,
            triggerTag,
            damageEffect,
            attackPlusAttribute,
            dmg,
            radius,
            enemyMask,
            lightningPrefab,
            cooldownSeconds
        );

        mgr.Register(proc);
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return BuildTemplatedTooltip(
            "● 적중 확정 시 주변 적에게 번개 추가타\n● 번개 피해 {lightning_damage}\n● 범위 {radius}\n{cooldown_line}",
            new Dictionary<string, string>
            {
                ["lightning_damage"] = RelicTooltipFormatter.FormatUnsignedValueToken(EvalDamage(previewLevel), false),
                ["radius"] = RelicTooltipFormatter.FormatUnsignedValueToken(radius, false),
                ["cooldown_line"] = cooldownSeconds > 0f
                    ? $"● 쿨다운 {RelicTooltipFormatter.FormatSeconds(cooldownSeconds)}"
                    : string.Empty,
            });
    }
}

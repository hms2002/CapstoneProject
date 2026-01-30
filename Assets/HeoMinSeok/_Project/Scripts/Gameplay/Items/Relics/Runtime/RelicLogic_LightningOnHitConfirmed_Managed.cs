using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Lightning On HitConfirmed (Managed)")]
public class RelicLogic_LightningOnHitConfirmed_Managed : RelicLogic
{
    [Header("Trigger")]
    public GameplayTag triggerTag; // 네가 만든 HitConfirmed 태그

    [Header("Damage")]
    public GE_Damage_Spec damageEffect;
    public AttributeDefinition attackPlusAttribute;
    public float baseDamage = 50f;
    public float radius = 4f;
    public LayerMask enemyMask;

    [Header("VFX")]
    public LightningStrikeVfx lightningPrefab;

    [Header("Cooldown")]
    public float cooldownSeconds = 0f;

    public override void OnEquipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) mgr = ctx.owner.AddComponent<RelicProcManager>(); // 매니저는 1회만 추가

        // Proc 등록 (MonoBehaviour 추가 X)
        var proc = new LightningStrikeProc2D(
            ctx,
            triggerTag,
            damageEffect,
            attackPlusAttribute,
            baseDamage,
            radius,
            enemyMask,
            lightningPrefab,
            cooldownSeconds
        );

        mgr.Register(proc);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) return;

        mgr.UnregisterAll(ctx.token); // token 단위 제거 → 중복 유물 안전
    }
}

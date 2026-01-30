using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Lightning On Hit Confirmed (Managed)")]
public class RelicLogic_LightningOnHitConfirmed_Managed : RelicLogic
{
    [Header("Trigger")]
    public GameplayTag triggerTag; // 네가 만든 HitConfirmed 태그

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
        if (level < 1) level = 1;

        if (baseDamageByLevel != null && baseDamageByLevel.Count > 0)
        {
            int idx = Mathf.Clamp(level - 1, 0, baseDamageByLevel.Count - 1);
            return baseDamageByLevel[idx];
        }

        return baseDamage * level;
    }

    public override void OnEquipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;

        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) mgr = ctx.owner.AddComponent<RelicProcManager>(); // 매니저는 1회만 추가

        int level = ctx.level > 0 ? ctx.level : 1;
        float dmg = EvalDamage(level);

        // Proc 등록 (MonoBehaviour 추가 X)
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

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null) return;
        var mgr = ctx.owner.GetComponent<RelicProcManager>();
        if (mgr == null) return;

        mgr.UnregisterAll(ctx.token); // token 단위 제거 → 강화/중복/재적용 안전
    }
}

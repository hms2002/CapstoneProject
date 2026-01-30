using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class LightningStrikeProc2D : IRelicProc
{
    public Object Token => token;

    private readonly Object token;
    private readonly GameObject owner;
    private readonly AbilitySystem ownerSystem;
    private readonly GameplayEffectRunner effectRunner;
    private readonly AttributeSet ownerAttributeSet;

    private readonly GameplayTag triggerTag;
    private readonly GE_Damage_Spec damageEffect;
    private readonly AttributeDefinition attackPlusAttribute;
    private readonly float baseDamage;
    private readonly float radius;
    private readonly LayerMask enemyMask;

    private readonly LightningStrikeVfx lightningPrefab;
    private readonly float cooldownSeconds;
    private float nextReadyTime;

    public LightningStrikeProc2D(
        RelicContext ctx,
        GameplayTag triggerTag,
        GE_Damage_Spec damageEffect,
        AttributeDefinition attackPlusAttribute,
        float baseDamage,
        float radius,
        LayerMask enemyMask,
        LightningStrikeVfx lightningPrefab,
        float cooldownSeconds)
    {
        this.token = ctx.token;
        this.owner = ctx.owner;
        this.ownerSystem = ctx.abilitySystem;
        this.effectRunner = ctx.effectRunner;
        this.ownerAttributeSet = ctx.attributeSet;

        this.triggerTag = triggerTag;
        this.damageEffect = damageEffect;
        this.attackPlusAttribute = attackPlusAttribute;
        this.baseDamage = baseDamage;
        this.radius = radius;
        this.enemyMask = enemyMask;

        this.lightningPrefab = lightningPrefab;
        this.cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        this.nextReadyTime = 0f;
    }

    public void Handle(GameplayTag tag, AbilityEventData data)
    {
        if (triggerTag == null || tag != triggerTag) return;
        if (ownerSystem == null || effectRunner == null || damageEffect == null) return;

        if (cooldownSeconds > 0f && Time.time < nextReadyTime) return;

        Vector3 strikePos;
        if (data.Target != null) strikePos = data.Target.transform.position;
        else strikePos = data.WorldPosition;

        if (lightningPrefab != null)
        {
            var vfx = Object.Instantiate(lightningPrefab);
            vfx.Play(strikePos, () => ApplyAoeDamage(strikePos));
        }
        else
        {
            ApplyAoeDamage(strikePos);
        }

        if (cooldownSeconds > 0f)
            nextReadyTime = Time.time + cooldownSeconds;
    }

    private void ApplyAoeDamage(Vector3 center)
    {
        float atkPlus = 0f;
        if (ownerAttributeSet != null && attackPlusAttribute != null)
            atkPlus = ownerAttributeSet.GetAttributeValue(attackPlusAttribute);

        float dmg = baseDamage + atkPlus;
        if (dmg <= 0f) return;

        int mask = enemyMask.value;
        var hits = (mask != 0)
            ? Physics2D.OverlapCircleAll(center, radius, mask)
            : Physics2D.OverlapCircleAll(center, radius);

        for (int i = 0; i < hits.Length; i++)
        {
            var go = hits[i].attachedRigidbody ? hits[i].attachedRigidbody.gameObject : hits[i].gameObject;
            if (go == null || go == owner) continue;
            if (go.GetComponent<AttributeSet>() == null) continue;

            var spec = ownerSystem.MakeSpec(damageEffect, causer: owner, sourceObject: token);
            if (damageEffect.damageKey != null)
                spec.SetSetByCallerMagnitude(damageEffect.damageKey, dmg);

            effectRunner.ApplyEffectSpec(spec, go);
        }
    }

    public void Dispose()
    {
        // 필요하면 풀 반환/정리 로직 추가 가능
    }
}

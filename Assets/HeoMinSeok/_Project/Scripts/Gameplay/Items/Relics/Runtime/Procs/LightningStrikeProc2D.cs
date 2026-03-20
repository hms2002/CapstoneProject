using System.Collections.Generic;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class LightningStrikeProc2D : IRelicProc
{
    public Object Token => token;

    private readonly Object token;
    private readonly GameObject owner;
    private readonly AbilitySystem ownerSystem;
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

    private readonly GameplayTag hitConfirmedTag;
    private readonly GE_Knockback_Spec knockbackEffect;

    public LightningStrikeProc2D(
        RelicContext ctx,
        GameplayTag triggerTag,
        GE_Damage_Spec damageEffect,
        AttributeDefinition attackPlusAttribute,
        float baseDamage,
        float radius,
        LayerMask enemyMask,
        LightningStrikeVfx lightningPrefab,
        float cooldownSeconds,
        GameplayTag hitConfirmedTag = null,
        GE_Knockback_Spec knockbackEffect = null)
    {
        this.token = ctx.token;
        this.owner = ctx.owner;
        this.ownerSystem = ctx.abilitySystem;
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

        this.hitConfirmedTag = hitConfirmedTag;
        this.knockbackEffect = knockbackEffect;
    }

    public void Handle(GameplayTag tag, AbilityEventData data)
    {
        if (triggerTag == null || tag != triggerTag) return;
        if (ownerSystem == null || damageEffect == null) return;
        if (cooldownSeconds > 0f && Time.time < nextReadyTime) return;

        Vector3 strikePos = ResolveStrikePosition(data);

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

    private Vector3 ResolveStrikePosition(AbilityEventData data)
    {
        if (data.Target != null)
            return data.Target.transform.position;

        return data.WorldPosition;
    }

    private void ApplyAoeDamage(Vector3 center)
    {
        float atkPlus = 0f;
        if (ownerAttributeSet != null && attackPlusAttribute != null)
            atkPlus = ownerAttributeSet.GetAttributeValue(attackPlusAttribute);

        float finalDamage = baseDamage + atkPlus;
        if (finalDamage <= 0f)
            return;

        Collider2D[] hits = enemyMask.value != 0
            ? Physics2D.OverlapCircleAll(center, radius, enemyMask)
            : Physics2D.OverlapCircleAll(center, radius);

        if (hits == null || hits.Length == 0)
            return;

        var uniqueTargets = new List<GameObject>(hits.Length);
        var visited = new HashSet<GameObject>();

        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null)
                continue;

            GameObject target = ResolveTargetRoot(hit);
            if (target == null || target == owner)
                continue;

            if (!visited.Add(target))
                continue;

            uniqueTargets.Add(target);
        }

        if (uniqueTargets.Count == 0)
            return;

        var snapshot = new CombatDamageSnapshot(
            finalHpDamage: finalDamage,
            finalStaggerBuildUp: 0f,
            finalKnockbackImpulse: 0f,
            elementBuildUps: null
        );

        CombatDamageApplicator.ApplyToTargets(
            system: ownerSystem,
            spec: null,
            damageEffect: damageEffect,
            knockbackEffect: knockbackEffect,
            targets: uniqueTargets,
            snapshot: snapshot,
            hitConfirmedTag: hitConfirmedTag,
            causer: owner
        );
    }

    private static GameObject ResolveTargetRoot(Collider2D hit)
    {
        if (hit == null)
            return null;

        if (hit.attachedRigidbody != null)
        {
            var rbGo = hit.attachedRigidbody.gameObject;
            if (rbGo.GetComponent<AttributeSet>() != null)
                return rbGo;
        }

        var attr = hit.GetComponentInParent<AttributeSet>();
        if (attr != null)
            return attr.gameObject;

        return null;
    }

    public void Dispose()
    {
    }
}
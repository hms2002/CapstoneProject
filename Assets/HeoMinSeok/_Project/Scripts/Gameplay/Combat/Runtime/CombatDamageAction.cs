using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// Centralized "apply damage result" utility.
/// - HP damage: applied via GameplayEffect(Spec) + SetByCaller damageKey (GE_Damage_Spec)
/// - Knockback: applied via separate GE_Knockback_Spec
/// - Stagger build-up: applied to StaggerGaugeSystem on target (if present)
/// - Element build-up: applied to ElementGaugeSystem on target (if present)
///
/// NOTE: In this project, "element damage" is treated as "element gauge build-up".
/// </summary>
public static class CombatDamageAction
{
    // Backward-compatible overload (no stagger / no knockback effect)
    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GameObject target,
        float finalHpDamage,
        IReadOnlyList<ElementDamageResult> elementDamages,
        GameplayTag hitConfirmedTag,
        GameObject causer)
    {
        ApplyDamageAndEmitHit(
            system,
            spec,
            damageEffect,
            knockbackEffect: null,
            target: target,
            finalHpDamage: finalHpDamage,
            finalStaggerBuildUp: 0f,
            elementBuildUps: elementDamages,
            finalKnockbackImpulse: 0f,
            hitConfirmedTag: hitConfirmedTag,
            causer: causer);
    }

    // New overload: includes knockback effect + impulse
    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        float finalKnockbackImpulse,
        GameplayTag hitConfirmedTag,
        GameObject causer)
    {
        ApplyDamageAndEmitHit_Internal(
            system,
            spec,
            damageEffect,
            knockbackEffect,
            target,
            finalHpDamage,
            finalStaggerBuildUp,
            elementBuildUps,
            finalKnockbackImpulse,
            hitConfirmedTag,
            causer);
    }

    /// <summary>
    /// Apply HP damage + knockback + stagger build-up + element build-up,
    /// then optionally emit hit-confirmed event.
    /// </summary>
    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        GameplayTag hitConfirmedTag,
        GameObject causer)
    {
        ApplyDamageAndEmitHit_Internal(
            system,
            spec,
            damageEffect,
            knockbackEffect,
            target,
            finalHpDamage,
            finalStaggerBuildUp,
            elementBuildUps,
            finalKnockbackImpulse: 0f,
            hitConfirmedTag,
            causer);
    }

    // ---- Internal orchestration -------------------------------------------------------------

    private static void ApplyDamageAndEmitHit_Internal(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        float finalKnockbackImpulse,
        GameplayTag hitConfirmedTag,
        GameObject causer)
    {
        if (!Validate(system, damageEffect, target)) return;
        var runner = system.EffectRunner;

        // 0) Extract GE_Damage_Spec once
        var geDmg = damageEffect as GE_Damage_Spec;

        // 1) capture pre-HP for KillConfirmed check
        var killCheck = CaptureKillCheck(target, geDmg);

        // 2) Apply damage effect
        var damageSpec = BuildDamageSpec(
            system,
            spec,
            damageEffect,
            geDmg,
            finalHpDamage,
            elementBuildUps,
            causer);

        runner.ApplyEffectSpec(damageSpec, target);

        // 3) Apply knockback effect separately
        ApplyKnockbackEffect(
            system,
            spec,
            knockbackEffect,
            target,
            finalKnockbackImpulse,
            causer);

        // 4) KillConfirmed
        TryEmitKillConfirmed(system, spec, target, causer, killCheck);

        // 5) Post systems
        ApplyStagger(target, finalStaggerBuildUp, system.gameObject, causer);
        ApplyElements(target, elementBuildUps, system.gameObject, causer);

        // 6) Hit confirmed event
        EmitHitConfirmed(system, spec, target, causer, hitConfirmedTag);
    }

    private static bool Validate(AbilitySystem system, GameplayEffect damageEffect, GameObject target)
    {
        if (system == null || damageEffect == null || target == null) return false;
        if (system.EffectRunner == null) return false;
        return true;
    }

    // ---- Spec building ----------------------------------------------------------------------

    private static GameplayEffectSpec BuildDamageSpec(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GE_Damage_Spec geDmg,
        float finalHpDamage,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        GameObject causer)
    {
        var geSpec = system.MakeSpec(
            damageEffect,
            causer: causer,
            sourceObject: spec != null ? spec.Definition : null);

        // HP damage via SetByCaller
        if (geDmg != null && geDmg.damageKey != null)
            geSpec.SetSetByCallerMagnitude(geDmg.damageKey, finalHpDamage);

        // Keep element breakdown in context as payload
        if (elementBuildUps != null && elementBuildUps.Count > 0)
        {
            var dst = geSpec.Context.ElementDamages;
            dst.Clear();
            for (int i = 0; i < elementBuildUps.Count; i++)
                dst.Add(elementBuildUps[i]);
        }

        return geSpec;
    }

    private static void ApplyKnockbackEffect(
        AbilitySystem system,
        AbilitySpec spec,
        GE_Knockback_Spec knockbackEffect,
        GameObject target,
        float finalKnockbackImpulse,
        GameObject causer)
    {
        if (system == null || system.EffectRunner == null) return;
        if (knockbackEffect == null) return;
        if (target == null) return;
        if (finalKnockbackImpulse <= 0f) return;

        var knockbackSpec = system.MakeSpec(
            knockbackEffect,
            causer: causer,
            sourceObject: spec != null ? spec.Definition : null);

        if (knockbackEffect.knockbackKey != null)
            knockbackSpec.SetSetByCallerMagnitude(
                knockbackEffect.knockbackKey,
                finalKnockbackImpulse);

        system.EffectRunner.ApplyEffectSpec(knockbackSpec, target);
    }

    // ---- Kill confirmed ---------------------------------------------------------------------

    private readonly struct KillCheckData
    {
        public readonly float preHp;
        public readonly AttributeDefinition hpAttr;
        public readonly AttributeSet targetAttrs;

        public KillCheckData(float preHp, AttributeDefinition hpAttr, AttributeSet targetAttrs)
        {
            this.preHp = preHp;
            this.hpAttr = hpAttr;
            this.targetAttrs = targetAttrs;
        }

        public bool IsValid => targetAttrs != null && hpAttr != null && preHp > 0f;
    }

    private static KillCheckData CaptureKillCheck(GameObject target, GE_Damage_Spec geDmg)
    {
        if (geDmg == null || geDmg.healthAttribute == null)
            return default;

        var hpAttr = geDmg.healthAttribute;
        var targetAttrs = target.GetComponent<AttributeSet>();
        if (targetAttrs == null) return new KillCheckData(preHp: -1f, hpAttr, targetAttrs: null);

        float preHp = targetAttrs.GetAttributeValue(hpAttr);
        return new KillCheckData(preHp, hpAttr, targetAttrs);
    }

    private static void TryEmitKillConfirmed(
        AbilitySystem system,
        AbilitySpec spec,
        GameObject target,
        GameObject causer,
        KillCheckData killCheck)
    {
        if (system.KillConfirmedTag == null) return;
        if (!killCheck.IsValid) return;

        float postHp = killCheck.targetAttrs.GetAttributeValue(killCheck.hpAttr);
        if (postHp > 0f) return;

        system.SendGameplayEvent(system.KillConfirmedTag, new AbilityEventData
        {
            AbilitySystem = system,
            Spec = spec,
            Instigator = system.gameObject,
            Target = target,
            WorldPosition = target.transform.position,
            Causer = causer
        });
    }

    // ---- Post systems -----------------------------------------------------------------------

    private static void ApplyStagger(
        GameObject target,
        float finalStaggerBuildUp,
        GameObject instigator,
        GameObject causer)
    {
        if (finalStaggerBuildUp <= 0f) return;

        var stagger = target.GetComponent<StaggerGaugeSystem>();
        if (stagger == null) return;

        stagger.AddBuildUp(finalStaggerBuildUp, instigator: instigator, causer: causer);
    }

    private static void ApplyElements(
        GameObject target,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        GameObject instigator,
        GameObject causer)
    {
        if (elementBuildUps == null || elementBuildUps.Count <= 0) return;

        var elem = target.GetComponent<ElementGaugeSystem>();
        if (elem == null) return;

        for (int i = 0; i < elementBuildUps.Count; i++)
        {
            var e = elementBuildUps[i];
            if (e.elementType != null && e.damage > 0f)
                elem.AddBuildUp(e.elementType, e.damage, instigator: instigator, causer: causer);
        }
    }

    // ---- Hit confirmed ----------------------------------------------------------------------

    private static void EmitHitConfirmed(
        AbilitySystem system,
        AbilitySpec spec,
        GameObject target,
        GameObject causer,
        GameplayTag hitConfirmedTag)
    {
        if (hitConfirmedTag == null) return;

        system.SendGameplayEvent(hitConfirmedTag, new AbilityEventData
        {
            AbilitySystem = system,
            Spec = spec,
            Instigator = system.gameObject,
            Target = target,
            WorldPosition = target.transform.position,
            Causer = causer
        });
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// Centralized "apply damage result" utility.
/// - HP damage: applied via GameplayEffect(Spec) + SetByCaller damageKey (GE_Damage_Spec)
/// - Stagger build-up: applied to StaggerGaugeSystem on target (if present)
/// - Element build-up: applied to ElementGaugeSystem on target (if present)
///
/// NOTE: In this project, "element damage" is treated as "element gauge build-up".
/// </summary>
public static class CombatDamageAction
{
    // Backward-compatible overload (no stagger)
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
        ApplyDamageAndEmitHit(system, spec, damageEffect, target,
            finalHpDamage, finalStaggerBuildUp: 0f, elementBuildUps: elementDamages,
            hitConfirmedTag: hitConfirmedTag, causer: causer);
    }


    // New overload: includes knockback impulse (SetByCaller) for GE_Damage_Spec.
    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        float finalKnockbackImpulse,
        GameplayTag hitConfirmedTag,
        GameObject causer)
    {
        ApplyDamageAndEmitHit_Internal(system, spec, damageEffect, target,
            finalHpDamage, finalStaggerBuildUp, elementBuildUps, finalKnockbackImpulse,
            hitConfirmedTag, causer);
    }

    /// <summary>
    /// Apply HP damage + stagger build-up + element build-up, then optionally emit hit-confirmed event.
    /// </summary>
    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        GameplayTag hitConfirmedTag,
        GameObject causer)
    {
        ApplyDamageAndEmitHit_Internal(system, spec, damageEffect, target,
            finalHpDamage, finalStaggerBuildUp, elementBuildUps, finalKnockbackImpulse: 0f,
            hitConfirmedTag, causer);
    }

    private static void ApplyDamageAndEmitHit_Internal(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GameObject target,
        float finalHpDamage,
        float finalStaggerBuildUp,
        IReadOnlyList<ElementDamageResult> elementBuildUps,
        float finalKnockbackImpulse,
        GameplayTag hitConfirmedTag,
        GameObject causer)
    {
        if (system == null || damageEffect == null || target == null) return;

        var runner = system.EffectRunner;
        if (runner == null) return;

        // 1) HP damage via Spec + SetByCaller
        //    + (Optional) Kill confirmed event: detect target HP crossing >0 -> <=0
        float preHp = -1f;
        AttributeDefinition hpAttr = null;
        AttributeSet targetAttrs = null;
        if (damageEffect is GE_Damage_Spec geDmg0 && geDmg0.healthAttribute != null)
        {
            hpAttr = geDmg0.healthAttribute;
            targetAttrs = target.GetComponent<AttributeSet>();
            if (targetAttrs != null)
                preHp = targetAttrs.GetAttributeValue(hpAttr);
        }
        // NOTE: KillConfirmed 판정은 아래에서 preHp/postHp만 사용합니다.
        GameplayTag damageKey = null;
        if (damageEffect is GE_Damage_Spec geDmg)
            damageKey = geDmg.damageKey;

        var geSpec = system.MakeSpec(damageEffect, causer: causer, sourceObject: spec != null ? spec.Definition : null);
        if (damageKey != null) geSpec.SetSetByCallerMagnitude(damageKey, finalHpDamage);

        // 1b) Knockback impulse via Spec + SetByCaller (optional)
        GameplayTag knockbackKey = null;
        if (damageEffect is GE_Damage_Spec geDmg2)
            knockbackKey = geDmg2.knockbackKey;
        if (knockbackKey != null && finalKnockbackImpulse > 0f)
            geSpec.SetSetByCallerMagnitude(knockbackKey, finalKnockbackImpulse);

        // Keep element breakdown in context as payload (optional)
        if (elementBuildUps != null && elementBuildUps.Count > 0)
        {
            var dst = geSpec.Context.ElementDamages;
            dst.Clear();
            for (int i = 0; i < elementBuildUps.Count; i++)
                dst.Add(elementBuildUps[i]);
        }

        runner.ApplyEffectSpec(geSpec, target);

        // 1c) Kill confirmed event
        // NOTE: "죽음" 판정은 프로젝트마다 다를 수 있어요(부활/더미/neverDie 등).
        //       여기서는 HP가 0 이하로 떨어지는 순간을 기준으로 이벤트를 보냅니다.
        if (system.KillConfirmedTag != null &&
            targetAttrs != null && hpAttr != null && preHp > 0f)
        {
            float postHp = targetAttrs.GetAttributeValue(hpAttr);
            if (postHp <= 0f)
            {
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
        }

        // (KillConfirmed 중복 블록 제거)
        // 2) Stagger build-up
        if (finalStaggerBuildUp > 0f)
        {
            var stagger = target.GetComponent<StaggerGaugeSystem>();
            if (stagger != null)
                stagger.AddBuildUp(finalStaggerBuildUp, instigator: system.gameObject, causer: causer);
        }

        // 3) Element build-up
        if (elementBuildUps != null && elementBuildUps.Count > 0)
        {
            var elem = target.GetComponent<ElementGaugeSystem>();
            if (elem != null)
            {
                for (int i = 0; i < elementBuildUps.Count; i++)
                {
                    var e = elementBuildUps[i];
                    if (e.elementType != null && e.damage > 0f)
                        elem.AddBuildUp(e.elementType, e.damage, instigator: system.gameObject, causer: causer);
                }
            }
        }

        // 4) Emit hit confirmed event (optional)
        if (hitConfirmedTag != null)
        {
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

}

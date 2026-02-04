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
        if (system == null || damageEffect == null || target == null) return;

        var runner = system.EffectRunner;
        if (runner == null) return;

        // 1) HP damage via Spec + SetByCaller
        GameplayTag damageKey = null;
        if (damageEffect is GE_Damage_Spec geDmg)
            damageKey = geDmg.damageKey;

        var geSpec = system.MakeSpec(damageEffect, causer: causer, sourceObject: spec != null ? spec.Definition : null);
        if (damageKey != null) geSpec.SetSetByCallerMagnitude(damageKey, finalHpDamage);

        // Keep element breakdown in context as payload (optional)
        if (elementBuildUps != null && elementBuildUps.Count > 0)
        {
            var dst = geSpec.Context.ElementDamages;
            dst.Clear();
            for (int i = 0; i < elementBuildUps.Count; i++)
                dst.Add(elementBuildUps[i]);
        }

        runner.ApplyEffectSpec(geSpec, target);

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

using UnityEngine;
using UnityGAS;

public static class CombatDamageAction
{
    public static void ApplyDamageAndEmitHit(
        AbilitySystem system,
        AbilitySpec spec,
        GameplayEffect damageEffect,
        GameObject target,
        float finalHpDamage,
        System.Collections.Generic.IReadOnlyList<ElementDamageResult> elementDamages,
        GameplayTag hitConfirmedTag,
        GameObject causer)
    {
        if (system == null || spec == null) return;
        if (damageEffect == null || target == null) return;
        if (finalHpDamage <= 0f) return;

        var runner = system.EffectRunner;
        if (runner == null) return;

        GameplayTag damageKey = null;
        if (damageEffect is GE_Damage_Spec geDmg)
            damageKey = geDmg.damageKey;

        var geSpec = system.MakeSpec(damageEffect, causer: causer, sourceObject: spec.Definition);
        if (damageKey != null) geSpec.SetSetByCallerMagnitude(damageKey, finalHpDamage);

        // Deliver element damage channels (application is implemented later)
        if (elementDamages != null && elementDamages.Count > 0)
        {
            var dst = geSpec.Context.ElementDamages;
            dst.Clear();
            for (int i = 0; i < elementDamages.Count; i++)
                dst.Add(elementDamages[i]);
        }

        runner.ApplyEffectSpec(geSpec, target);

        // ✅ "피해가 실제로 들어간 순간"에 타겟 포함해서 이벤트 발행
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

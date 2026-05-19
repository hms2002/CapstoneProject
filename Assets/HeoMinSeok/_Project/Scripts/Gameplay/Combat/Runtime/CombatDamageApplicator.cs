using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    public static class CombatDamageApplicator
    {
        public static void ApplyToTargets(
            AbilitySystem system,
            AbilitySpec spec,
            GameplayEffect damageEffect,
            GE_Knockback_Spec knockbackEffect,
            IReadOnlyList<GameObject> targets,
            CombatDamageSnapshot snapshot,
            GameplayTag hitConfirmedTag,
            GameObject causer)
        {
            if (targets == null)
                return;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null)
                    continue;

                CombatDamageAction.ApplyDamageAndEmitHit(
                    system: system,
                    spec: spec,
                    damageEffect: damageEffect,
                    knockbackEffect: knockbackEffect,
                    target: target,
                    finalHpDamage: snapshot.FinalHpDamage,
                    finalStaggerBuildUp: snapshot.FinalStaggerBuildUp,
                    finalKnockbackImpulse: snapshot.FinalKnockbackImpulse,
                    hitConfirmedTag: hitConfirmedTag,
                    hitWorldPosition: target.transform.position,
                    causer: causer,
                    isCriticalHit: snapshot.IsCriticalHit,
                    elementBuildUps: snapshot.FinalElementBuildUps,
                    hasResolvedElementBuildUps: snapshot.HasResolvedElementBuildUps
                );
            }
        }
    }
}

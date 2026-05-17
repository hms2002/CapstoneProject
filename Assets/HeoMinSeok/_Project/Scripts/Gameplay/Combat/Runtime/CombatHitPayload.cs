using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Common payload for one hit application.
    /// Element build-up is not stored here; CombatDamageAction resolves it from the source attacker.
    /// </summary>
    [System.Serializable]
    public sealed class CombatHitPayload
    {
        public AbilitySystem sourceSystem;
        public AbilitySpec sourceSpec;
        public GameplayEffect damageEffect;
        public GE_Knockback_Spec knockbackEffect;
        public float finalHpDamage;
        public float finalStaggerBuildUp;
        public float finalKnockbackImpulse;
        public GameplayTag hitConfirmedTag;
        public GameObject causer;
        public bool isCriticalHit;

        public bool IsValid()
        {
            return sourceSystem != null && damageEffect != null;
        }

        public static CombatHitPayload FromSnapshot(
            AbilitySystem sourceSystem,
            AbilitySpec sourceSpec,
            GameplayEffect damageEffect,
            GE_Knockback_Spec knockbackEffect,
            CombatDamageSnapshot snapshot,
            GameplayTag hitConfirmedTag,
            GameObject causer)
        {
            return new CombatHitPayload
            {
                sourceSystem = sourceSystem,
                sourceSpec = sourceSpec,
                damageEffect = damageEffect,
                knockbackEffect = knockbackEffect,
                finalHpDamage = snapshot.FinalHpDamage,
                finalStaggerBuildUp = snapshot.FinalStaggerBuildUp,
                finalKnockbackImpulse = snapshot.FinalKnockbackImpulse,
                hitConfirmedTag = hitConfirmedTag,
                causer = causer,
                isCriticalHit = snapshot.IsCriticalHit
            };
        }
    }

    public static class CombatHitPayloadApplier
    {
        public static bool Apply(GameObject target, CombatHitPayload payload)
        {
            return Apply(target, payload, target != null ? target.transform.position : Vector3.zero);
        }

        public static bool Apply(GameObject target, CombatHitPayload payload, Vector3 hitWorldPosition)
        {
            if (target == null || payload == null || !payload.IsValid())
                return false;

            CombatDamageAction.ApplyDamageAndEmitHit(
                system: payload.sourceSystem,
                spec: payload.sourceSpec,
                damageEffect: payload.damageEffect,
                knockbackEffect: payload.knockbackEffect,
                target: target,
                finalHpDamage: payload.finalHpDamage,
                finalStaggerBuildUp: payload.finalStaggerBuildUp,
                finalKnockbackImpulse: payload.finalKnockbackImpulse,
                hitConfirmedTag: payload.hitConfirmedTag,
                hitWorldPosition: hitWorldPosition,
                causer: payload.causer,
                isCriticalHit: payload.isCriticalHit);

            return true;
        }
    }
}

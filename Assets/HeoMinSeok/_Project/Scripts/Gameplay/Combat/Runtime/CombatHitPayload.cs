using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 한 번의 타격에 필요한 공통 피해 payload를 보관한다.
    /// - source system/spec, damage effect, knockback effect, 최종 수치, causer를 한 묶음으로 전달한다.
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
        public ElementDamageResult[] elementBuildUps;
        public float finalKnockbackImpulse;
        public GameplayTag hitConfirmedTag;
        public GameObject causer;

        public bool IsValid()
        {
            return sourceSystem != null && damageEffect != null;
        }
    }

    /// <summary>
    /// 책임 :
    /// - CombatHitPayload를 실제 CombatDamageAction 호출로 변환한다.
    /// - 공격체/유물/장판 등 호출 주체가 달라도 같은 방식으로 피해 적용을 수행한다.
    /// </summary>
    public static class CombatHitPayloadApplier
    {
        public static bool Apply(GameObject target, CombatHitPayload payload)
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
                elementBuildUps: payload.elementBuildUps,
                finalKnockbackImpulse: payload.finalKnockbackImpulse,
                hitConfirmedTag: payload.hitConfirmedTag,
                causer: payload.causer);

            return true;
        }
    }
}
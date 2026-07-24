using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 한 번의 적중 적용에 필요한 생성 시점 전투 수치를 보관한다.
    /// - 장기 생존 공격체가 적중할 때 공격자 스탯을 다시 읽지 않도록 HP / 스태거 / 넉백 / 원소 누적 snapshot을 전달한다.
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
        public HitImpactCueKind hitImpactCueKind;
        public GameObject causer;
        public bool isCriticalHit;
        public ElementDamageResult[] elementBuildUps;
        public bool hasResolvedElementBuildUps;

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
            GameObject causer,
            HitImpactCueKind hitImpactCueKind = HitImpactCueKind.Default)
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
                hitImpactCueKind = hitImpactCueKind,
                causer = causer,
                isCriticalHit = snapshot.IsCriticalHit,
                elementBuildUps = snapshot.FinalElementBuildUps,
                hasResolvedElementBuildUps = snapshot.HasResolvedElementBuildUps
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

            if (CombatInvulnerabilityUtil.IsDamageSuppressed(target, payload.damageEffect as GE_Damage_Spec))
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
                isCriticalHit: payload.isCriticalHit,
                elementBuildUps: payload.elementBuildUps,
                hasResolvedElementBuildUps: payload.hasResolvedElementBuildUps,
                hitImpactCueKind: payload.hitImpactCueKind);

            return true;
        }
    }
}

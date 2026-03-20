using System.Collections.Generic;

namespace UnityGAS
{
    public readonly struct CombatDamageSnapshot
    {
        public readonly float FinalHpDamage;
        public readonly float FinalStaggerBuildUp;
        public readonly float FinalKnockbackImpulse;
        public readonly List<ElementDamageResult> ElementBuildUps;

        public CombatDamageSnapshot(
            float finalHpDamage,
            float finalStaggerBuildUp,
            float finalKnockbackImpulse,
            List<ElementDamageResult> elementBuildUps)
        {
            FinalHpDamage = finalHpDamage;
            FinalStaggerBuildUp = finalStaggerBuildUp;
            FinalKnockbackImpulse = finalKnockbackImpulse;
            ElementBuildUps = elementBuildUps;
        }
    }
}
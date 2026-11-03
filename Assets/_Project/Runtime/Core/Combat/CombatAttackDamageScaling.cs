using UnityEngine;

namespace UnityGAS
{
    /// <summary>The gameplay weapon-input owner identifies attacks without a Core -> Gameplay dependency.</summary>
    public interface ICombatAttackKindSource
    {
        bool TryGetAttackKind(AbilityDefinition ability, out DamageAttackKind kind);
    }

    /// <summary>Capture normal/skill HP multipliers when constructing a weapon payload, never on projectile impact.</summary>
    public static class CombatAttackDamageScaling
    {
        public static CombatDamageSnapshot Capture(CombatDamageSnapshot snapshot, AbilitySystem system, AbilitySpec spec,
            bool scalePhysicalDamage = true)
        {
            if (system == null || spec?.Definition == null) return snapshot;
            var source = system.GetComponent<ICombatAttackKindSource>();
            if (source == null || !source.TryGetAttackKind(spec.Definition, out var kind)) return snapshot;
            StatId stat = kind == DamageAttackKind.Normal ? StatId.NormalMul : StatId.SkillMul;
            var bindings = system.DamageProfile != null ? system.DamageProfile.GetStatBindings() : null;
            if (bindings == null || !bindings.TryGetBinding(stat, out var binding) || binding.attribute == null ||
                system.AttributeSet == null || system.AttributeSet.GetAttribute(binding.attribute) == null) return snapshot;
            IStatProvider provider = AbilityStatProviderFactory.Create(system);
            if (provider == null) return snapshot;

            float multiplier = Mathf.Max(0f, provider.Get(stat));
            if (Mathf.Approximately(multiplier, 1f)) return snapshot;
            ElementDamageResult[] elements = snapshot.FinalElementHpDamages;
            if (elements != null && elements.Length > 0)
            {
                elements = (ElementDamageResult[])elements.Clone();
                for (int i = 0; i < elements.Length; i++) elements[i].damage *= multiplier;
            }
            return new CombatDamageSnapshot(snapshot.FinalHpDamage * (scalePhysicalDamage ? multiplier : 1f), snapshot.FinalStaggerBuildUp,
                snapshot.FinalKnockbackImpulse, snapshot.IsCriticalHit, snapshot.FinalElementBuildUps,
                snapshot.HasResolvedElementBuildUps, elements);
        }
    }
}

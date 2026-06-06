using UnityEngine;

namespace UnityGAS
{
    public static class CombatEvasionUtil
    {
        public static bool TryRollEvasion(GameObject target)
        {
            if (target == null)
                return false;

            var abilitySystem = target.GetComponent<AbilitySystem>();
            var attributeSet = target.GetComponent<AttributeSet>();
            if (abilitySystem == null || attributeSet == null || abilitySystem.DamageProfile == null)
                return false;

            var bindings = abilitySystem.DamageProfile.GetStatBindings();
            if (bindings == null)
                return false;

            var provider = new AttributeStatProvider(attributeSet, bindings);
            float evasionChance = Mathf.Clamp01(provider.Get(StatId.EvasionFinal));
            if (evasionChance <= 0f)
                return false;

            return Random.value < evasionChance;
        }
    }
}

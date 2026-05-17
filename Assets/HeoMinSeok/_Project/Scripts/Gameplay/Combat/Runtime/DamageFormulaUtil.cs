using UnityEngine;

namespace UnityGAS
{
    public enum DamageAttackKind { Normal, Skill }

    public struct DamageResult
    {
        public float hpDamage;
        public float staggerDamage;
        public bool isCrit;
    }

    /// <summary>
    /// Applies shared post-process rules to formula outputs.
    /// Scaled formulas are expected to output final base values before crit/final multipliers.
    /// </summary>
    public static class DamageFormulaUtil
    {
        public static DamageResult PostProcess(
            IStatProvider provider,
            float baseHpDamage,
            float baseStaggerDamage,
            float? critRoll01 = null,
            bool forceCrit = false)
        {
            var result = new DamageResult();

            float hp = Mathf.Max(0f, baseHpDamage);
            float stagger = Mathf.Max(0f, baseStaggerDamage);

            if (provider == null)
            {
                result.hpDamage = hp;
                result.staggerDamage = stagger;
                result.isCrit = false;
                return result;
            }

            float critChance = Mathf.Clamp01(provider.Get(StatId.CritChanceFinal));
            float critMul = Mathf.Max(0f, provider.Get(StatId.CritMultiplier));
            float finalMul = Mathf.Max(0f, provider.Get(StatId.FinalMul));

            bool isCrit = forceCrit || (Roll(critRoll01) < critChance);
            float critFactor = isCrit ? critMul : 1f;

            result.isCrit = isCrit;
            result.hpDamage = hp * critFactor * finalMul;
            result.staggerDamage = stagger * finalMul;
            return result;
        }

        private static float Roll(float? roll01)
        {
            if (roll01.HasValue) return Mathf.Clamp01(roll01.Value);
            return Random.value;
        }
    }
}

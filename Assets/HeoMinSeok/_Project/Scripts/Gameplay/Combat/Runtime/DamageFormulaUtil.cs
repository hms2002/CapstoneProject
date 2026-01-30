using UnityEngine;

namespace UnityGAS
{
    public enum DamageAttackKind { Normal, Skill }

    public struct DamageResult
    {
        public float hpDamage;
        public float elementDamage;
        public float staggerDamage;
        public bool isCrit;
    }

    public static class DamageFormulaUtil
    {
        public static DamageResult Compute(
            AttributeSet attacker,
            DamageFormulaStats stats,
            DamageAttackKind kind,
            float baseHpDamage,
            float baseStaggerDamage = 0f,
            bool includeElement = false,
            bool includeStagger = false,
            float? critRoll01 = null,
            bool forceCrit = false)
        {
            var r = new DamageResult();
            if (attacker == null || stats == null) return r;

            float atkAdd = Get(attacker, stats.attackAdd, 0f);
            float atkMul = GetMul(attacker, stats.attackMul, stats.attackMulMode);
            float finalMul = GetMul(attacker, stats.finalMul, stats.finalMulMode);

            float critChance = Mathf.Clamp01(Get(attacker, stats.critChance, 0f));
            float critMul = GetMul(attacker, stats.critMultiplier, stats.critMulMode);
            bool isCrit = forceCrit || (Roll(critRoll01) < critChance);
            float critFactor = isCrit ? critMul : 1f;
            r.isCrit = isCrit;

            // HP
            if (kind == DamageAttackKind.Normal)
            {
                float normalAdd = Get(attacker, stats.normalAdd, 0f);
                float normalMul = GetMul(attacker, stats.normalMul, stats.normalMulMode);

                float dmg = baseHpDamage + atkAdd;
                dmg = dmg * atkMul + normalAdd;
                dmg = dmg * normalMul;
                dmg = dmg * critFactor;
                dmg = dmg * finalMul;
                r.hpDamage = Mathf.Max(0f, dmg);
            }
            else
            {
                float skillAdd = Get(attacker, stats.skillAdd, 0f);
                float skillMul = GetMul(attacker, stats.skillMul, stats.skillMulMode);

                float dmg = baseHpDamage + atkAdd;
                dmg = dmg * atkMul + skillAdd;
                dmg = dmg * skillMul;
                dmg = dmg * critFactor;
                dmg = dmg * finalMul;
                r.hpDamage = Mathf.Max(0f, dmg);
            }

            // Stagger
            if (includeStagger)
            {
                float stAdd = Get(attacker, stats.staggerAdd, 0f);
                float stMul = GetMul(attacker, stats.staggerMul, stats.staggerMulMode);
                float dmg = (baseStaggerDamage + stAdd) * stMul * finalMul;
                r.staggerDamage = Mathf.Max(0f, dmg);
            }

            // Element
            if (includeElement)
            {
                float elAdd = Get(attacker, stats.elementAdd, 0f);
                float elMul = GetMul(attacker, stats.elementMul, stats.elementMulMode);
                float dmg = elAdd * elMul * finalMul;
                r.elementDamage = Mathf.Max(0f, dmg);
            }

            return r;
        }

        private static float Get(AttributeSet set, AttributeDefinition def, float fallback)
        {
            if (set == null || def == null) return fallback;
            var v = set.GetAttribute(def);
            return v != null ? v.CurrentValue : fallback;
        }

        private static float GetMul(AttributeSet set, AttributeDefinition def, DamageFormulaStats.MultiplierMode mode)
        {
            if (def == null) return 1f;

            float fallback = mode == DamageFormulaStats.MultiplierMode.AsFactor ? 1f : 0f;
            float raw = Get(set, def, fallback);

            if (mode == DamageFormulaStats.MultiplierMode.AsFactor)
                return Mathf.Max(0f, raw);

            return Mathf.Max(0f, 1f + raw);
        }

        private static float Roll(float? roll01)
        {
            if (roll01.HasValue) return Mathf.Clamp01(roll01.Value);
            return Random.value;
        }
    }
}

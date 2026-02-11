using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

namespace UnityGAS
{
    public enum DamageAttackKind { Normal, Skill }

    public struct DamageResult
    {
        public float hpDamage;
        public float staggerDamage;
        public float elementDamage;
        public bool isCrit;
    }

    /// <summary>
    /// Solution 1 pipeline:
    /// - Scaled formulas output FINAL values (already includes (Base+Add)*Mul style stats).
    /// - This utility only applies shared post-process rules (crit/global multipliers).
    ///
    /// It intentionally does NOT apply AttackAdd/AttackMul/NormalMul/etc to avoid double-scaling.
    /// </summary>
    public static class DamageFormulaUtil
    {
        /// <summary>
        /// Applies shared post-process rules to formula outputs.
        /// - baseHpDamage/baseStaggerDamage: FINAL values from formulas.
        /// - elementInputs: FINAL per-element build-up values (optional).
        /// - outElementResults: optional list filled with final per-element build-up values.
        /// </summary>
        public static DamageResult PostProcess(
            AttributeSet attacker,
            DamagePostProcessStats post,
            float baseHpDamage,
            float baseStaggerDamage,
            IReadOnlyList<ElementDamageInput> elementInputs,
            List<ElementDamageResult> outElementResults,
            float? critRoll01 = null,
            bool forceCrit = false,
            bool critAffectsElement = true)
        {
            var r = new DamageResult();
            if (outElementResults != null) outElementResults.Clear();

            // If no post stats, just passthrough
            if (post == null || attacker == null)
            {
                r.hpDamage = Mathf.Max(0f, baseHpDamage);
                r.staggerDamage = Mathf.Max(0f, baseStaggerDamage);
                if (elementInputs != null)
                {
                    float sum = 0f;
                    for (int i = 0; i < elementInputs.Count; i++)
                    {
                        var e = elementInputs[i];
                        float v = Mathf.Max(0f, e.baseDamage);
                        sum += v;
                        if (outElementResults != null && e.elementType != null && v > 0f)
                            outElementResults.Add(new ElementDamageResult { elementType = e.elementType, damage = v });
                    }
                    r.elementDamage = sum;
                }
                return r;
            }

            float critChance = post.ReadCritChance(attacker);
            float critMul = post.ReadCritMultiplier(attacker);

            bool isCrit = forceCrit || (Roll(critRoll01) < critChance);
            float critFactor = isCrit ? critMul : 1f;
            r.isCrit = isCrit;

            float finalMul = post.ReadFinalMul(attacker);

            r.hpDamage = Mathf.Max(0f, baseHpDamage) * critFactor * finalMul;
            r.staggerDamage = Mathf.Max(0f, baseStaggerDamage) * finalMul;

            if (elementInputs != null && elementInputs.Count > 0)
            {
                float sum = 0f;
                float elementCritFactor = critAffectsElement ? critFactor : 1f;
                for (int i = 0; i < elementInputs.Count; i++)
                {
                    var e = elementInputs[i];
                    if (e.elementType == null) continue;

                    float v = Mathf.Max(0f, e.baseDamage) * elementCritFactor * finalMul;
                    if (v <= 0f) continue;

                    sum += v;
                    if (outElementResults != null)
                        outElementResults.Add(new ElementDamageResult { elementType = e.elementType, damage = v });
                }
                r.elementDamage = sum;
            }

            return r;
        }

        private static float Roll(float? roll01)
        {
            if (roll01.HasValue) return Mathf.Clamp01(roll01.Value);
            return Random.value;
        }
    }
}
/// - Scaled formulas output FINAL values (already includes (Base+Add)*Mul style stats).
/// - This utility only applies shared post-process rules (crit/global multipliers).
///
/// It intentionally does NOT apply AttackAdd/AttackMul/NormalMul/etc to avoid double-scaling.
/// </summary>
public static class DamageFormulaUtil
{
    public static DamageResult PostProcess(
        AttributeSet attacker,
        DamagePostProcessStats post,
        float baseHpDamage,
        float baseStaggerDamage,
        IReadOnlyList<ElementDamageInput> elementInputs,
        List<ElementDamageResult> outElementResults,
        bool critAffectsElement = true,
        float? critRoll01 = null,
        bool forceCrit = false)
    {
        var r = new DamageResult();
        if (outElementResults != null) outElementResults.Clear();

        float hp = Mathf.Max(0f, baseHpDamage);
        float st = Mathf.Max(0f, baseStaggerDamage);

        float sumElem = 0f;
        if (elementInputs != null)
        {
            for (int i = 0; i < elementInputs.Count; i++)
            {
                var e = elementInputs[i];
                if (e.elementType == null) continue;
                float d = Mathf.Max(0f, e.baseDamage);
                sumElem += d;
                if (outElementResults != null && d > 0f)
                    outElementResults.Add(new ElementDamageResult { elementType = e.elementType, damage = d });
            }
        }

        if (post == null || attacker == null)
        {
            r.hpDamage = hp;
            r.staggerDamage = st;
            r.elementDamage = sumElem;
            r.isCrit = false;
            return r;
        }

        float critChance = post.ReadCritChance(attacker);
        float critMul = post.ReadCritMultiplier(attacker);
        bool isCrit = forceCrit || (Roll(critRoll01) < critChance);
        float critFactor = isCrit ? critMul : 1f;

        float finalMul = post.ReadFinalMul(attacker);

        hp = hp * critFactor * finalMul;
        st = st * finalMul; // stagger usually not crit

        float elemMul = finalMul * (critAffectsElement ? critFactor : 1f);
        sumElem = sumElem * elemMul;

        if (outElementResults != null)
        {
            for (int i = 0; i < outElementResults.Count; i++)
            {
                var e = outElementResults[i];
                e.damage = e.damage * elemMul;
                outElementResults[i] = e;
            }
        }

        r.hpDamage = Mathf.Max(0f, hp);
        r.staggerDamage = Mathf.Max(0f, st);
        r.elementDamage = Mathf.Max(0f, sumElem);
        r.isCrit = isCrit;
        return r;
    }

    private static float Roll(float? roll01)
    {
        if (roll01.HasValue) return Mathf.Clamp01(roll01.Value);
        return Random.value;
    }
}


using System.Collections.Generic;
using UnityEngine;

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
        ///
        /// StatId requirements:
        /// - CritChance
        /// - CritMultiplier
        /// - FinalDamageMultiplier
        /// </summary>
        public static DamageResult PostProcess(
            IStatProvider provider,
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

            float hp = Mathf.Max(0f, baseHpDamage);
            float stagger = Mathf.Max(0f, baseStaggerDamage);

            // provider가 없으면 후처리 없이 그대로 통과
            if (provider == null)
            {
                r.hpDamage = hp;
                r.staggerDamage = stagger;

                if (elementInputs != null)
                {
                    float sum = 0f;
                    for (int i = 0; i < elementInputs.Count; i++)
                    {
                        var e = elementInputs[i];
                        if (e.elementType == null) continue;

                        float v = Mathf.Max(0f, e.baseDamage);
                        if (v <= 0f) continue;

                        sum += v;
                        if (outElementResults != null)
                        {
                            outElementResults.Add(new ElementDamageResult
                            {
                                elementType = e.elementType,
                                damage = v
                            });
                        }
                    }
                    r.elementDamage = sum;
                }

                r.isCrit = false;
                return r;
            }

            float critChance = Mathf.Clamp01(provider.Get(StatId.CritChance));
            float critMul = Mathf.Max(0f, provider.Get(StatId.CritMultiplier));
            float finalMul = Mathf.Max(0f, provider.Get(StatId.FinalMul));

            bool isCrit = forceCrit || (Roll(critRoll01) < critChance);
            float critFactor = isCrit ? critMul : 1f;

            r.isCrit = isCrit;
            r.hpDamage = hp * critFactor * finalMul;
            r.staggerDamage = stagger * finalMul; // stagger는 보통 crit 비적용

            if (elementInputs != null)
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
                    {
                        outElementResults.Add(new ElementDamageResult
                        {
                            elementType = e.elementType,
                            damage = v
                        });
                    }
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
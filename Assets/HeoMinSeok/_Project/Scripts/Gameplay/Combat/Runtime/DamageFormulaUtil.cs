using UnityEngine;

namespace UnityGAS
{
    public enum DamageAttackKind { Normal, Skill }

    public struct DamageResult
    {
        public float hpDamage;
        /// <summary>
        /// Sum of all computed element damages for this hit.
        /// (Breakdown can be obtained via Compute(..., outElementResults: ...))
        /// </summary>
        public float elementDamage;
        public float staggerDamage;
        public bool isCrit;
    }

    public static class DamageFormulaUtil
    {
        /// <summary>
        /// New: Compute a hit damage that may contain multiple element channels.
        /// - elementInputs: list of (elementType, baseDamage) authored on the attack.
        /// - outElementResults: optional list that will be filled with per-element final damages.
        ///   (Caller owns the list; it will be cleared by this method.)
        /// - critAffectsElement: if true, the same crit roll affects element damages as well.
        /// </summary>
        public static DamageResult Compute(
            AttributeSet attacker,
            DamageFormulaStats stats,
            DamageAttackKind kind,
            float baseHpDamage,
            float baseStaggerDamage,
            System.Collections.Generic.IReadOnlyList<ElementDamageInput> elementInputs,
            System.Collections.Generic.List<ElementDamageResult> outElementResults,
            bool includeStagger = false,
            float? critRoll01 = null,
            bool forceCrit = false,
            bool critAffectsElement = true)
        {
            var r = new DamageResult();
            if (outElementResults != null) outElementResults.Clear();
            if (attacker == null || stats == null) return r;

            float atkAdd = Get(attacker, stats.attackAdd, 0f);
            float atkMul = GetMul(attacker, stats.attackMul, stats.attackMulMode);
            float finalMul = GetMul(attacker, stats.finalMul, stats.finalMulMode);

            float critChance = Mathf.Clamp01(Get(attacker, stats.critChance, 0f));
            float critMul = GetMul(attacker, stats.critMultiplier, stats.critMulMode);
            bool isCrit = forceCrit || (Roll(critRoll01) < critChance);
            float critFactor = isCrit ? critMul : 1f;
            r.isCrit = isCrit;

            // HP (same as legacy)
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

            // Elements (multi-channel)
            // NOTE: Channel existence is based on the presence of an entry in elementInputs,
            // not on baseDamage being > 0. (baseDamage may be 0 and still become >0 via relic/buff adds.)
            // To reduce inspector maintenance when adding new elements, optional default channels can be injected
            // from DamageFormulaStats.defaultElementChannels (processed with baseDamage=0).
            // 'elementInputs != null' is treated as an opt-in signal that this hit participates in
            // the element buildup system. (Pass null to fully disable element buildup for this hit.)
            bool elementEnabled = elementInputs != null;
            bool hasAuthoredEntries = elementEnabled && elementInputs.Count > 0;
            bool hasDefaultChannels = elementEnabled && stats.injectDefaultElementChannels && stats.defaultElementChannels != null && stats.defaultElementChannels.Length > 0;

            if (hasAuthoredEntries || hasDefaultChannels)
            {
                float elementCritFactor = critAffectsElement ? critFactor : 1f;
                float sum = 0f;

                // 1) Authored channels (per-attack)
                if (hasAuthoredEntries)
                {
                    for (int i = 0; i < elementInputs.Count; i++)
                    {
                        var input = elementInputs[i];
                        if (input.elementType == null) continue;

                        float elAdd;
                        float elMul;

                        var bind = stats.GetElementScaling(input.elementType);
                        if (bind != null)
                        {
                            elAdd = Get(attacker, bind.elementAdd, 0f);
                            elMul = GetMul(attacker, bind.elementMul, bind.elementMulMode);
                        }
                        else
                        {
                            // legacy fallback
                            elAdd = Get(attacker, stats.elementAdd, 0f);
                            elMul = GetMul(attacker, stats.elementMul, stats.elementMulMode);
                        }

                        float dmg = (input.baseDamage + elAdd) * elMul * finalMul * elementCritFactor;
                        dmg = Mathf.Max(0f, dmg);
                        sum += dmg;

                        if (outElementResults != null)
                        {
                            // 유물/버프로 더해진 값까지 포함해서 “최종 입력” 만들기
                            float pre = input.baseDamage + elAdd;

                            // (정책) 최종이 0 이하면 이번 히트에서는 축적 없음
                            if (pre <= 0f) continue;

                            float elDamage = pre * elMul * finalMul * elementCritFactor;
                            if (elDamage <= 0f) continue;

                            outElementResults.Add(new ElementDamageResult { elementType = input.elementType, damage = elDamage });
                        }
                    }
                }

                // 2) Default channels (profile-level) injected with baseDamage=0 when missing in authored inputs
                if (hasDefaultChannels)
                {
                    var defaults = stats.defaultElementChannels;
                    for (int d = 0; d < defaults.Length; d++)
                    {
                        var elementType = defaults[d];
                        if (elementType == null) continue;

                        // Skip if already authored
                        bool alreadyAuthored = false;
                        if (hasAuthoredEntries)
                        {
                            for (int i = 0; i < elementInputs.Count; i++)
                            {
                                if (elementInputs[i].elementType == elementType)
                                {
                                    alreadyAuthored = true;
                                    break;
                                }
                            }
                        }
                        if (alreadyAuthored) continue;

                        float elAdd;
                        float elMul;

                        var bind = stats.GetElementScaling(elementType);
                        if (bind != null)
                        {
                            elAdd = Get(attacker, bind.elementAdd, 0f);
                            elMul = GetMul(attacker, bind.elementMul, bind.elementMulMode);
                        }
                        else
                        {
                            elAdd = Get(attacker, stats.elementAdd, 0f);
                            elMul = GetMul(attacker, stats.elementMul, stats.elementMulMode);
                        }

                        // baseDamage=0 injection
                        float dmg = (0f + elAdd) * elMul * finalMul * elementCritFactor;
                        dmg = Mathf.Max(0f, dmg);
                        sum += dmg;

                        if (outElementResults != null)
                        {
                            float pre = 0f + elAdd;
                            if (pre <= 0f) continue;
                            float elDamage = pre * elMul * finalMul * elementCritFactor;
                            if (elDamage <= 0f) continue;
                            outElementResults.Add(new ElementDamageResult { elementType = elementType, damage = elDamage });
                        }
                    }
                }

                r.elementDamage = sum;
            }

            return r;
        }

        // ------------------------------------------------------------------
        // Legacy wrapper: keeps old call sites compiling.
        // - includeElement=true with no elementInputs will compute the legacy single elementDamage.
        // ------------------------------------------------------------------
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
            // If caller wants multi-element, use the new overload.
            // Otherwise keep the exact legacy behaviour.
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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// A reusable "stat-scaled" formula:
    ///   Value = Sum( sourceAttribute * rate + flat )
    ///
    /// Examples:
    /// - ATK * 100% + HP * 1%  => terms: (ATK, 1.0), (HP, 0.01)
    /// - KnockbackPower * 80%  => terms: (KnockbackPower, 0.8)
    ///
    /// Design notes:
    /// - rate uses 1.0 = 100% convention.
    /// - flat is optional (can remain 0 to avoid direct base values).
    /// - This is attacker-side computation (reads from the instigator's AttributeSet).
    /// </summary>
    [CreateAssetMenu(fileName = "SF_NewScaledFormula", menuName = "GAS/Formula/Scaled Stat Formula")]
    public sealed class ScaledStatFormula : ScriptableObject
    {
        [Serializable]
        public struct Term
        {
            [Header("Source")]
            [Tooltip("If true, this term queries the stat provider by StatId (recommended). If false, it reads the AttributeDefinition directly (legacy).")]
            public bool useStatId;

            [Tooltip("StatId queried from IStatProvider when useStatId is enabled.")]
            public StatId statId;

            [Tooltip("Attacker-side attribute used as the source value.")]
            public AttributeDefinition sourceAttribute;

            [Tooltip("Rate (1.0 = 100%, 0.01 = 1%).")]
            public float rate;

            [Tooltip("Optional flat addition for this term (can be 0).")]
            public float flat;
        }

        [SerializeField] private List<Term> terms = new List<Term>();

        // Runtime-compiled cache per AttributeSet instance (avoids repeated Dictionary lookups per hit)
        private readonly Dictionary<int, CompiledFormula> _compiledBySource = new Dictionary<int, CompiledFormula>(8);
        private int _termsHash;

        private sealed class CompiledFormula
        {
            public CompiledTerm[] terms;
            public int termsHash;
        }

        private readonly struct CompiledTerm
        {
            public readonly IReadOnlyAttributeValue value;
            public readonly StatId statId;
            public readonly bool useStatId;
            public readonly float rate;
            public readonly float flat;

            public CompiledTerm(IReadOnlyAttributeValue value, StatId statId, bool useStatId, float rate, float flat)
            {
                this.value = value;
                this.statId = statId;
                this.useStatId = useStatId;
                this.rate = rate;
                this.flat = flat;
            }
        }


        public IReadOnlyList<Term> Terms => terms;

        private void OnValidate()
        {
            _compiledBySource.Clear();
            _termsHash = 0;
        }



        public float Evaluate(AttributeSet source, float defaultIfEmpty = 0f)
        {
            if (source == null) return defaultIfEmpty;
            if (terms == null || terms.Count == 0) return defaultIfEmpty;

            var compiled = GetOrBuildCompiled(source);
            if (compiled == null || compiled.terms == null || compiled.terms.Length == 0) return defaultIfEmpty;

            float v = 0f;
            var cterms = compiled.terms;
            for (int i = 0; i < cterms.Length; i++)
            {
                var t = cterms[i];
                // NOTE: StatId-backed terms require a provider to resolve.
                // For AttributeSet-only evaluation, StatId terms contribute 0 unless they also have sourceAttribute set.
                float src;
                if (t.useStatId)
                {
                    // Fallback: if author also provided sourceAttribute, use it.
                    src = t.value != null ? t.value.CurrentValue : 0f;
                }
                else
                {
                    src = t.value != null ? t.value.CurrentValue : 0f;
                }
                v += src * t.rate + t.flat;
            }
            return v;
        }

        /// <summary>
        /// New: Evaluate using an <see cref="IStatProvider"/> (StatId-based), while still supporting legacy AD terms.
        /// - For terms with useStatId=true, values are read from provider.
        /// - For terms with useStatId=false, values are read from the given AttributeSet.
        ///
        /// NOTE: This does NOT use compiled caching because StatId queries are already O(1) in provider.
        /// </summary>
        public float Evaluate(AttributeSet source, IStatProvider provider, float defaultIfEmpty = 0f)
        {
            if (terms == null || terms.Count == 0) return defaultIfEmpty;
            if (source == null && provider == null) return defaultIfEmpty;

            float v = 0f;
            for (int i = 0; i < terms.Count; i++)
            {
                var t = terms[i];
                float src = 0f;

                if (t.useStatId)
                {
                    if (provider != null && t.statId != StatId.None)
                        src = provider.Get(t.statId);
                    else if (source != null && t.sourceAttribute != null)
                        src = source.GetAttributeValue(t.sourceAttribute);
                }
                else
                {
                    if (source != null && t.sourceAttribute != null)
                        src = source.GetAttributeValue(t.sourceAttribute);
                }

                v += src * t.rate + t.flat;
            }
            return v;
        }

        private CompiledFormula GetOrBuildCompiled(AttributeSet source)
        {
            int sid = source.GetInstanceID();

            int hash = ComputeTermsHash();
            if (_compiledBySource.TryGetValue(sid, out var c) && c != null && c.termsHash == hash)
                return c;

            var built = new CompiledFormula
            {
                termsHash = hash,
                terms = BuildCompiledTerms(source)
            };
            _compiledBySource[sid] = built;
            return built;
        }

        private CompiledTerm[] BuildCompiledTerms(AttributeSet source)
        {
            // Build once per source (or when formula changes)
            var outTerms = new CompiledTerm[terms.Count];
            for (int i = 0; i < terms.Count; i++)
            {
                var t = terms[i];
                IReadOnlyAttributeValue v = null;
                // Legacy compilation is only for AD-backed terms.
                if (t.sourceAttribute != null)
                    source.TryGetReadOnly(t.sourceAttribute, out v);

                outTerms[i] = new CompiledTerm(v, t.statId, t.useStatId, t.rate, t.flat);
            }
            return outTerms;
        }

        private int ComputeTermsHash()
        {
            // Lightweight change detector; only used to invalidate compiled caches when terms change.
            unchecked
            {
                int h = 17;
                h = h * 31 + (terms != null ? terms.Count : 0);
                if (terms != null)
                {
                    for (int i = 0; i < terms.Count; i++)
                    {
                        var t = terms[i];
                        h = h * 31 + (t.useStatId ? 1 : 0);
                        h = h * 31 + (int)t.statId;
                        h = h * 31 + (t.sourceAttribute != null ? t.sourceAttribute.GetInstanceID() : 0);
                        h = h * 31 + t.rate.GetHashCode();
                        h = h * 31 + t.flat.GetHashCode();
                    }
                }
                return h;
            }
        }



        public string BuildDebugString(AttributeSet source = null)
        {
            if (terms == null || terms.Count == 0) return "(empty)";
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < terms.Count; i++)
            {
                var t = terms[i];
                if (!t.useStatId && t.sourceAttribute == null) continue;
                if (sb.Length > 0) sb.Append(" + ");
                float src = 0f;
                if (t.useStatId)
                {
                    sb.Append(t.statId.ToString());
                }
                else
                {
                    src = source != null ? source.GetAttributeValue(t.sourceAttribute) : 0f;
                    sb.Append(t.sourceAttribute.name);
                }
                sb.Append(" * ");
                sb.Append((t.rate * 100f).ToString("0.##"));
                sb.Append("%");
                if (Mathf.Abs(t.flat) > 0.0001f)
                {
                    sb.Append(t.flat >= 0 ? " + " : " - ");
                    sb.Append(Mathf.Abs(t.flat).ToString("0.##"));
                }
                if (source != null)
                {
                    sb.Append(" (");
                    sb.Append((src * t.rate + t.flat).ToString("0.##"));
                    sb.Append(")");
                }
            }
            return sb.ToString();
        }
    }
}

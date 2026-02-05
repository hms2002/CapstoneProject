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
            public readonly float rate;
            public readonly float flat;

            public CompiledTerm(IReadOnlyAttributeValue value, float rate, float flat)
            {
                this.value = value;
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
                float src = t.value != null ? t.value.CurrentValue : 0f;
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
                if (t.sourceAttribute != null)
                    source.TryGetReadOnly(t.sourceAttribute, out v);

                outTerms[i] = new CompiledTerm(v, t.rate, t.flat);
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
                if (t.sourceAttribute == null) continue;
                if (sb.Length > 0) sb.Append(" + ");
                float src = source != null ? source.GetAttributeValue(t.sourceAttribute) : 0f;
                sb.Append(t.sourceAttribute.name);
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

using System;
using UnityEngine;
using UnityGAS;

namespace UnityGAS
{
    [Serializable]
    public sealed class ElementFormulaEntry
    {
        public GameplayTag elementType;
        public ScaledStatFormula formula;
    }

    /// <summary>
    /// Damage configuration for one hit.
    ///
    /// Solution 1 policy:
    /// - ScaledStatFormula outputs FINAL values.
    /// - Common post-process (crit/global multipliers) is applied afterward.
    /// </summary>
    [Serializable]
    public sealed class DamagePayloadConfig
    {
        [Header("Channels")]
        public bool includeStaggerBuildUp = true;
        public bool includeElementBuildUp = true;

        [Header("Optional Formulas")]
        public ScaledStatFormula staggerFormula;

        [Tooltip("Legacy per-hit element formulas. Applied build-up is resolved from ElementOffenseSource.")]
        public ElementFormulaEntry[] elementFormulas;

        [Tooltip("Legacy flag for per-hit element formulas. Ignored by the applied build-up path.")]
        public bool critAffectsElement = true;

        public bool HasElementFormulas => elementFormulas != null && elementFormulas.Length > 0;
    }
}
/// - Common post-process (crit/global multipliers) is applied afterwards.
/// </summary>
[Serializable]
public sealed class DamagePayloadConfig
{
    [Header("Channels")]
    public bool includeStaggerBuildUp = true;
    public bool includeElementBuildUp = true;

    [Header("Optional Formulas")]
    [Tooltip("Optional stagger formula. If null, stagger build-up is treated as 0.")]
    public ScaledStatFormula staggerFormula;

    [Tooltip("Legacy per-hit element formulas. Applied build-up is resolved from ElementOffenseSource.")]
    public ElementFormulaEntry[] elementFormulas;

    [Tooltip("Legacy flag for per-hit element formulas. Ignored by the applied build-up path.")]
    public bool critAffectsElement = true;

    public bool HasElementFormulas => elementFormulas != null && elementFormulas.Length > 0;

    
}

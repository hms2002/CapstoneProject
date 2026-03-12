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
        public ElementFormulaEntry[] elementFormulas;

        [Header("Post Process")]
        public DamagePostProcessStats postProcess;
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

    [Tooltip("Optional element formulas. Each formula outputs FINAL build-up value for its element.")]
    public ElementFormulaEntry[] elementFormulas;

    [Header("Post Process")]
    public DamagePostProcessStats postProcess;

    [Tooltip("If true, crit factor applies to element build-up as well.")]
    public bool critAffectsElement = true;

    public bool HasElementFormulas => elementFormulas != null && elementFormulas.Length > 0;

    
}

using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Shared post-process rules applied after Scaled formulas produced FINAL values.
    ///
    /// Multiplier attributes are expected to be stored as x1 factors (e.g., 1.25).
    /// </summary>
    [CreateAssetMenu(fileName = "DamagePostProcessStats", menuName = "GAS/Combat/Damage PostProcess Stats")]
    public sealed class DamagePostProcessStats : ScriptableObject
    {
        [Header("Crit")]
        public AttributeDefinition critChance;

        [Tooltip("Stored as x1 factor. Example: 1.5 means +50%.")]
        public AttributeDefinition critMultiplier;

        [Header("Global")]
        [Tooltip("Optional global final multiplier, stored as x1 factor.")]
        public AttributeDefinition finalMul;

        public float ReadCritChance(AttributeSet set)
        {
            if (set == null || critChance == null) return 0f;
            return Mathf.Clamp01(set.GetAttributeValue(critChance));
        }

        public float ReadCritMultiplier(AttributeSet set)
        {
            if (set == null || critMultiplier == null) return 1f;
            return Mathf.Max(0f, set.GetAttributeValue(critMultiplier));
        }

        public float ReadFinalMul(AttributeSet set)
        {
            if (set == null || finalMul == null) return 1f;
            return Mathf.Max(0f, set.GetAttributeValue(finalMul));
        }
    }
}

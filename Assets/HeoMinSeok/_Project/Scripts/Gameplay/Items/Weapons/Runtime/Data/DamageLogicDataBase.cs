using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Base class for ScriptableObject "LogicData" assets that deal damage.
    ///
    /// This enforces the existence of a shared <see cref="DamagePayloadConfig" />
    /// so other programmers cannot "forget" to add common channel fields.
    ///
    /// Implementation note:
    /// Unity only calls the most-derived OnValidate(). To ensure migration/validation
    /// runs, derived classes should call <see cref="ValidateDamageConfig" /> inside their OnValidate().
    /// </summary>
    public abstract class DamageLogicDataBase : ScriptableObject
    {
        [Header("Common Damage (Required)")]
        [SerializeField] private DamagePayloadConfig damageConfig = new DamagePayloadConfig();

        public DamagePayloadConfig DamageConfig => damageConfig;

        /// <summary>
        /// Call this from derived OnValidate() to ensure the config exists and
        /// (optionally) migrate legacy fields into it once.
        /// </summary>
        protected void ValidateDamageConfig(ref DamageFormulaStats legacyFormulaStats, ref bool legacyIncludeElement, ref bool legacyIncludeStagger)
        {
            if (damageConfig == null)
                damageConfig = new DamagePayloadConfig();

            // One-way migration: legacy -> new, only once.
            damageConfig.MigrateFromLegacyOnce(legacyFormulaStats, legacyIncludeElement, legacyIncludeStagger);

            // Keep legacy formulaStats in sync for existing code paths/editor blocks.
            // (We do not override legacy flags, to avoid surprising changes in existing assets.)
            if (damageConfig.formulaStats != null)
                legacyFormulaStats = damageConfig.formulaStats;
        }
    }
}

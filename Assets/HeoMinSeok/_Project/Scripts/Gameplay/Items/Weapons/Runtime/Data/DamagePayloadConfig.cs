using System;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Common damage-channel configuration shared across many LogicData assets.
    ///
    /// Design goals:
    /// - Make "damage channel" fields impossible to forget when authoring new LogicData.
    /// - Keep future maintenance localized when channels/policies evolve.
    /// </summary>
    [Serializable]
    public sealed class DamagePayloadConfig
    {
        // NOTE: We intentionally keep this as a nested-serializable class (not a ScriptableObject)
        // so it can live inside any LogicData asset without extra asset management.

        [Header("Formula")]
        [Tooltip("DamageFormulaStats used to compute damage (attacker-side). If null, systems may fallback to DamageProfile stats.")]
        public DamageFormulaStats formulaStats;

        [Header("Channels")]
        [Tooltip("If true, this hit participates in element buildup (gauge filling).")]
        public bool includeElementBuildup = true;

        [Tooltip("If true, this hit contributes to stagger buildup.")]
        public bool includeStaggerDamage = true;

        [Header("Policy")]
        [Tooltip("If true, critical hit multiplier applies to element buildup too.")]
        public bool critAffectsElement = true;

        // Serialized marker used for safe one-way migration from legacy fields.
        [SerializeField, HideInInspector]
        private bool _migratedFromLegacy;

        public bool IsMigratedFromLegacy => _migratedFromLegacy;

        /// <summary>
        /// One-time migration helper: if this config hasn't been migrated yet,
        /// copy values from legacy fields into this config.
        /// </summary>
        public void MigrateFromLegacyOnce(DamageFormulaStats legacyFormulaStats, bool legacyIncludeElement, bool legacyIncludeStagger)
        {
            if (_migratedFromLegacy) return;

            formulaStats = legacyFormulaStats;
            includeElementBuildup = legacyIncludeElement;
            includeStaggerDamage = legacyIncludeStagger;

            _migratedFromLegacy = true;
        }
    }
}

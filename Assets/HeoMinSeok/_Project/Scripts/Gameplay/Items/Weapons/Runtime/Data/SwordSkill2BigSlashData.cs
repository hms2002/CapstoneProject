using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "SwordSkill2_BigSlashData", menuName = "GAS/Samples/Sword Skill2 BigSlash Data")]
    public class SwordSkill2BigSlashData : DamageLogicDataBase
    {
        [Header("Damage Formula (Legacy - migrated to Damage)")]
        [SerializeField, HideInInspector] public DamageFormulaStats formulaStats;
        [SerializeField, HideInInspector] public bool includeElementDamage = false;
        [Tooltip("Element damages (can contain multiple elements per hit).")]
        public List<ElementDamageInput> elementDamages = new();
        [SerializeField, HideInInspector] public bool includeStaggerDamage = false;
        public float baseStaggerDamage = 0f;

        public Vector2 hitboxSize = new Vector2(4f, 4f);
        public float forwardOffset = 1.0f;
        public LayerMask hitLayers;

        public GameplayEffect damageEffect; // GE_Damage_Spec (HP)

        [Header("Damage / Knockback Formula")]
        [Tooltip("If set, base HP damage is computed from attacker stats via this formula.\nIf null, legacy 'damage' is used.")]
        public ScaledStatFormula damageFormula;

        [Tooltip("If set, knockback impulse is computed from attacker stats via this formula.")]
        public ScaledStatFormula knockbackFormula;

        [Header("Legacy Base Damage (Deprecated)")]
        public float damage = 50f;          // 무기 스킬 기본 피해량


        public GameplayTag hitEventTag;
        public float hitEventTimeout = 0.4f;

        public float recoveryOverride = 0.2f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateDamageConfig(ref formulaStats, ref includeElementDamage, ref includeStaggerDamage);
        }
#endif
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "SwordSkill2_BigSlashData", menuName = "GAS/Samples/Sword Skill2 BigSlash Data")]
    public class SwordSkill2BigSlashData : ScriptableObject
    {
        [Header("Damage Channels")]
        [SerializeField] private DamagePayloadConfig damageConfig = new();
        public DamagePayloadConfig DamageConfig => damageConfig;

        [Tooltip("Legacy per-hit element damages (FINAL values). Optional if you use DamageConfig.elementFormulas instead.")]
        public List<ElementDamageInput> elementDamages = new();

        [Tooltip("Legacy stagger damage (FINAL value). Optional if you use DamageConfig.staggerFormula instead.")]
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

    }
}

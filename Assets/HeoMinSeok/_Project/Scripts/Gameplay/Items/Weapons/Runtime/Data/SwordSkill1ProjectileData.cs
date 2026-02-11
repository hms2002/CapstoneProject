using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "SwordSkill1_ProjectileData", menuName = "GAS/Samples/Sword Skill1 Projectile Data")]
    public class SwordSkill1ProjectileData : ScriptableObject, IDetailProvider
    {
        [Header("Damage Channels")]
        [SerializeField] private DamagePayloadConfig damageConfig = new();
        public DamagePayloadConfig DamageConfig => damageConfig;

        [Tooltip("Legacy per-hit element damages (FINAL values). Optional if you use DamageConfig.elementFormulas instead.")]
        public List<ElementDamageInput> elementDamages = new();

        [Tooltip("Legacy stagger damage (FINAL value). Optional if you use DamageConfig.staggerFormula instead.")]
        public float baseStaggerDamage = 0f;

        public GameObject projectilePrefab;
        public float projectileSpeed = 12f;
        public float lifetime = 2.5f;

        public LayerMask wallLayers;
        public LayerMask damageLayers;

        public GameplayEffect damageEffect; // GE_Damage_Spec (HP)

        [Header("Damage / Knockback Formula")]
        [Tooltip("If set, base HP damage is computed from attacker stats via this formula.\nIf null, legacy 'damage' is used.")]
        public ScaledStatFormula damageFormula;

        [Tooltip("If set, knockback impulse is computed from attacker stats via this formula.")]
        public ScaledStatFormula knockbackFormula;

        [Header("Legacy Base Damage (Deprecated)")]
        public float damage = 20f;          // 무기 스킬 기본 피해량

        public Vector3 spawnOffset = new Vector3(0.8f, 0.2f, 0f);
        public ItemDetailBlock BuildDetailBlock(ItemDetailContext ctx)
        {
            return new ItemDetailBlock
            {
                title = "투사체 스킬",
                body = (damageFormula != null)
                    ? $"피해(공식): {damageFormula.BuildDebugString(ctx.attributeSet)}"
                    : $"피해: {damage}"
            };
        }
    }
}

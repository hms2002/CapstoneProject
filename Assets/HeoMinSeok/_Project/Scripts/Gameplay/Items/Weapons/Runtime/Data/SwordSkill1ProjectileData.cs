using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "SwordSkill1_ProjectileData", menuName = "GAS/Samples/Sword Skill1 Projectile Data")]
    public class SwordSkill1ProjectileData : DamageLogicDataBase, IDetailProvider
    {
        [Header("Damage Formula (Legacy - migrated to Damage)")]
        [SerializeField, HideInInspector] public DamageFormulaStats formulaStats;
        [SerializeField, HideInInspector] public bool includeElementDamage = false;
        [Tooltip("Element damages (can contain multiple elements per hit).")]
        public List<ElementDamageInput> elementDamages = new();
        [SerializeField, HideInInspector] public bool includeStaggerDamage = false;
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
            var stats = DamageConfig != null && DamageConfig.formulaStats != null ? DamageConfig.formulaStats : formulaStats;
            float atk = (stats != null)
                ? ctx.attributeSet.GetAttributeValue(stats.attackAdd)
                : 0f;
            return new ItemDetailBlock
            {
                title = "투사체 스킬",
                body = (damageFormula != null)
                    ? $"피해(공식): {damageFormula.BuildDebugString(ctx.attributeSet)}"
                    : $"피해: {damage}+[[공격력(+)]]({atk:F0})"
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateDamageConfig(ref formulaStats, ref includeElementDamage, ref includeStaggerDamage);
        }
#endif
    }
}

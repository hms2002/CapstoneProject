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
                body = $"피해: {damage}+[[공격력(+)]]({atk:F0})"
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

using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "DamageFormulaStats", menuName = "GAS/Damage/Damage Formula Stats")]
    public class DamageFormulaStats : ScriptableObject
    {
        public enum MultiplierMode
        {
            AsFactor,           // 1.15 같은 배율
            AsAdditivePercent   // 0.15 같은 퍼센트 (1 + 값으로 배율화)
        }

        [Header("Attack Power")]
        public AttributeDefinition attackAdd;   // 공격력(+)
        public AttributeDefinition attackMul;   // 공격력(*)
        public MultiplierMode attackMulMode = MultiplierMode.AsAdditivePercent;

        [Header("Normal Attack Power")]
        public AttributeDefinition normalAdd;   // 일반공격력(+)
        public AttributeDefinition normalMul;   // 일반공격력(*)
        public MultiplierMode normalMulMode = MultiplierMode.AsAdditivePercent;

        [Header("Skill Attack Power")]
        public AttributeDefinition skillAdd;    // 스킬공격력(+)
        public AttributeDefinition skillMul;    // 스킬공격력(*)
        public MultiplierMode skillMulMode = MultiplierMode.AsAdditivePercent;

        [Header("Crit")]
        public AttributeDefinition critChance;      // 치명타 확률(0~1 권장)
        public AttributeDefinition critMultiplier;  // 치명타 배율(보통 1.5)
        public MultiplierMode critMulMode = MultiplierMode.AsFactor;

        [Header("Stagger")]
        public AttributeDefinition staggerAdd;  // 무력화 피해(+)
        public AttributeDefinition staggerMul;  // 무력화 피해(*)
        public MultiplierMode staggerMulMode = MultiplierMode.AsAdditivePercent;

        [Header("Element")]
        public AttributeDefinition elementAdd;  // 속성피해량(+)
        public AttributeDefinition elementMul;  // 속성피해량(*)
        public MultiplierMode elementMulMode = MultiplierMode.AsAdditivePercent;

        [Header("Final Damage")]
        public AttributeDefinition finalMul;    // 최종피해 증가(*)
        public MultiplierMode finalMulMode = MultiplierMode.AsAdditivePercent;
    }
}

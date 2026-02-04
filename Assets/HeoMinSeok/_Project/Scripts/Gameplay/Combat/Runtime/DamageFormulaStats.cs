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

        [System.Serializable]
        public class ElementScaling
        {
            [Tooltip("Element type tag. e.g. Element.Fire / Element.Bleed / Element.Poison")]
            public GameplayTag elementType;
            public AttributeDefinition elementAdd;  // 해당 속성 피해량(+)
            public AttributeDefinition elementMul;  // 해당 속성 피해량(*)
            public MultiplierMode elementMulMode = MultiplierMode.AsAdditivePercent;
        }

        [Header("Element")]
        [Tooltip("Per-element scaling bindings. If a binding is missing, legacy Element(Add/Mul) is used as fallback.")]
        public ElementScaling[] elementScalings;

        [Header("Element Channels (Defaults)")]
        [Tooltip("If enabled, these element channels are treated as present for every hit, even when the attack did not author an entry in elementInputs.\n\nUse this to avoid per-skill/weapon inspector edits when adding new elements (e.g., make FireAdd relics work on any hit).\n\nIMPORTANT: Only channels listed here are auto-considered; attacks that should NEVER contribute to elements can still pass null/empty elementInputs and you can disable injection per profile by turning this off.")]
        public bool injectDefaultElementChannels = true;

        [Tooltip("Default element channels considered present for every hit when 'injectDefaultElementChannels' is true.\nThese are processed with baseDamage=0, so buffs/relics can still make them >0.")]
        public GameplayTag[] defaultElementChannels;

        [Header("Element (Legacy / Fallback)")]
        public AttributeDefinition elementAdd;  // (fallback) 속성피해량(+)
        public AttributeDefinition elementMul;  // (fallback) 속성피해량(*)
        public MultiplierMode elementMulMode = MultiplierMode.AsAdditivePercent;

        [Header("Final Damage")]
        public AttributeDefinition finalMul;    // 최종피해 증가(*)
        public MultiplierMode finalMulMode = MultiplierMode.AsAdditivePercent;

        /// <summary>
        /// Returns the per-element scaling binding for the given element type.
        /// If not found or invalid, returns null (caller may use legacy fallback).
        /// </summary>
        public ElementScaling GetElementScaling(GameplayTag elementType)
        {
            if (elementType == null) return null;
            if (elementScalings == null) return null;
            for (int i = 0; i < elementScalings.Length; i++)
            {
                var e = elementScalings[i];
                if (e != null && e.elementType == elementType)
                    return e;
            }
            return null;
        }
    }
}

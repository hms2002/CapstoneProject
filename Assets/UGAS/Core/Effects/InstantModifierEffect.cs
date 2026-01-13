using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "NewInstantModifierEffect", menuName = "GAS/Effects/Instant Modifier")]
    public class InstantModifierEffect : GameplayEffect
    {
        [Header("Modifier")]
        public AttributeDefinition attribute;
        public ModifierType type;
        public float value;

        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1)
        {
            var attributeSet = target.GetComponent<AttributeSet>();
            if (attributeSet == null || attribute == null) return;

            var attrValue = attributeSet.GetAttribute(attribute);
            if (attrValue == null) return;

            float modValue = value * stackCount;
            if (type == ModifierType.Flat)
            {
                attrValue.BaseValue += modValue;
            }
            else if (type == ModifierType.Percent)
            {
                // Note: Percent modifier on instant effects can be tricky.
                // This implementation modifies the base value, which is one way to do it.
                attrValue.BaseValue *= (1 + modValue);
            }
        }

        public override void Remove(GameObject target, GameObject instigator)
        {
            // Instant effects typically don't have a remove action.
        }
    }
}
using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "NewDurationModifierEffect", menuName = "GAS/Effects/Duration Modifier")]
    public class DurationModifierEffect : GameplayEffect
    {
        [Header("Modifier")]
        public AttributeDefinition attribute;
        public ModifierType type;
        public float value;

        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1)
        {
            var attributeSet = target.GetComponent<AttributeSet>();
            if (attributeSet == null || attribute == null) return;

            // Remove previous instances from the same source to ensure a clean re-application
            attributeSet.GetAttribute(attribute)?.RemoveModifiersFromSource(this);

            var modifier = new AttributeModifier(type, value * stackCount, this, duration);
            attributeSet.AddModifier(attribute, modifier);
        }

        public override void Remove(GameObject target, GameObject instigator)
        {
            var attributeSet = target.GetComponent<AttributeSet>();
            if (attributeSet == null || attribute == null) return;
            attributeSet.GetAttribute(attribute)?.RemoveModifiersFromSource(this);
        }
    }
}
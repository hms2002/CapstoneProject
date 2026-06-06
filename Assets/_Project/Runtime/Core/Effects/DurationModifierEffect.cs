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
            if (target == null || attribute == null) return;

            var attributeSet = target.GetComponent<AttributeSet>();
            if (attributeSet == null) return;

            // BaseOnly 속성(예: HP)에는 Duration Modifier를 얹지 않는다.
            if (attribute.IsBaseOnly())
            {
                Debug.LogWarning(
                    $"[DurationModifierEffect] '{attribute.attributeName}' 은(는) BaseOnly 속성이므로 Duration Modifier를 적용할 수 없습니다. " +
                    $"Effect: {name}");
                return;
            }

            // 같은 source(this)에서 들어간 기존 modifier를 먼저 제거해서 재적용을 깔끔하게 만든다.
            attributeSet.RemoveModifiersFromSource(this);

            var modifier = new AttributeModifier(type, value * stackCount, this, duration);
            attributeSet.TryAddModifier(attribute, modifier);
        }

        public override void Remove(GameObject target, GameObject instigator)
        {
            if (target == null || attribute == null) return;

            var attributeSet = target.GetComponent<AttributeSet>();
            if (attributeSet == null) return;

            // 같은 source(this)로 들어간 modifier 제거
            attributeSet.RemoveModifiersFromSource(this);
        }
    }
}
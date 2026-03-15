using UnityEngine;

namespace UnityGAS
{
    /*
    Responsibility:
    - GameplayEffect 에셋 기반으로 "즉발(1회성) Base 값 변화"를 적용한다.
    - 주로 HP 데미지/회복, 영구 스탯 커밋(레벨업/보상) 같은 Base 값 변경을 표현하기 위해 존재한다.

    Important Notes / Warnings:
    - 이 효과는 AttributeModifier(overlay, 지속/영구 버프)와 의미가 다르다.
      * AttributeModifier: 계산식에 얹히는 수정(제거/만료 가능)
      * 이 클래스: BaseValue 자체를 즉시 변경하는 커밋
    - Percent 적용은 특히 주의:
      * BaseValue에 곱해져 "영구 성장"처럼 동작할 수 있어 지속 % 버프와 의미가 다르다.
      * BaseOnly 속성(예: HP)에는 Percent 적용을 금지한다.
    - 외부 코드는 AttributeSet 경유로 값을 바꾸는 것을 원칙으로 한다.
    */
    [CreateAssetMenu(fileName = "NewInstantBaseValueEffect", menuName = "GAS/Effects/Instant Modifier")]
    public class InstantBaseValueEffect : GameplayEffect
    {
        [Header("Delta")]
        public AttributeDefinition attribute;
        public ModifierType type;
        public float value;

        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1)
        {
            if (target == null || attribute == null) return;

            var attributeSet = target.GetComponent<AttributeSet>();
            if (attributeSet == null) return;

            var attrValue = attributeSet.GetAttribute(attribute);
            if (attrValue == null) return;

            float finalValue = value * stackCount;

            switch (type)
            {
                case ModifierType.Flat:
                    {
                        // BaseOnly / BaseAndModifier 모두 허용
                        // "즉시 Base 변화"라는 이 클래스의 성격과 맞음
                        attributeSet.TryModifyAttributeValue(attribute, finalValue, this);
                        break;
                    }

                case ModifierType.Percent:
                    {
                        // BaseOnly 속성(예: HP)에는 Percent 즉발 변경 금지
                        if (attribute.IsBaseOnly())
                        {
                            Debug.LogWarning(
                                $"[InstantBaseValueEffect] '{attribute.attributeName}' 은(는) BaseOnly 속성이므로 Percent 즉발 변경을 적용할 수 없습니다. " +
                                $"Effect: {name}");
                            return;
                        }

                        // Percent는 BaseValue 자체를 커밋형으로 변경한다.
                        // 예: 0.2 => BaseValue * 1.2
                        float currentBase = attrValue.BaseValue;
                        float newBase = currentBase * (1f + finalValue);
                        float delta = newBase - currentBase;

                        attributeSet.TryModifyAttributeValue(attribute, delta, this);
                        break;
                    }

                default:
                    Debug.LogWarning($"[InstantBaseValueEffect] 지원하지 않는 ModifierType 입니다. Effect: {name}");
                    break;
            }
        }

        public override void Remove(GameObject target, GameObject instigator)
        {
            // 즉발 Base 변경 효과는 보통 제거 동작이 없다.
        }
    }
}
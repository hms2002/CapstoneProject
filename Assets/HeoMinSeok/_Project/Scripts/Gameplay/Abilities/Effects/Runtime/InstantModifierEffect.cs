using UnityEngine;

namespace UnityGAS
{
    /*
Responsibility:
- GameplayEffect 에셋 기반으로 "즉발(1회성) Attribute 변화"를 적용한다.
- 주로 HP 데미지/회복, 영구 스탯 커밋(레벨업/보상) 같은 Base 값 변경을 표현하기 위해 존재한다.

Important Notes / Warnings:
- 이 효과는 AttributeModifier(overlay, 지속/영구 버프)와 의미가 다르다.
  * AttributeModifier: 계산식에 얹히는 수정(제거/만료 가능)
  * InstantModifierEffect: BaseValue 자체를 변경하는 커밋(되돌리기 어려움)
- Percent 적용은 특히 주의:
  * BaseValue에 곱해져 "영구 성장"처럼 동작할 수 있어(지속 % 버프와 의미 불일치).
  * 밸런스/디버깅 혼란을 유발하므로 기본적으로 사용 금지(또는 별도 규칙 필요).
- 이 구현이 AttributeSet 경유(ModifyAttributeValue/ForceRecalculate)를 거치지 않으면
  UI/이벤트/연쇄 트리거 반영 타이밍이 불일치할 수 있다(1프레임 지연/누락 위험).

Usage Rules:
- "상태값" 성격(HP 등)은 Instant(Add)로만 변경한다. (HP에 modifier를 얹지 않는 방향 권장)
- "버프/패시브/장비"는 Duration/Perma AttributeModifier를 사용한다.
- 새로운 스탯 % 증가는 Instant로 만들지 말고 Modifier로 표현한다.

Status / Future Plan:
- Deprecated 후보: Effect SO 내부에 AttributeDeltaSpec(ApplyTo: Current/Base) + ModifierSpec를 두는 구조로 통합 예정.
- 새 기능 추가 시 이 클래스를 복제/확장하지 말고, 통합 Effect 스펙 구조로 이동한다.
*/
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
                attrValue.AddBaseValue(modValue);
            }
            else if (type == ModifierType.Percent)
            {
                // Note: Percent modifier on instant effects can be tricky.
                // This implementation modifies the base value, which is one way to do it.
                attrValue.SetBaseValue (attrValue.BaseValue * (1 + modValue));
            }
        }

        public override void Remove(GameObject target, GameObject instigator)
        {
            // Instant effects typically don't have a remove action.
        }
    }
}
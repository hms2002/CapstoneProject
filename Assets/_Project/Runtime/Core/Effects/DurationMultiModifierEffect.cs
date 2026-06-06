using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 하나의 지속형 GameplayEffect가 여러 Attribute modifier를 함께 적용하게 만든다.
    /// - 여러 능력치가 하나의 버프/디버프 상태로 묶여야 할 때 HUD와 lifetime을 단일 정의로 유지한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDurationMultiModifierEffect", menuName = "GAS/Effects/Duration Multi Modifier")]
    public sealed class DurationMultiModifierEffect : GameplayEffect
    {
        [SerializeField] private List<ModifierEntry> modifiers = new();

        public override void Apply(GameObject target, GameObject instigator, int stackCount = 1)
        {
            if (target == null)
                return;

            AttributeSet attributeSet = target.GetComponent<AttributeSet>();
            if (attributeSet == null)
                return;

            // 같은 버프를 refresh할 때 이전 modifier를 지우고 현재 stack/duration 기준으로 다시 건다.
            attributeSet.RemoveModifiersFromSource(this);

            foreach (ModifierEntry entry in modifiers)
            {
                if (!entry.IsValid)
                    continue;

                if (entry.Attribute.IsBaseOnly())
                {
                    Debug.LogWarning(
                        $"[DurationMultiModifierEffect] '{entry.Attribute.attributeName}' 은(는) BaseOnly 속성이므로 Duration Modifier를 적용할 수 없습니다. Effect: {name}",
                        this);
                    continue;
                }

                AttributeModifier modifier = new(entry.Type, entry.Value * stackCount, this, duration);
                attributeSet.TryAddModifier(entry.Attribute, modifier);
            }
        }

        public override void Remove(GameObject target, GameObject instigator)
        {
            if (target == null)
                return;

            AttributeSet attributeSet = target.GetComponent<AttributeSet>();
            if (attributeSet == null)
                return;

            attributeSet.RemoveModifiersFromSource(this);
        }

        /// <summary>
        /// 책임 :
        /// - DurationMultiModifierEffect가 적용할 Attribute, modifier 타입, 수치를 한 항목으로 보관한다.
        /// - Unity inspector에서 다중 modifier 목록을 안전하게 authoring할 수 있게 한다.
        /// </summary>
        [Serializable]
        private sealed class ModifierEntry
        {
            [SerializeField] private AttributeDefinition attribute;
            [SerializeField] private ModifierType type;
            [SerializeField] private float value;

            public AttributeDefinition Attribute => attribute;
            public ModifierType Type => type;
            public float Value => value;
            public bool IsValid => attribute != null;
        }
    }
}

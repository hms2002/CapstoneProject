using DG.Tweening;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace UnityGAS
{
    /*
[프로젝트 규칙: HP는 상태값(State)이다]
- HP는 Modifier/Regeneration/Percent 변경을 금지한다.
- HP 변경은 델타(Add) 방식(데미지/회복)만 허용한다.
- 버프/패시브는 MaxHP 또는 DamageTakenMultiplier/HealingMultiplier 같은 별도 Attribute로 표현한다.
*/
    /*
    [책임]
    - (AttributeDefinition 1개에 대응하는) 런타임 Attribute 상태를 보관/갱신한다.
    - BaseValue(기본/상태값)과 CurrentValue(최종 계산값)를 관리한다.
    - Modifier(지속/영구 효과) 만료, 재생(옵션), 동적 최대치(MaxValueGetter) 등 시간/규칙 기반 처리를 수행한다.
    - 값 변경 시 OnValueChanged 이벤트를 발행한다.

    [규칙/계약]
    - CurrentValue는 "계산 결과"이므로 직접 Set하지 않는다(재계산 시 덮어써질 수 있음).
    - 외부 시스템은 AttributeValue를 직접 조작하지 말고 AttributeSet의 공식 API(델타/모디파이어)를 통해 변경한다.

    [주의/경고]
    - Percent(%) 의미가 '지속(overlay)'인지 '커밋(base 변경)'인지 섞이면 혼란/버그가 발생한다.
    - 이 Attribute가 HP처럼 '상태값'이라면 Modifier 허용 여부를 명확히 규정한다.
    */
    [Serializable]
    public class AttributeValue : IReadOnlyAttributeValue
    {
        public AttributeDefinition Definition { get; }
        public float BaseValue { get; private set; }
        public float CurrentValue { get; private set; }

        // ✅ 동적 Max (예: Health의 max를 MaxHealth.CurrentValue로)
        public Func<float> MaxValueGetter { get; private set; }

        private readonly List<AttributeModifier> modifiers = new List<AttributeModifier>();
        private float lastDamageTime;
        private bool keepBaseWithinClamp;

        public Action<float, float> OnValueChanged;
        private bool dirty;

        public AttributeValue(AttributeDefinition definition)
        {
            Definition = definition;
            BaseValue = definition.defaultBaseValue;
            dirty = true;
            RecalculateValue(); // 초기 CurrentValue 세팅
        }

        public void SetMaxValueGetter(Func<float> getter)
        {
            MaxValueGetter = getter;
            dirty = true;
        }

        /// <summary>
        /// 책임 : 이 AttributeValue가 재계산 시 base 값까지 clamp 영역 안으로 정규화할지 결정한다.
        /// Health처럼 상태형 값은 숨은 base 초과분이 남지 않도록 true로 바인딩한다.
        /// </summary>
        public void SetClampNormalizationPolicy(bool shouldKeepBaseWithinClamp)
        {
            keepBaseWithinClamp = shouldKeepBaseWithinClamp;
            dirty = true;
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        // ✅ 즉시 clamp가 필요한 경우(예: MaxHealth가 내려간 순간)
        public void ForceRecalculate()
        {
            dirty = false;
            RecalculateValue();
        }

        public void SetBaseValue(float value)
        {
            if (Math.Abs(BaseValue - value) < 0.0001f) return;
            BaseValue = value;
            dirty = true;
        }

        public void AddBaseValue(float delta)
        {
            if (Math.Abs(delta) < 0.0001f) return;
            BaseValue += delta;
            dirty = true;
        }

        public void AddModifier(AttributeModifier modifier)
        {
            modifiers.Add(modifier);
            dirty = true;
        }

        public void RemoveModifier(AttributeModifier modifier)
        {
            modifiers.Remove(modifier);
            dirty = true;
        }

        public void RemoveModifiersFromSource(UnityEngine.Object source)
        {
            if (modifiers.RemoveAll(mod => mod.Source == source) > 0)
                dirty = true;
        }

        public void ClearAllModifiers()
        {
            if (modifiers.Count == 0) return;
            modifiers.Clear();
            dirty = true;
        }

        /// <summary>
        /// 책임 : 현재 적용 중인 Flat modifier 총합을 계산해 제공한다.
        /// current 복원 시 base 역산의 입력값으로 사용된다.
        /// </summary>
        public float GetFlatModifierSum()
        {
            return modifiers.Where(m => m.Type == ModifierType.Flat).Sum(m => m.Value);
        }

        /// <summary>
        /// 책임 : 현재 적용 중인 Percent modifier 총합을 계산해 제공한다.
        /// current 복원 시 base 역산의 입력값으로 사용된다.
        /// </summary>
        public float GetPercentModifierSum()
        {
            return modifiers.Where(m => m.Type == ModifierType.Percent).Sum(m => m.Value);
        }

        /// <summary>
        /// 책임 : 현재 modifier/clamp 규칙을 유지한 채 목표 current 값을 만들 수 있도록
        /// 필요한 base 값을 역산하여 설정한다.
        /// 복원 시점의 현재 HP/MP 같은 상태값을 되살릴 때 사용한다.
        /// </summary>
        public bool TrySetCurrentValue(float targetCurrentValue)
        {
            float clampedTarget = Mathf.Clamp(targetCurrentValue, Definition.minValue, GetMaxForClamp());

            float flatSum = GetFlatModifierSum();
            float percentSum = GetPercentModifierSum();
            float multiplier = 1f + percentSum;

            // multiplier가 0이면 역산 불가
            if (Mathf.Abs(multiplier) < 0.0001f)
                return false;

            float requiredBase = (clampedTarget / multiplier) - flatSum;

            BaseValue = requiredBase;
            dirty = false;
            RecalculateValue();
            return true;
        }

        public void Update(float deltaTime)
        {
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                modifiers[i].Update(deltaTime);
                if (!modifiers[i].IsPermanent && modifiers[i].TimeRemaining <= 0)
                {
                    modifiers.RemoveAt(i);
                    dirty = true;
                }
            }

            float max = GetMaxForClamp();

            // ✅ regen도 동적 max 기준
            if (Definition.hasRegeneration && CurrentValue < max)
            {
                if (Time.time - lastDamageTime >= Definition.regenerationDelay)
                {
                    AddBaseValue(Definition.regenerationRate * deltaTime); // dirty=true
                }
            }

            if (dirty)
            {
                dirty = false;
                RecalculateValue();
            }
        }

        private float GetMaxForClamp()
        {
            float max = Definition.maxValue;
            if (MaxValueGetter != null)
            {
                try { max = MaxValueGetter(); }
                catch { max = Definition.maxValue; }
            }

            if (max < Definition.minValue) max = Definition.minValue;
            return max;
        }

        private void RecalculateValue()
        {
            float oldValue = CurrentValue;
            float max = GetMaxForClamp();
            float normalizedBase = BaseValue;

            if (keepBaseWithinClamp)
            {
                normalizedBase = Mathf.Clamp(BaseValue, Definition.minValue, max);
                BaseValue = normalizedBase;
            }

            float finalValue = normalizedBase;

            var flatModifiers = modifiers.Where(m => m.Type == ModifierType.Flat).Sum(m => m.Value);
            var percentModifiers = modifiers.Where(m => m.Type == ModifierType.Percent).Sum(m => m.Value);

            finalValue += flatModifiers;
            finalValue *= (1f + percentModifiers);

            CurrentValue = Mathf.Clamp(finalValue, Definition.minValue, max);

            if (Math.Abs(oldValue - CurrentValue) > 0.001f)
            {
                OnValueChanged?.Invoke(oldValue, CurrentValue);
                if (CurrentValue < oldValue)
                    lastDamageTime = Time.time;
            }
        }
    }
}

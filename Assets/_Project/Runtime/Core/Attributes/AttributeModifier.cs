using UnityEngine;

namespace UnityGAS
{

    /*
    AttributeModifier 책임:
    “AttributeValue가 최종 값을 계산할 때 적용되는 ‘수정 항목(Flat/Percent)’ 1개를 나타내며, 출처(Source)와 지속시간(TimeRemaining)을 통해 만료/일괄 제거가 가능하게 한다.”
     */
    [System.Serializable]
    public class AttributeModifier
    {
        public ModifierType Type { get; }
        public float Value { get; }
        public Object Source { get; } // e.g., GameplayEffect asset

        public float Duration { get; private set; }
        public bool IsPermanent => Duration <= 0;
        public float TimeRemaining { get; private set; }

        public AttributeModifier(ModifierType type, float value, Object source, float duration = 0)
        {
            Type = type;
            Value = value;
            Source = source;
            Duration = duration;
            TimeRemaining = duration;
        }

        public void Update(float deltaTime)
        {
            if (!IsPermanent)
            {
                TimeRemaining -= deltaTime;
            }
        }
    }
}
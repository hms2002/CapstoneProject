using UnityEngine;

namespace UnityGAS
{
    [System.Serializable]
    public class AttributeModifier
    {
        public ModifierType Type { get; }
        public float Value { get; }
        public Object Source { get; } // e.g., GameplayEffect asset

        public float Duration { get; set; }
        public bool IsPermanent => Duration <= 0;
        public float TimeRemaining { get; set; }

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
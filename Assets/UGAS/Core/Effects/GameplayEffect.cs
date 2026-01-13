using UnityEngine;
using System.Collections.Generic;

namespace UnityGAS
{
    public abstract class GameplayEffect : ScriptableObject
    {
        [Header("Info")]
        public string effectName = "New Effect";
        [TextArea] public string description = "Effect description.";
        public Sprite icon;

        [Header("Duration")]
        public float duration = 0f; // 0 for instant

        [Header("Stacking")]
        public bool canStack = false;
        public int maxStacks = 1;

        [Header("Granted Tags")]
        public List<GameplayTag> grantedTags = new List<GameplayTag>();

        // Properties
        public bool IsInstant => duration <= 0f;
        public bool IsDuration => duration > 0f;

        public abstract void Apply(GameObject target, GameObject instigator, int stackCount = 1);
        public abstract void Remove(GameObject target, GameObject instigator);

    }
}
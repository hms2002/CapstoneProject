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
        [Header("Granted Tag Sets (Optional)")]
        public List<GameplayTagSet> grantedTagSets = new();

        // -------------------------
        // GameplayCue (Cosmetic)
        // -------------------------
        [Header("GameplayCue (Optional)")]
        [Tooltip("Instant 효과가 실행될 때(또는 Duration 효과가 처음 적용될 때) 1회 실행되는 큐")]
        public GameplayTag cueOnExecute;

        [Tooltip("Duration 효과가 유지되는 동안 지속되는 큐(Add/Remove)")]
        public GameplayTag cueWhileActive;

        [Tooltip("Duration 효과가 제거될 때 1회 실행되는 큐")]
        public GameplayTag cueOnRemove;

        // Properties
        public bool IsInstant => duration <= 0f;
        public bool IsDuration => duration > 0f;

        public abstract void Apply(GameObject target, GameObject instigator, int stackCount = 1);
        public abstract void Remove(GameObject target, GameObject instigator);
    }
}

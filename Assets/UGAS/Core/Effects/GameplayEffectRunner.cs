using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityGAS
{
    public class GameplayEffectRunner : MonoBehaviour
    {
        private readonly List<ActiveGameplayEffect> activeEffects = new List<ActiveGameplayEffect>();
        private TagSystem tagSystem;

        private void Awake()
        {
            tagSystem = GetComponent<TagSystem>();
        }

        private void Update()
        {
            if (activeEffects.Count == 0) return;

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var activeEffect = activeEffects[i];
                activeEffect.TimeRemaining -= Time.deltaTime;
                if (activeEffect.TimeRemaining <= 0)
                {
                    EndEffect(activeEffect);
                    activeEffects.RemoveAt(i);
                }
            }
        }

        public void ApplyEffect(GameplayEffect effect, GameObject target, GameObject instigator)
        {
            if (effect == null || target == null) return;

            var existingEffect = activeEffects.FirstOrDefault(e => e.Effect == effect && e.Target == target);

            if (existingEffect != null)
            {
                if (effect.canStack && existingEffect.StackCount < effect.maxStacks)
                {
                    existingEffect.StackCount++;
                }
                // Always refresh duration
                existingEffect.TimeRemaining = effect.duration;
                // Re-apply effect to update modifier values with new stack count
                effect.Apply(target, instigator, existingEffect.StackCount);
            }
            else
            {
                if (effect.IsInstant)
                {
                    effect.Apply(target, instigator);
                }
                else // Is Duration
                {
                    var newActiveEffect = new ActiveGameplayEffect(effect, target, instigator);
                    activeEffects.Add(newActiveEffect);
                    effect.Apply(target, instigator);

                    var targetTags = target.GetComponent<TagSystem>();
                    if (targetTags != null)
                    {
                        targetTags.AddTags(effect.grantedTags);
                    }
                }
            }
        }

        public void RemoveEffect(GameplayEffect effect, GameObject target)
        {
            var activeEffect = activeEffects.FirstOrDefault(e => e.Effect == effect && e.Target == target);
            if (activeEffect != null)
            {
                EndEffect(activeEffect);
                activeEffects.Remove(activeEffect);
            }
        }

        private void EndEffect(ActiveGameplayEffect activeEffect)
        {
            activeEffect.Effect.Remove(activeEffect.Target, activeEffect.Instigator);

            var targetTags = activeEffect.Target.GetComponent<TagSystem>();
            if (targetTags != null)
            {
                targetTags.RemoveTags(activeEffect.Effect.grantedTags);
            }
        }
    }

    public class ActiveGameplayEffect
    {
        public GameplayEffect Effect { get; }
        public GameObject Target { get; }
        public GameObject Instigator { get; }
        public float TimeRemaining { get; set; }
        public int StackCount { get; set; }

        public ActiveGameplayEffect(GameplayEffect effect, GameObject target, GameObject instigator)
        {
            Effect = effect;
            Target = target;
            Instigator = instigator;
            TimeRemaining = effect.duration;
            StackCount = 1;
        }
    }
}
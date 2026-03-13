using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// ActiveGameplayEffect 런타임 저장소.
    /// 책임:
    /// - active effect 목록 보관
    /// - 검색 / 조회
    /// - 만료 tick
    /// - 남은 시간 조작
    /// 
    /// 정책:
    /// - "어떤 effect를 적용할지"는 모름
    /// - "끝날 때 어떤 side effect가 필요한지"도 모름
    /// - 종료 필요 시 콜백(onExpired)을 호출해 상위(Runner)가 정리하도록 함
    /// </summary>
    public sealed class ActiveGameplayEffectRepository
    {
        private readonly List<ActiveGameplayEffect> activeEffects = new();

        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects => activeEffects;

        public void Add(ActiveGameplayEffect effect)
        {
            if (effect == null) return;
            activeEffects.Add(effect);
        }

        public bool Remove(ActiveGameplayEffect effect)
        {
            if (effect == null) return false;
            return activeEffects.Remove(effect);
        }

        public ActiveGameplayEffect FindFirst(GameplayEffect effect, GameObject target)
        {
            if (effect == null || target == null) return null;

            return activeEffects.FirstOrDefault(e =>
                e.Effect == effect &&
                e.Target == target);
        }

        public ActiveGameplayEffect FindFirst(GameplayEffect effect, GameObject target, UnityEngine.Object sourceObject)
        {
            if (effect == null || target == null) return null;

            return activeEffects.FirstOrDefault(e =>
                e.Effect == effect &&
                e.Target == target &&
                e.SourceObject == sourceObject);
        }

        public bool HasActive(GameplayEffect effect, GameObject target)
        {
            if (effect == null || target == null) return false;

            return activeEffects.Any(e =>
                e.Effect == effect &&
                e.Target == target &&
                e.TimeRemaining > 0f);
        }

        public bool HasActive(GameplayEffect effect, GameObject target, UnityEngine.Object sourceObject)
        {
            if (effect == null || target == null) return false;

            return activeEffects.Any(e =>
                e.Effect == effect &&
                e.Target == target &&
                e.SourceObject == sourceObject &&
                e.TimeRemaining > 0f);
        }

        public float GetRemainingTime(GameplayEffect effect, GameObject target)
        {
            if (effect == null || target == null) return 0f;

            float max = 0f;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                var e = activeEffects[i];
                if (e.Effect == effect && e.Target == target && e.TimeRemaining > max)
                    max = e.TimeRemaining;
            }

            return max;
        }

        public float GetRemainingTime(GameplayEffect effect, GameObject target, UnityEngine.Object sourceObject)
        {
            if (effect == null || target == null || sourceObject == null) return 0f;

            float max = 0f;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                var e = activeEffects[i];
                if (e.Effect == effect &&
                    e.Target == target &&
                    e.SourceObject == sourceObject &&
                    e.TimeRemaining > max)
                {
                    max = e.TimeRemaining;
                }
            }

            return max;
        }

        public void Tick(float deltaTime, Action<ActiveGameplayEffect> onExpired)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var ae = activeEffects[i];
                ae.TimeRemaining -= deltaTime;

                if (ae.TimeRemaining <= 0f)
                {
                    onExpired?.Invoke(ae);
                    activeEffects.RemoveAt(i);
                }
            }
        }

        public int AdjustRemainingTime(
            Func<ActiveGameplayEffect, bool> predicate,
            Func<float, float> adjust,
            Action<ActiveGameplayEffect> onExpired)
        {
            if (predicate == null || adjust == null) return 0;

            int affected = 0;

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var ae = activeEffects[i];
                if (!predicate(ae)) continue;

                ae.TimeRemaining = adjust(ae.TimeRemaining);
                affected++;

                if (ae.TimeRemaining <= 0f)
                {
                    onExpired?.Invoke(ae);
                    activeEffects.RemoveAt(i);
                }
            }

            return affected;
        }
    }
}
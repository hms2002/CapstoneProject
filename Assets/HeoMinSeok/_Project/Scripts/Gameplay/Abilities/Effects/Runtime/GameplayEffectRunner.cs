using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityGAS
{
    public class GameplayEffectRunner : MonoBehaviour
    {
        private readonly List<ActiveGameplayEffect> activeEffects = new();
        [SerializeField] private GameplayCueManager cueManager;

        private void Awake()
        {
#if UNITY_2023_1_OR_NEWER
            if (cueManager == null) cueManager = Object.FindAnyObjectByType<GameplayCueManager>();
#else
            if (cueManager == null) cueManager = FindObjectOfType<GameplayCueManager>();
#endif
        }

        private void Update()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var ae = activeEffects[i];
                ae.TimeRemaining -= Time.deltaTime;
                if (ae.TimeRemaining <= 0f)
                {
                    EndEffect(ae);
                    activeEffects.RemoveAt(i);
                }
            }
        }

        // -------------------------
        // Query helpers (Cooldown/Relic 등에 필요)
        // -------------------------
        public bool HasActiveEffect(GameplayEffect effect, GameObject target)
        {
            if (effect == null || target == null) return false;
            return activeEffects.Any(e => e.Effect == effect && e.Target == target && e.TimeRemaining > 0f);
        }
        public bool HasActiveEffect(GameplayEffect effect, GameObject target, Object sourceObject)
        {
            if (effect == null || target == null) return false;

            // ✅ 쿨다운은 SourceObject(AbilityDefinition)로 구분
            if (effect is GE_Cooldown && sourceObject != null)
                return activeEffects.Any(e => e.Effect == effect && e.Target == target && e.SourceObject == sourceObject && e.TimeRemaining > 0f);

            return HasActiveEffect(effect, target);
        }
        public float GetRemainingTime(GameplayEffect effect, GameObject target)
        {
            if (effect == null || target == null) return 0f;
            float max = 0f;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                var e = activeEffects[i];
                if (e.Target == target && e.Effect == effect)
                    if (e.TimeRemaining > max) max = e.TimeRemaining;
            }
            return max;
        }
        public float GetRemainingTime(GameplayEffect effect, GameObject target, Object sourceObject)
        {
            if (effect == null || target == null) return 0f;

            if (effect is GE_Cooldown && sourceObject != null)
            {
                float max = 0f;
                for (int i = 0; i < activeEffects.Count; i++)
                {
                    var e = activeEffects[i];
                    if (e.Target == target && e.Effect == effect && e.SourceObject == sourceObject)
                        if (e.TimeRemaining > max) max = e.TimeRemaining;
                }
                return max;
            }

            return GetRemainingTime(effect, target);
        }
        // -------------------------
        // Non-Spec Apply (기존)
        // -------------------------
        public void ApplyEffect(GameplayEffect effect, GameObject target, GameObject instigator)
        {
            if (effect == null || target == null) return;

            var existing = activeEffects.FirstOrDefault(e => e.Effect == effect && e.Target == target);

            if (existing != null)
            {
                if (effect.canStack && existing.StackCount < effect.maxStacks)
                    existing.StackCount++;

                existing.TimeRemaining = effect.duration;
                existing.Instigator = instigator;
                existing.Causer = instigator;
                existing.SourceObject = effect;
                existing.Context = null;

                effect.Apply(target, instigator, existing.StackCount);

                FireEffectExecuteCue(effect, instigator, target, effect, existing.StackCount, null);
                if (effect.cueWhileActive != null)
                    cueManager?.AddCue(effect.cueWhileActive, BuildCueParams(instigator, instigator, target, effect, existing.StackCount, null));
                return;
            }

            if (effect.IsInstant)
            {
                effect.Apply(target, instigator);
                FireEffectExecuteCue(effect, instigator, target, effect, 1f, null);
                return;
            }

            var ae = new ActiveGameplayEffect(effect, target)
            {
                Instigator = instigator,
                Causer = instigator,
                SourceObject = effect,
                Context = null,
                TimeRemaining = effect.duration,
                StackCount = 1
            };
            activeEffects.Add(ae);

            // granted tags
            var tags = target.GetComponent<TagSystem>();
            if (tags != null)
            {
                var temp = new HashSet<GameplayTag>();
                CollectGrantedTags(effect, temp);
                foreach (var t in temp) tags.AddTag(t, 1);
            }


            effect.Apply(target, instigator, ae.StackCount);

            FireEffectExecuteCue(effect, instigator, target, effect, ae.StackCount, null);
            if (effect.cueWhileActive != null)
                cueManager?.AddCue(effect.cueWhileActive, BuildCueParams(instigator, instigator, target, effect, ae.StackCount, null));
        }

        // -------------------------
        // Spec Apply (Duration 완전 지원)
        // -------------------------
        public void ApplyEffectSpec(GameplayEffectSpec spec, GameObject target)
        {
            if (spec == null || spec.Effect == null || target == null) return;

            var effect = spec.Effect;
            var ctx = spec.Context;

            var inst = ctx != null ? ctx.Instigator : null;
            var causer = ctx != null ? ctx.Causer : inst;
            var srcObj = ctx != null ? ctx.SourceObject : null;

            if (srcObj == null) srcObj = effect;
            if (causer == null) causer = inst;

            // ✅ spec duration override를 먼저 고려한다.
            // (GE_Cooldown 같은 공통 GE가 duration=0(instant)로 저장돼 있더라도,
            //  spec.SetDuration(x)로 Duration 효과로 동작해야 한다.)
            float duration = spec.GetDurationOrDefault(effect.duration);

            // Instant ("최종 duration" 기준)
            if (duration <= 0f)
            {
                if (effect is ISpecGameplayEffect specEffect)
                    specEffect.Apply(spec, target);
                else
                    effect.Apply(target, inst, Mathf.Max(1, spec.StackCount));

                FireEffectExecuteCue(effect, inst, target, srcObj, Mathf.Max(1, spec.StackCount), ctx);
                return;
            }

            bool isCooldown = effect is GE_Cooldown;
            var existing = activeEffects.FirstOrDefault(e =>
                e.Effect == effect &&
                e.Target == target &&
                (!isCooldown || e.SourceObject == srcObj));


            if (existing != null)
            {
                int add = Mathf.Max(1, spec.StackCount);
                if (effect.canStack)
                    existing.StackCount = Mathf.Min(effect.maxStacks, existing.StackCount + add);

                existing.TimeRemaining = duration;
                existing.Instigator = inst;
                existing.Causer = causer;
                existing.SourceObject = srcObj;
                existing.Context = ctx;

                spec.StackCount = existing.StackCount;

                if (effect is ISpecGameplayEffect specEffect)
                    specEffect.Apply(spec, target);
                else
                    effect.Apply(target, inst, existing.StackCount);

                FireEffectExecuteCue(effect, inst, target, srcObj, existing.StackCount, ctx);
                if (effect.cueWhileActive != null)
                    cueManager?.AddCue(effect.cueWhileActive, BuildCueParams(inst, causer, target, srcObj, existing.StackCount, ctx));

                return;
            }

            var ae = new ActiveGameplayEffect(effect, target)
            {
                Instigator = inst,
                Causer = causer,
                SourceObject = srcObj,
                Context = ctx,
                TimeRemaining = duration,
                StackCount = Mathf.Max(1, spec.StackCount)
            };
            activeEffects.Add(ae);

            // ✅ CooldownTag 지원: GE_Cooldown은 SourceObject에 AbilityDefinition을 넣어 구분한다.
            // 하나의 GE_Cooldown을 쓰더라도, AbilityDefinition.cooldownTag가 있으면 해당 태그를 "쿨다운이 도는 동안" 부여한다.
            if (isCooldown && srcObj is AbilityDefinition ad && ad.cooldownTag != null)
            {
                var ts = target.GetComponent<TagSystem>();
                if (ts != null) ts.AddTag(ad.cooldownTag, 1);
            }

            // granted tags (1회) - direct + tagsets
            var tgs = target.GetComponent<TagSystem>();
            if (tgs != null)
            {
                var temp = new HashSet<GameplayTag>();
                CollectGrantedTags(effect, temp);
                foreach (var t in temp) tgs.AddTag(t, 1);
            }


            spec.StackCount = ae.StackCount;

            if (effect is ISpecGameplayEffect specEffectNew)
                specEffectNew.Apply(spec, target);
            else
                effect.Apply(target, inst, ae.StackCount);

            FireEffectExecuteCue(effect, inst, target, srcObj, ae.StackCount, ctx);
            if (effect.cueWhileActive != null)
                cueManager?.AddCue(effect.cueWhileActive, BuildCueParams(inst, causer, target, srcObj, ae.StackCount, ctx));
        }

        public void RemoveEffect(GameplayEffect effect, GameObject target)
        {
            var ae = activeEffects.FirstOrDefault(e => e.Effect == effect && e.Target == target);
            if (ae != null)
            {
                EndEffect(ae);
                activeEffects.Remove(ae);
            }
        }

        private void EndEffect(ActiveGameplayEffect ae)
        {
            var effect = ae.Effect;
            if (effect == null) return;

            // ✅ CooldownTag 회수 (ApplyEffectSpec에서 추가한 것과 페어)
            if (effect is GE_Cooldown && ae.SourceObject is AbilityDefinition ad && ad.cooldownTag != null)
            {
                var ts = ae.Target != null ? ae.Target.GetComponent<TagSystem>() : null;
                if (ts != null) ts.RemoveTag(ad.cooldownTag, 1);
            }

            var inst = ae.Instigator;
            var causer = ae.Causer != null ? ae.Causer : inst;
            var srcObj = ae.SourceObject != null ? ae.SourceObject : effect;
            var ctx = ae.Context;

            if (cueManager != null)
            {
                if (effect.cueWhileActive != null)
                    cueManager.RemoveCue(effect.cueWhileActive, BuildCueParams(inst, causer, ae.Target, srcObj, ae.StackCount, ctx));

                if (effect.cueOnRemove != null)
                    cueManager.ExecuteCue(effect.cueOnRemove, BuildCueParams(inst, causer, ae.Target, srcObj, ae.StackCount, ctx));
            }

            effect.Remove(ae.Target, inst);

            var tags = ae.Target.GetComponent<TagSystem>();
            if (tags != null)
            {
                var temp = new HashSet<GameplayTag>();
                CollectGrantedTags(effect, temp);
                foreach (var t in temp) tags.RemoveTag(t, 1);
            }

        }

        private void FireEffectExecuteCue(GameplayEffect effect, GameObject instigator, GameObject target, Object sourceObject, float magnitude, GameplayEffectContext ctx)
        {
            if (cueManager == null || effect == null) return;
            if (effect.cueOnExecute == null) return;

            cueManager.ExecuteCue(effect.cueOnExecute, BuildCueParams(instigator, ctx != null ? ctx.Causer : instigator, target, sourceObject, magnitude, ctx));
        }

        private GameplayCueParams BuildCueParams(GameObject instigator, GameObject causer, GameObject target, Object sourceObject, float magnitude, GameplayEffectContext ctx)
        {
            var p = new GameplayCueParams
            {
                Instigator = instigator,
                Causer = causer,
                Target = target,
                SourceObject = sourceObject,
                Magnitude = magnitude
            };

            if (ctx != null)
            {
                if (ctx.Hit3D.HasValue)
                {
                    var h = ctx.Hit3D.Value;
                    p.Position = h.point;
                    p.Normal = h.normal;
                    return p;
                }
                if (ctx.Hit2D.HasValue)
                {
                    var h2 = ctx.Hit2D.Value;
                    p.Position = h2.point;
                    p.Normal = h2.normal;
                    return p;
                }
            }

            p.Position = target != null ? target.transform.position : Vector3.zero;
            p.Normal = Vector3.up;
            return p;
        }

        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects => activeEffects;

        // 유물용: 특정 grantedTag가 붙은 Duration의 남은 시간 조작
        public int ReduceRemainingTimeByGrantedTag(GameObject target, GameplayTag tag, float reduceSeconds)
        {
            if (target == null || tag == null || reduceSeconds <= 0f) return 0;

            int affected = 0;

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var ae = activeEffects[i];
                if (ae.Target != target) continue;
                if (!EffectHasGrantedTag(ae.Effect, tag)) continue;


                ae.TimeRemaining -= reduceSeconds;
                affected++;

                if (ae.TimeRemaining <= 0f)
                {
                    EndEffect(ae);
                    activeEffects.RemoveAt(i);
                }
            }
            return affected;
        }

        public int MultiplyRemainingTimeByGrantedTag(GameObject target, GameplayTag tag, float multiplier)
        {
            if (target == null || tag == null) return 0;
            multiplier = Mathf.Clamp(multiplier, 0f, 10f);

            int affected = 0;

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var ae = activeEffects[i];
                if (ae.Target != target) continue;
                if (!EffectHasGrantedTag(ae.Effect, tag)) continue;


                ae.TimeRemaining *= multiplier;
                affected++;

                if (ae.TimeRemaining <= 0f)
                {
                    EndEffect(ae);
                    activeEffects.RemoveAt(i);
                }
            }
            return affected;
        }
        // GameplayEffectRunner.cs
        public int ReduceRemainingTimeBySourceObject(
            GameObject target,
            GameplayEffect effect,
            Object sourceObject,
            float reduceSeconds)
        {
            if (target == null || effect == null || sourceObject == null) return 0;
            if (reduceSeconds <= 0f) return 0;

            int affected = 0;

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var ae = activeEffects[i];
                if (ae.Target != target) continue;
                if (ae.Effect != effect) continue;
                if (ae.SourceObject != sourceObject) continue;

                ae.TimeRemaining -= reduceSeconds;
                affected++;

                if (ae.TimeRemaining <= 0f)
                {
                    EndEffect(ae);
                    activeEffects.RemoveAt(i);
                }
            }

            return affected;
        }

        public int MultiplyRemainingTimeBySourceObject(
            GameObject target,
            GameplayEffect effect,
            Object sourceObject,
            float multiplier)
        {
            if (target == null || effect == null || sourceObject == null) return 0;
            multiplier = Mathf.Clamp(multiplier, 0f, 10f);

            int affected = 0;

            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                var ae = activeEffects[i];
                if (ae.Target != target) continue;
                if (ae.Effect != effect) continue;
                if (ae.SourceObject != sourceObject) continue;

                ae.TimeRemaining *= multiplier;
                affected++;

                if (ae.TimeRemaining <= 0f)
                {
                    EndEffect(ae);
                    activeEffects.RemoveAt(i);
                }
            }

            return affected;
        }

        private static void CollectGrantedTags(GameplayEffect effect, HashSet<GameplayTag> outTags)
        {
            if (effect == null || outTags == null) return;

            if (effect.grantedTags != null)
                for (int i = 0; i < effect.grantedTags.Count; i++)
                    if (effect.grantedTags[i] != null) outTags.Add(effect.grantedTags[i]);

            if (effect.grantedTagSets != null)
            {
                var visited = new HashSet<GameplayTagSet>();
                for (int i = 0; i < effect.grantedTagSets.Count; i++)
                    effect.grantedTagSets[i]?.CollectTags(outTags, visited);
            }
        }

        private static bool EffectHasGrantedTag(GameplayEffect effect, GameplayTag tag)
        {
            if (effect == null || tag == null) return false;

            if (effect.grantedTags != null && effect.grantedTags.Contains(tag))
                return true;

            if (effect.grantedTagSets != null)
            {
                var visited = new HashSet<GameplayTagSet>();
                for (int i = 0; i < effect.grantedTagSets.Count; i++)
                {
                    var set = effect.grantedTagSets[i];
                    if (set != null && set.ContainsTag(tag, visited)) return true;
                }
            }
            return false;
        }

    }

    public class ActiveGameplayEffect
    {
        public GameplayEffect Effect { get; }
        public GameObject Target { get; }

        public GameObject Instigator { get; set; }
        public GameObject Causer { get; set; }
        public Object SourceObject { get; set; }
        public GameplayEffectContext Context { get; set; }

        public float TimeRemaining { get; set; }
        public int StackCount { get; set; }

        public ActiveGameplayEffect(GameplayEffect effect, GameObject target)
        {
            Effect = effect;
            Target = target;
        }
    }
}

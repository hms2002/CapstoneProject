using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// GameplayEffect의 런타임 적용을 조율하는 오케스트레이터.
    ///
    /// 책임:
    /// - GameplayEffect를 simple apply 또는 spec apply 경로로 적용한다.
    /// - Duration 효과의 런타임 인스턴스를 생성/갱신한다.
    /// - 활성 효과의 수명을 추적하고 종료 시점을 처리한다.
    /// - granted tag 부여/회수, cue 연출, 특수 처리(예: cooldown 태그 연동)를 조율한다.
    ///
    /// 적용 정책:
    /// - ApplyEffectSpec(...)은 런타임 문맥을 풍부하게 전달하는 정식 경로이다.
    ///   sourceObject, SetByCaller, duration override, hit context 등이 필요하면 이 경로를 사용한다.
    /// - ApplyEffect(...)는 단순/고정 효과를 위한 간편 경로이다.
    ///   레거시 코드나, 문맥 정보가 거의 필요 없는 월드 시스템에서 주로 사용한다.
    ///
    /// 중요한 점:
    /// - 이 Runner는 "활성 GameplayEffect의 수명"을 관리한다.
    /// - 효과 내부 payload가 별도의 수명 개념을 가질 수 있지만,
    ///   그것은 Runner가 관리하는 활성 효과 수명과 다른 개념일 수 있다.
    ///
    /// 운영 원칙:
    /// - Ability 시스템 내부의 공식 효과 적용 경로는 가능하면 spec 기반으로 유지한다.
    /// - 단순 self/target 고정 효과는 non-spec 경로를 허용한다.
    /// - 특수 규칙은 Runner 내부의 special handling 구역에 모아 관리한다.
    /// </summary>
    public class GameplayEffectRunner : MonoBehaviour
    {
        private readonly ActiveGameplayEffectRepository repository = new();

        [SerializeField] private GameplayCueManager cueManager;

        private GameplayEffectPresentationRouter presentationRouter;

        public IReadOnlyList<ActiveGameplayEffect> ActiveEffects => repository.ActiveEffects;

        private void Awake()
        {
#if UNITY_2023_1_OR_NEWER
            if (cueManager == null) cueManager = Object.FindAnyObjectByType<GameplayCueManager>();
#else
            if (cueManager == null) cueManager = FindObjectOfType<GameplayCueManager>();
#endif
            presentationRouter = new GameplayEffectPresentationRouter(cueManager);
        }

        private void Update()
        {
            repository.Tick(Time.deltaTime, EndEffect);
        }

        #region Queries

        public bool HasActiveEffect(GameplayEffect effect, GameObject target)
        {
            return repository.HasActive(effect, target);
        }

        public bool HasActiveEffect(GameplayEffect effect, GameObject target, Object sourceObject)
        {
            if (effect == null || target == null) return false;

            if (ShouldMatchSourceObject(effect, sourceObject))
                return repository.HasActive(effect, target, sourceObject);

            return repository.HasActive(effect, target);
        }

        public float GetRemainingTime(GameplayEffect effect, GameObject target)
        {
            return repository.GetRemainingTime(effect, target);
        }

        public float GetRemainingTime(GameplayEffect effect, GameObject target, Object sourceObject)
        {
            if (effect == null || target == null) return 0f;

            if (ShouldMatchSourceObject(effect, sourceObject))
                return repository.GetRemainingTime(effect, target, sourceObject);

            return repository.GetRemainingTime(effect, target);
        }

        #endregion

        #region Apply - Non Spec
        /// <summary>
        /// 단순 / 레거시 적용 경로.
        ///
        /// 용도:
        /// - 고정된 효과를 간단히 적용할 때 사용한다.
        /// - 런타임 문맥 정보가 거의 필요 없는 경우에 적합하다.
        ///
        /// 한계:
        /// - SetByCaller 값을 전달할 수 없다.
        /// - sourceObject를 별도로 지정할 수 없다. (기본적으로 effect 자신이 source처럼 동작)
        /// - duration override를 줄 수 없다.
        /// - hit context를 전달할 수 없다.
        ///
        /// 권장 사용처:
        /// - 단순 월드 시스템
        /// - 고정 self/target 효과
        /// - 기존 레거시 호출부
        ///
        /// 주의:
        /// - 새 시스템, 특히 Ability 기반 효과 적용은 가능하면 ApplyEffectSpec(...)을 우선 사용한다.
        /// </summary>
        public void ApplyEffect(GameplayEffect effect, GameObject target, GameObject instigator)
        {
            if (effect == null || target == null) return;

            var existing = FindExistingActiveEffect(effect, target, effect);

            if (existing != null)
            {
                RefreshActiveEffectFromNonSpec(existing, effect, instigator);
                ApplyEffectPayloadNonSpec(effect, target, instigator, existing.StackCount);
                PlayStartPresentation(effect, instigator, instigator, target, effect, existing.StackCount, null);
                return;
            }

            if (effect.IsInstant)
            {
                ApplyEffectPayloadNonSpec(effect, target, instigator, 1);
                PlayExecutePresentation(effect, instigator, instigator, target, effect, 1f, null);
                return;
            }

            var ae = CreateDurationEffectNonSpec(effect, target, instigator);
            repository.Add(ae);

            ApplySpecialStartHandling(ae);
            ApplyGrantedTags(effect, target);

            ApplyEffectPayloadNonSpec(effect, target, instigator, ae.StackCount);
            PlayStartPresentation(effect, instigator, instigator, target, effect, ae.StackCount, null);
        }

        #endregion

        #region Apply - Spec
        /// <summary>
        /// 런타임 문맥을 포함한 정식 적용 경로.
        ///
        /// 지원 기능:
        /// - sourceObject
        /// - instigator / causer 분리
        /// - SetByCaller 값 전달
        /// - duration override
        /// - hit context
        /// - spec-aware payload 적용
        ///
        /// 권장 사용처:
        /// - Ability 시스템에서 발생하는 효과
        /// - 동적 수치가 필요한 효과
        /// - 어느 시스템/능력에서 나온 효과인지 추적이 필요한 경우
        ///
        /// 운영 원칙:
        /// - 새로 만드는 시스템은 가능하면 이 경로를 기본으로 사용한다.
        /// - AbilitySystem 내부 공식 효과 적용 경로도 이 메서드를 사용한다.
        /// </summary>
        public void ApplyEffectSpec(GameplayEffectSpec spec, GameObject target)
        {
            if (spec == null || spec.Effect == null || target == null) return;

            var effect = spec.Effect;
            var ctx = spec.Context;

            ResolveSpecActors(effect, ctx, out var inst, out var causer, out var srcObj);

            float duration = GetFinalDuration(spec, effect);

            if (duration <= 0f)
            {
                ApplyInstantSpec(effect, spec, target, inst, causer, srcObj, ctx);
                return;
            }

            var existing = FindExistingActiveEffect(effect, target, srcObj);

            if (existing != null)
            {
                RefreshActiveEffectFromSpec(existing, effect, spec, duration, inst, causer, srcObj, ctx);
                ApplyEffectPayloadSpec(effect, spec, target, inst, existing.StackCount);
                PlayStartPresentation(effect, inst, causer, target, srcObj, existing.StackCount, ctx);
                return;
            }

            var ae = CreateDurationEffectSpec(effect, spec, target, duration, inst, causer, srcObj, ctx);
            repository.Add(ae);

            ApplySpecialStartHandling(ae);
            ApplyGrantedTags(effect, target);

            spec.StackCount = ae.StackCount;

            ApplyEffectPayloadSpec(effect, spec, target, inst, ae.StackCount);
            PlayStartPresentation(effect, inst, causer, target, srcObj, ae.StackCount, ctx);
        }

        private void ApplyInstantSpec(
            GameplayEffect effect,
            GameplayEffectSpec spec,
            GameObject target,
            GameObject inst,
            GameObject causer,
            Object srcObj,
            GameplayEffectContext ctx)
        {
            int stackCount = Mathf.Max(1, spec.StackCount);

            ApplyEffectPayloadSpec(effect, spec, target, inst, stackCount);
            PlayExecutePresentation(effect, inst, causer, target, srcObj, stackCount, ctx);
        }

        #endregion

        #region Remove / End

        public void RemoveEffect(GameplayEffect effect, GameObject target)
        {
            var ae = repository.FindFirst(effect, target);
            if (ae == null) return;

            EndEffect(ae);
            repository.Remove(ae);
        }

        private void EndEffect(ActiveGameplayEffect ae)
        {
            var effect = ae.Effect;
            if (effect == null) return;

            ApplySpecialEndHandling(ae);
            PlayEndPresentation(ae);

            effect.Remove(ae.Target, ae.Instigator);
            RemoveGrantedTags(effect, ae.Target);
        }

        #endregion

        #region Time Adjustment

        public int ReduceRemainingTimeByGrantedTag(GameObject target, GameplayTag tag, float reduceSeconds)
        {
            if (target == null || tag == null || reduceSeconds <= 0f) return 0;

            return repository.AdjustRemainingTime(
                ae => ae.Target == target && EffectHasGrantedTag(ae.Effect, tag),
                time => time - reduceSeconds,
                EndEffect);
        }

        public int MultiplyRemainingTimeByGrantedTag(GameObject target, GameplayTag tag, float multiplier)
        {
            if (target == null || tag == null) return 0;
            multiplier = Mathf.Clamp(multiplier, 0f, 10f);

            return repository.AdjustRemainingTime(
                ae => ae.Target == target && EffectHasGrantedTag(ae.Effect, tag),
                time => time * multiplier,
                EndEffect);
        }

        public int ReduceRemainingTimeBySourceObject(
            GameObject target,
            GameplayEffect effect,
            Object sourceObject,
            float reduceSeconds)
        {
            if (target == null || effect == null || sourceObject == null || reduceSeconds <= 0f) return 0;

            return repository.AdjustRemainingTime(
                ae => ae.Target == target && ae.Effect == effect && ae.SourceObject == sourceObject,
                time => time - reduceSeconds,
                EndEffect);
        }

        public int MultiplyRemainingTimeBySourceObject(
            GameObject target,
            GameplayEffect effect,
            Object sourceObject,
            float multiplier)
        {
            if (target == null || effect == null || sourceObject == null) return 0;
            multiplier = Mathf.Clamp(multiplier, 0f, 10f);

            return repository.AdjustRemainingTime(
                ae => ae.Target == target && ae.Effect == effect && ae.SourceObject == sourceObject,
                time => time * multiplier,
                EndEffect);
        }

        #endregion

        #region ActiveEffect Creation / Refresh

        private ActiveGameplayEffect FindExistingActiveEffect(GameplayEffect effect, GameObject target, Object sourceObject)
        {
            bool matchSource = ShouldMatchSourceObject(effect, sourceObject);

            return matchSource
                ? repository.FindFirst(effect, target, sourceObject)
                : repository.FindFirst(effect, target);
        }

        private ActiveGameplayEffect CreateDurationEffectNonSpec(GameplayEffect effect, GameObject target, GameObject instigator)
        {
            return new ActiveGameplayEffect(effect, target)
            {
                Instigator = instigator,
                Causer = instigator,
                SourceObject = effect,
                Context = null,
                TimeRemaining = effect.duration,
                StackCount = 1
            };
        }

        private ActiveGameplayEffect CreateDurationEffectSpec(
            GameplayEffect effect,
            GameplayEffectSpec spec,
            GameObject target,
            float duration,
            GameObject inst,
            GameObject causer,
            Object srcObj,
            GameplayEffectContext ctx)
        {
            return new ActiveGameplayEffect(effect, target)
            {
                Instigator = inst,
                Causer = causer,
                SourceObject = srcObj,
                Context = ctx,
                TimeRemaining = duration,
                StackCount = Mathf.Max(1, spec.StackCount)
            };
        }

        private void RefreshActiveEffectFromNonSpec(
            ActiveGameplayEffect existing,
            GameplayEffect effect,
            GameObject instigator)
        {
            if (effect.canStack && existing.StackCount < effect.maxStacks)
                existing.StackCount++;

            existing.TimeRemaining = effect.duration;
            existing.Instigator = instigator;
            existing.Causer = instigator;
            existing.SourceObject = effect;
            existing.Context = null;
        }

        private void RefreshActiveEffectFromSpec(
            ActiveGameplayEffect existing,
            GameplayEffect effect,
            GameplayEffectSpec spec,
            float duration,
            GameObject inst,
            GameObject causer,
            Object srcObj,
            GameplayEffectContext ctx)
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
        }

        #endregion

        #region Payload Apply

        private void ApplyEffectPayloadNonSpec(
            GameplayEffect effect,
            GameObject target,
            GameObject instigator,
            int stackCount)
        {
            effect.Apply(target, instigator, stackCount);
        }

        private void ApplyEffectPayloadSpec(
            GameplayEffect effect,
            GameplayEffectSpec spec,
            GameObject target,
            GameObject inst,
            int stackCount)
        {
            if (effect is ISpecGameplayEffect specEffect)
            {
                spec.StackCount = stackCount;
                specEffect.Apply(spec, target);
            }
            else
            {
                effect.Apply(target, inst, stackCount);
            }
        }

        #endregion

        #region Tag Handling

        private void ApplyGrantedTags(GameplayEffect effect, GameObject target)
        {
            var tags = target != null ? target.GetComponent<TagSystem>() : null;
            if (tags == null) return;

            var temp = new HashSet<GameplayTag>();
            CollectGrantedTags(effect, temp);

            foreach (var t in temp)
                tags.AddTag(t, 1);
        }

        private void RemoveGrantedTags(GameplayEffect effect, GameObject target)
        {
            var tags = target != null ? target.GetComponent<TagSystem>() : null;
            if (tags == null) return;

            var temp = new HashSet<GameplayTag>();
            CollectGrantedTags(effect, temp);

            foreach (var t in temp)
                tags.RemoveTag(t, 1);
        }

        #endregion

        #region Special Handling
        /// <summary>
        /// 효과 시작 시 필요한 특수 규칙을 한 곳에 모아 처리한다.
        ///
        /// 목적:
        /// - Runner 본문에 예외 규칙이 흩어지는 것을 막는다.
        /// - 현재는 cooldown 태그 연동만 처리하지만,
        ///   앞으로 특정 효과 타입에 대한 예외 규칙이 늘어나면 이 구역에 추가한다.
        /// </summary>
        private void ApplySpecialStartHandling(ActiveGameplayEffect ae)
        {
            ApplyCooldownStartTag(ae);
        }
        /// <summary>
        /// 효과 종료 시 필요한 특수 규칙을 한 곳에 모아 처리한다.
        ///
        /// 목적:
        /// - Runner 본문에 예외 규칙이 흩어지는 것을 막는다.
        /// - 현재는 cooldown 태그 회수만 처리하지만,
        ///   앞으로 특정 효과 타입에 대한 종료 규칙이 늘어나면 이 구역에 추가한다.
        /// </summary>
        private void ApplySpecialEndHandling(ActiveGameplayEffect ae)
        {
            RemoveCooldownEndTag(ae);
        }

        private void ApplyCooldownStartTag(ActiveGameplayEffect ae)
        {
            if (!IsCooldownEffect(ae.Effect)) return;
            if (ae.SourceObject is not AbilityDefinition ad) return;
            if (ad.cooldownTag == null) return;

            var ts = ae.Target != null ? ae.Target.GetComponent<TagSystem>() : null;
            if (ts != null)
                ts.AddTag(ad.cooldownTag, 1);
        }

        private void RemoveCooldownEndTag(ActiveGameplayEffect ae)
        {
            if (!IsCooldownEffect(ae.Effect)) return;
            if (ae.SourceObject is not AbilityDefinition ad) return;
            if (ad.cooldownTag == null) return;

            var ts = ae.Target != null ? ae.Target.GetComponent<TagSystem>() : null;
            if (ts != null)
                ts.RemoveTag(ad.cooldownTag, 1);
        }

        #endregion

        #region Presentation

        private void PlayStartPresentation(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            GameObject target,
            Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
        {
            PlayExecutePresentation(effect, instigator, causer, target, sourceObject, magnitude, ctx);
            presentationRouter?.AddWhileActive(effect, instigator, causer, target, sourceObject, magnitude, ctx);
        }

        private void PlayExecutePresentation(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            GameObject target,
            Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
        {
            presentationRouter?.PlayExecute(effect, instigator, causer, target, sourceObject, magnitude, ctx);
        }

        private void PlayEndPresentation(ActiveGameplayEffect ae)
        {
            var effect = ae.Effect;
            if (effect == null) return;

            var inst = ae.Instigator;
            var causer = ae.Causer != null ? ae.Causer : inst;
            var srcObj = ae.SourceObject != null ? ae.SourceObject : effect;
            var ctx = ae.Context;

            presentationRouter?.RemoveWhileActive(effect, inst, causer, ae.Target, srcObj, ae.StackCount, ctx);
            presentationRouter?.PlayRemove(effect, inst, causer, ae.Target, srcObj, ae.StackCount, ctx);
        }

        #endregion

        #region Context / Utility

        private void ResolveSpecActors(
            GameplayEffect effect,
            GameplayEffectContext ctx,
            out GameObject inst,
            out GameObject causer,
            out Object srcObj)
        {
            inst = ctx != null ? ctx.Instigator : null;
            causer = ctx != null ? ctx.Causer : inst;
            srcObj = ctx != null ? ctx.SourceObject : null;

            if (srcObj == null) srcObj = effect;
            if (causer == null) causer = inst;
        }

        private float GetFinalDuration(GameplayEffectSpec spec, GameplayEffect effect)
        {
            return spec.GetDurationOrDefault(effect.duration);
        }

        private static bool IsCooldownEffect(GameplayEffect effect)
        {
            return effect is GE_Cooldown;
        }
        /// <summary>
        /// 일부 효과는 같은 GameplayEffect 자산을 공유하더라도,
        /// 런타임에서는 sourceObject까지 포함해서 별개의 인스턴스로 취급해야 한다.
        ///
        /// 대표 예:
        /// - GE_Cooldown
        ///   하나의 공통 쿨다운 GE를 여러 능력이 함께 사용할 수 있지만,
        ///   실제 런타임에서는 "어떤 AbilityDefinition의 쿨다운인가"를 구분해야 한다.
        ///
        /// 이런 경우 sourceObject가 활성 효과의 런타임 식별자 일부가 된다.
        /// </summary>
        private static bool ShouldMatchSourceObject(GameplayEffect effect, Object sourceObject)
        {
            return IsCooldownEffect(effect) && sourceObject != null;
        }

        private static void CollectGrantedTags(GameplayEffect effect, HashSet<GameplayTag> outTags)
        {
            if (effect == null || outTags == null) return;

            if (effect.grantedTags != null)
            {
                for (int i = 0; i < effect.grantedTags.Count; i++)
                {
                    if (effect.grantedTags[i] != null)
                        outTags.Add(effect.grantedTags[i]);
                }
            }

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
                    if (set != null && set.ContainsTag(tag, visited))
                        return true;
                }
            }

            return false;
        }

        #endregion
    }
    /// <summary>
    /// 활성화된 GameplayEffect의 런타임 인스턴스 정보.
    ///
    /// 의미:
    /// - GameplayEffect 정의 자산 자체가 아니라,
    ///   "누구에게, 어떤 문맥으로, 얼마나 남았는지"를 들고 있는 런타임 상태 객체.
    ///
    /// 포함 정보:
    /// - 어떤 effect인가
    /// - 누구(target)에게 적용됐는가
    /// - 누가 instigator / causer 인가
    /// - sourceObject는 무엇인가
    /// - context는 무엇인가
    /// - 남은 시간과 스택 수는 얼마인가
    ///
    /// 주의:
    /// - 이 객체의 lifetime은 GameplayEffectRunner / Repository가 관리한다.
    /// </summary>
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
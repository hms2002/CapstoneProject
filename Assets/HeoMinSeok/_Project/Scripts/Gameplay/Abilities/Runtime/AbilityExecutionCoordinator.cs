using System;
using System.Collections;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - Ability 1회의 실행 생명주기를 조율한다.
    /// - Exclusive 실행과 Parallel 실행 모두 같은 종료 규칙으로 정리한다.
    /// </summary>
    public sealed class AbilityExecutionCoordinator
    {
        public IEnumerator RunExclusive(AbilitySystem system, AbilitySpec spec, GameObject target)
        {
            return RunInternal(system, spec, target, isParallel: false);
        }

        public IEnumerator RunParallel(AbilitySystem system, AbilitySpec spec, GameObject target)
        {
            return RunInternal(system, spec, target, isParallel: true);
        }

        private IEnumerator RunInternal(
            AbilitySystem system,
            AbilitySpec spec,
            GameObject target,
            bool isParallel)
        {
            if (system == null || spec == null || spec.Definition == null)
                yield break;

            BeginExecution(system, spec, target, isParallel);

            bool cancelled = false;
            Action<GameplayTag, AbilityEventData> onEvent = null;

            try
            {
                var def = spec.Definition;
                if (def.logic == null)
                {
                    Debug.LogError($"[GAS] Ability '{def.name}' has no Logic.");
                    yield break;
                }

                onEvent = CreateExecutionEventHandler(system, spec, target, isParallel);
                system.SubscribeGameplayEvent(onEvent);

                yield return RunAbilityLogicAndRecovery(system, spec, target);
            }
            finally
            {
                cancelled = spec.Token != null && spec.Token.IsCancelled;

                if (onEvent != null)
                    system.UnsubscribeGameplayEvent(onEvent);

                EndExecution(system, spec, target, cancelled, isParallel);
            }
        }

        private void BeginExecution(
            AbilitySystem system,
            AbilitySpec spec,
            GameObject target,
            bool isParallel)
        {
            if (isParallel)
                system.BeginParallelExecution(spec, target);
            else
                system.SetExecutionState(true, spec, target);

            spec.Token = new AbilityCancellationToken();

            var def = spec.Definition;

            if (system.TagSystem != null && def.grantedTagsWhileActive != null)
                system.TagSystem.AddTags(def.grantedTagsWhileActive);

            system.PresentationRouter?.PlayExecutionStart(def, spec, target);
            system.ApplyEffectContainers(spec, target, AbilityEffectTiming.OnActivate, null);
        }

        private IEnumerator RunAbilityLogicAndRecovery(
            AbilitySystem system,
            AbilitySpec spec,
            GameObject target)
        {
            var def = spec.Definition;

            yield return def.logic.Activate(system, spec, target);

            float recovery = def.recoveryTime;
            if (spec.TryGetFloat("RecoveryOverride", out var overrideRecovery))
                recovery = overrideRecovery;

            if (recovery <= 0f)
                yield break;

            float end = Time.time + recovery;
            while (Time.time < end)
            {
                if (spec.Token != null && spec.Token.IsCancelled)
                    yield break;

                yield return null;
            }
        }

        private void EndExecution(
            AbilitySystem system,
            AbilitySpec spec,
            GameObject target,
            bool cancelled,
            bool isParallel)
        {
            var def = spec.Definition;

            system.CancelGameplayEventWaiters(spec);
            system.PresentationRouter?.PlayExecutionEnd(def, spec, target, cancelled);

            system.ApplyEffectContainers(spec, target, AbilityEffectTiming.OnEnd, null);

            if (system.TagSystem != null && def.grantedTagsWhileActive != null)
                system.TagSystem.RemoveTags(def.grantedTagsWhileActive);

            if (def != null && def.startCooldownOnEnd)
                system.CooldownController?.StartCooldown(spec);

            spec.Token?.Cancel();
            spec.Token = null;

            if (isParallel)
            {
                system.EndParallelExecution(spec);
            }
            else
            {
                system.SetExecutionState(false, null, null);
                system.ClearActiveExecutionCoroutine();
                system.TryConsumeBufferedActivation_Internal();
            }
        }

        private Action<GameplayTag, AbilityEventData> CreateExecutionEventHandler(
            AbilitySystem system,
            AbilitySpec spec,
            GameObject target,
            bool isParallel)
        {
            return (tag, data) =>
            {
                if (isParallel)
                {
                    if (!system.IsParallelExecuting(spec))
                        return;
                }
                else
                {
                    if (!system.IsExecuting)
                        return;

                    if (system.CurrentExecSpec != spec)
                        return;
                }

                if (data.Spec != null && data.Spec != spec)
                    return;

                system.ApplyEffectContainers(spec, target, AbilityEffectTiming.OnEvent, tag);
            };
        }
    }
}
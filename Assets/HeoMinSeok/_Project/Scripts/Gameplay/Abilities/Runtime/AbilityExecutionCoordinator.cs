using System;
using System.Collections;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Ability 1회의 실행 생명주기를 조율한다.
    /// - 실행 시작 상태 진입
    /// - OnActivate 처리
    /// - GameplayEvent 구독
    /// - AbilityLogic + Recovery 실행
    /// - 종료/취소 정리
    /// </summary>
    public sealed class AbilityExecutionCoordinator
    {
        public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject target)
        {
            if (system == null || spec == null || spec.Definition == null)
                yield break;

            BeginExecution(system, spec, target);

            bool cancelled = false;
            Action<GameplayTag, AbilityEventData> onEvent = null;

            try
            {
                var def = spec.Definition;
                if (def.logic == null)
                {
                    Debug.LogError($"[GAS] Ability '{def.name}' has no Logic. (Legacy pipeline removed)");
                    yield break;
                }

                onEvent = CreateExecutionEventHandler(system, spec, target);
                system.SubscribeGameplayEvent(onEvent);

                yield return RunAbilityLogicAndRecovery(system, spec, target);
            }
            finally
            {
                cancelled = spec.Token != null && spec.Token.IsCancelled;

                if (onEvent != null)
                    system.UnsubscribeGameplayEvent(onEvent);

                EndExecution(system, spec, target, cancelled);
            }
        }

        private void BeginExecution(AbilitySystem system, AbilitySpec spec, GameObject target)
        {
            system.SetExecutionState(true, spec, target);
            spec.Token = new AbilityCancellationToken();

            var def = spec.Definition;

            if (system.TagSystem != null && def.grantedTagsWhileActive != null)
                system.TagSystem.AddTags(def.grantedTagsWhileActive);

            system.PresentationRouter?.PlayExecutionStart(def, spec, target);
            system.ApplyEffectContainers(spec, target, AbilityEffectTiming.OnActivate, null);
        }

        private IEnumerator RunAbilityLogicAndRecovery(AbilitySystem system, AbilitySpec spec, GameObject target)
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

        private void EndExecution(AbilitySystem system, AbilitySpec spec, GameObject target, bool cancelled)
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

            system.SetExecutionState(false, null, null);
            system.ClearActiveExecutionCoroutine();
            system.TryConsumeBufferedActivation_Internal();
        }

        private Action<GameplayTag, AbilityEventData> CreateExecutionEventHandler(
            AbilitySystem system,
            AbilitySpec spec,
            GameObject target)
        {
            return (tag, data) =>
            {
                if (!system.IsExecuting)
                    return;

                if (system.CurrentExecSpec != spec)
                    return;

                if (data.Spec != null && data.Spec != spec)
                    return;

                system.ApplyEffectContainers(spec, target, AbilityEffectTiming.OnEvent, tag);
            };
        }
    }
}
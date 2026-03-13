using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// AbilitySystem 전용 GameplayEvent 채널.
    /// - 이벤트 발행
    /// - waiter 등록/해제
    /// - owner spec 기준 waiter 정리
    /// </summary>
    public sealed class AbilityGameplayEventChannel
    {
        private readonly AbilitySystem owner;
        private readonly GameplayCueManager cueManager;
        private readonly bool autoExecuteCueWhenGameplayEventTagExists;

        private readonly Dictionary<AbilitySpec, List<GameplayEventWaiter>> waitersBySpec = new();

        public AbilityGameplayEventChannel(
            AbilitySystem owner,
            GameplayCueManager cueManager,
            bool autoExecuteCueWhenGameplayEventTagExists)
        {
            this.owner = owner;
            this.cueManager = cueManager;
            this.autoExecuteCueWhenGameplayEventTagExists = autoExecuteCueWhenGameplayEventTagExists;
        }

        public event System.Action<GameplayTag, AbilityEventData> GameplayEventRaised;

        public void Send(GameplayTag tag, AbilityEventData data = default)
        {
            Raise(tag, data);
        }

        public void Raise(GameplayTag tag, AbilityEventData data)
        {
            if (tag == null)
                return;

            GameplayEventRaised?.Invoke(tag, data);

            if (autoExecuteCueWhenGameplayEventTagExists &&
                cueManager != null &&
                cueManager.HasCue(tag))
            {
                cueManager.ExecuteCue(tag, BuildCueParamsFromEvent(data));
            }
        }

        public GameplayEventWaiter Wait(GameplayTag tag, AbilitySpec ownerSpec)
        {
            if (tag == null)
                return null;

            var waiter = new GameplayEventWaiter();

            if (ownerSpec != null)
            {
                if (!waitersBySpec.TryGetValue(ownerSpec, out var list))
                {
                    list = new List<GameplayEventWaiter>();
                    waitersBySpec.Add(ownerSpec, list);
                }

                list.Add(waiter);
            }

            void Handler(GameplayTag raisedTag, AbilityEventData raisedData)
            {
                if (raisedTag != tag || waiter.Done)
                    return;

                waiter.Data = raisedData;
                waiter.Done = true;

                GameplayEventRaised -= Handler;
                waiter.Cleanup = null;

                if (ownerSpec != null && waitersBySpec.TryGetValue(ownerSpec, out var list))
                    list.Remove(waiter);
            }

            waiter.Cleanup = () =>
            {
                GameplayEventRaised -= Handler;

                if (ownerSpec != null && waitersBySpec.TryGetValue(ownerSpec, out var list))
                    list.Remove(waiter);
            };

            GameplayEventRaised += Handler;
            return waiter;
        }

        public void CancelWaiters(AbilitySpec spec)
        {
            if (spec == null)
                return;

            if (!waitersBySpec.TryGetValue(spec, out var list) || list.Count == 0)
                return;

            var copy = list.ToArray();
            list.Clear();

            foreach (var waiter in copy)
                waiter?.Cancel();
        }

        public void CancelAllWaiters()
        {
            foreach (var kv in waitersBySpec)
            {
                var list = kv.Value;
                if (list == null)
                    continue;

                for (int i = 0; i < list.Count; i++)
                    list[i]?.Cancel();
            }

            waitersBySpec.Clear();
        }

        private GameplayCueParams BuildCueParamsFromEvent(AbilityEventData data)
        {
            GameObject ownerObject = owner != null ? owner.gameObject : null;
            Transform ownerTransform = ownerObject != null ? ownerObject.transform : null;

            return new GameplayCueParams
            {
                Instigator = data.Instigator != null ? data.Instigator : ownerObject,
                Causer = data.Instigator != null ? data.Instigator : ownerObject,
                Target = data.Target,
                Position = data.WorldPosition != Vector3.zero
                    ? data.WorldPosition
                    : (data.Target != null
                        ? data.Target.transform.position
                        : (ownerTransform != null ? ownerTransform.position : Vector3.zero)),
                Normal = Vector3.up,
                SourceObject = data.Spec != null ? data.Spec.Definition : null,
                Magnitude = 1f
            };
        }
    }
}
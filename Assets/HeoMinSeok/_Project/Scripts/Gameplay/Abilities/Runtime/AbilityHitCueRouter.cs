
using System.Collections;
using System.Collections.Generic;
using CapstonePresentation;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 : 적중 확정 이벤트를 감지해 히트 전용 Gameplay Cue를 큐에 적재하고, 짧은 간격으로 분산 배출한다.
    /// 한 프레임에 과도한 히트 파티클이 동시에 생성되지 않도록 하되, 첫 타격감은 최대한 유지한다.
    /// </summary>
    public sealed class AbilityHitCueRouter
    {
        private const string DefaultHitConfirmTagResourcePath = "Tags/Event.HitConfirm";
        private const string DefaultHitImpactCueResourcePath = "Tags/Cue.Ability.Sword.Hit";
        private const int MaxQueuedHitCues = 24;
        private const int MaxHitCuesPerDrainStep = 2;
        private const float HitCueDrainIntervalSeconds = 0.02f;

        private readonly AbilitySystem owner;
        private readonly GameplayCueManager cueManager;
        private readonly AbilityGameplayEventChannel eventChannel;
        private readonly GameplayTag hitConfirmRootTag;
        private readonly GameplayTag defaultHitImpactCueTag;
        private readonly Queue<QueuedHitCueRequest> pendingHitCues = new();
        private Coroutine drainCoroutine;

        /// <summary>
        /// 책임 : 히트 큐 배출에 필요한 정의와 파라미터를 큐에 담아 순차 실행할 수 있게 보관한다.
        /// </summary>
        private struct QueuedHitCueRequest
        {
            public AbilityDefinition Definition;
            public GameplayCueParams CueParams;
            public HitImpactCueKind HitImpactCueKind;
        }

        public AbilityHitCueRouter(
            AbilitySystem owner,
            GameplayCueManager cueManager,
            AbilityGameplayEventChannel eventChannel,
            GameplayTag hitConfirmRootTag = null)
        {
            this.owner = owner;
            this.cueManager = cueManager;
            this.eventChannel = eventChannel;
            this.hitConfirmRootTag = hitConfirmRootTag != null
                ? hitConfirmRootTag
                : Resources.Load<GameplayTag>(DefaultHitConfirmTagResourcePath);
            defaultHitImpactCueTag = Resources.Load<GameplayTag>(DefaultHitImpactCueResourcePath);

            if (this.eventChannel != null)
                this.eventChannel.GameplayEventRaised += HandleGameplayEvent;
        }

        public void Dispose()
        {
            if (eventChannel != null)
                eventChannel.GameplayEventRaised -= HandleGameplayEvent;

            if (owner != null && drainCoroutine != null)
            {
                owner.StopCoroutine(drainCoroutine);
                drainCoroutine = null;
            }

            pendingHitCues.Clear();
        }

        private void HandleGameplayEvent(GameplayTag raisedTag, AbilityEventData data)
        {
            if (hitConfirmRootTag == null)
                return;

            if (!MatchesRequestedTag(raisedTag, hitConfirmRootTag))
                return;

            AbilityDefinition definition = data.Spec != null ? data.Spec.Definition : null;
            if (definition == null)
                return;

            GameplayCueParams cueParams = BuildCueParams(data, definition);
            EnqueueHitCues(definition, cueParams, data.HitImpactCueKind);
        }

        private GameplayCueParams BuildCueParams(AbilityEventData data, AbilityDefinition definition)
        {
            GameObject ownerObject = owner != null ? owner.gameObject : null;
            GameObject causerObject = ResolveGameObject(data.Causer);
            GameObject targetObject = data.Target;
            bool hasExplicitPosition = data.WorldPosition != Vector3.zero;
            Vector3 position = hasExplicitPosition ? data.WorldPosition : Vector3.zero;

            float baseMagnitude = Mathf.Max(0f, definition.hitCueMagnitude);
            float finalMagnitude = data.IsCriticalHit
                ? baseMagnitude * Mathf.Max(0f, definition.criticalHitCueMagnitudeMultiplier)
                : baseMagnitude;

            return new GameplayCueParams
            {
                Instigator = data.Instigator != null ? data.Instigator : ownerObject,
                Causer = causerObject != null ? causerObject : (data.Instigator != null ? data.Instigator : ownerObject),
                Target = targetObject,
                Position = position,
                HasExplicitPosition = hasExplicitPosition,
                Normal = Vector3.up,
                SourceObject = definition,
                Magnitude = finalMagnitude
            };
        }

        private static GameObject ResolveGameObject(object value)
        {
            switch (value)
            {
                case GameObject gameObject:
                    return gameObject;
                case Component component:
                    return component.gameObject;
                default:
                    return null;
            }
        }

        private static bool MatchesRequestedTag(GameplayTag raisedTag, GameplayTag requestedTag)
        {
            if (raisedTag == null || requestedTag == null)
                return false;

            for (GameplayTag current = raisedTag; current != null; current = current.Parent)
            {
                if (current == requestedTag)
                    return true;
            }

            return false;
        }

        private void EnqueueHitCues(AbilityDefinition definition, GameplayCueParams cueParams, HitImpactCueKind hitImpactCueKind)
        {
            if (definition == null || owner == null)
                return;

            if (pendingHitCues.Count >= MaxQueuedHitCues)
            {
                pendingHitCues.Dequeue();
            }

            pendingHitCues.Enqueue(new QueuedHitCueRequest
            {
                Definition = definition,
                CueParams = cueParams,
                HitImpactCueKind = hitImpactCueKind
            });

            if (drainCoroutine == null)
                drainCoroutine = owner.StartCoroutine(DrainQueuedHitCues());
        }

        private IEnumerator DrainQueuedHitCues()
        {
            while (pendingHitCues.Count > 0)
            {
                int remainingBudget = MaxHitCuesPerDrainStep;
                while (remainingBudget > 0 && pendingHitCues.Count > 0)
                {
                    QueuedHitCueRequest request = pendingHitCues.Dequeue();
                    ExecuteHitCuesImmediate(request.Definition, request.CueParams, request.HitImpactCueKind);
                    remainingBudget--;
                }

                if (pendingHitCues.Count > 0)
                    yield return new WaitForSeconds(HitCueDrainIntervalSeconds);
            }

            drainCoroutine = null;
        }

        private void ExecuteHitCuesImmediate(AbilityDefinition definition, GameplayCueParams cueParams, HitImpactCueKind hitImpactCueKind)
        {
            if (definition == null)
                return;

            GameplayPresentationPhase hitConfirmedPhase = definition.GetHitConfirmedPhase();
            if (cueManager != null)
            {
                GameplayTag automaticHitImpactCueTag = ResolveHitImpactCueTag(hitImpactCueKind);
                bool hasAutomaticHitImpactCue = false;
                foreach (GameplayTag tag in hitConfirmedPhase.EnumerateCues())
                {
                    if (tag == null)
                        continue;

                    if (IsSameExplicitTag(tag, automaticHitImpactCueTag))
                        hasAutomaticHitImpactCue = true;

                    cueManager.ExecuteCue(tag, cueParams);
                }

                if (!hasAutomaticHitImpactCue && automaticHitImpactCueTag != null)
                    cueManager.ExecuteCue(automaticHitImpactCueTag, cueParams);
            }

            Vector3 normal = cueParams.Normal.sqrMagnitude > 0.0001f ? cueParams.Normal : Vector3.up;
            WorldPresentationRuntime.PlayMerged(
                hitConfirmedPhase.Presentation,
                default,
                hitConfirmedPhase.CameraShake,
                WorldPresentationContext.AtWorld(
                    instigator: cueParams.Instigator,
                    position: cueParams.Position,
                    fallbackDirection: normal,
                    target: cueParams.Target,
                    sourceObject: cueParams.SourceObject,
                    rotation: Quaternion.LookRotation(Vector3.forward, normal),
                    causer: cueParams.Causer));
        }

        private static bool IsSameExplicitTag(GameplayTag tag, GameplayTag expected)
        {
            if (tag == null || expected == null)
                return false;

            if (tag == expected)
                return true;

            return tag.Id != 0 && tag.Id == expected.Id;
        }

        private GameplayTag ResolveHitImpactCueTag(HitImpactCueKind hitImpactCueKind)
        {
            switch (hitImpactCueKind)
            {
                case HitImpactCueKind.None:
                    return null;
                case HitImpactCueKind.Default:
                case HitImpactCueKind.Slash:
                case HitImpactCueKind.Blow:
                default:
                    return defaultHitImpactCueTag;
            }
        }
    }
}

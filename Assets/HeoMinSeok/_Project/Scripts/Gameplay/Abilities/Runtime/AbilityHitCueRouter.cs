using UnityEngine;

namespace UnityGAS
{
    public sealed class AbilityHitCueRouter
    {
        private const string DefaultHitConfirmTagResourcePath = "Tags/Event.HitConfirm";

        private readonly AbilitySystem owner;
        private readonly GameplayCueManager cueManager;
        private readonly AbilityGameplayEventChannel eventChannel;
        private readonly GameplayTag hitConfirmRootTag;

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

            if (this.eventChannel != null)
                this.eventChannel.GameplayEventRaised += HandleGameplayEvent;
        }

        public void Dispose()
        {
            if (eventChannel != null)
                eventChannel.GameplayEventRaised -= HandleGameplayEvent;
        }

        private void HandleGameplayEvent(GameplayTag raisedTag, AbilityEventData data)
        {
            if (cueManager == null || hitConfirmRootTag == null)
                return;

            if (!MatchesRequestedTag(raisedTag, hitConfirmRootTag))
                return;

            AbilityDefinition definition = data.Spec != null ? data.Spec.Definition : null;
            if (definition == null || definition.cueOnHitConfirmed == null)
                return;

            cueManager.ExecuteCue(definition.cueOnHitConfirmed, BuildCueParams(data, definition));
        }

        private GameplayCueParams BuildCueParams(AbilityEventData data, AbilityDefinition definition)
        {
            GameObject ownerObject = owner != null ? owner.gameObject : null;
            GameObject causerObject = ResolveGameObject(data.Causer);
            GameObject targetObject = data.Target;
            bool hasExplicitPosition = data.WorldPosition != Vector3.zero;
            Vector3 position = hasExplicitPosition ? data.WorldPosition : Vector3.zero;

            return new GameplayCueParams
            {
                Instigator = data.Instigator != null ? data.Instigator : ownerObject,
                Causer = causerObject != null ? causerObject : (data.Instigator != null ? data.Instigator : ownerObject),
                Target = targetObject,
                Position = position,
                HasExplicitPosition = hasExplicitPosition,
                Normal = Vector3.up,
                SourceObject = definition,
                Magnitude = data.IsCriticalHit ? 2f : 1f
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
    }
}

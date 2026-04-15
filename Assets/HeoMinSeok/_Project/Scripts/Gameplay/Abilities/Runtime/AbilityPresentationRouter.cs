using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;
using static UnityGAS.AbilityDefinition;

namespace UnityGAS
{
    /// <summary>
    /// Ability 관련 Animation / GameplayCue 연출 전담 라우터.
    /// AbilitySystem은 "언제" 연출할지만 결정하고,
    /// 실제 Animator 선택 / Cue 실행 / CueParams 생성은 이 라우터가 맡는다.
    /// </summary>
    public sealed class AbilityPresentationRouter
    {
        private readonly GameObject ownerObject;
        private readonly Transform ownerTransform;
        private readonly GameplayCueManager cueManager;
        private readonly Dictionary<AbilitySpec, AudioHandle> castingLoopHandles = new();
        private readonly Dictionary<AbilitySpec, AudioHandle> activeLoopHandles = new();

        private readonly Animator playerAnimator;
        private Animator weaponAnimator;

        public AbilityPresentationRouter(
            GameObject ownerObject,
            GameplayCueManager cueManager,
            Animator playerAnimator,
            Animator weaponAnimator = null)
        {
            this.ownerObject = ownerObject;
            this.ownerTransform = ownerObject != null ? ownerObject.transform : null;
            this.cueManager = cueManager;
            this.playerAnimator = playerAnimator;
            this.weaponAnimator = weaponAnimator;
        }

        public Animator PlayerAnimator => playerAnimator;
        public Animator WeaponAnimator => weaponAnimator;

        public void RegisterWeaponAnimator(Animator newWeaponAnimator)
        {
            weaponAnimator = newWeaponAnimator;
        }

        public bool ShouldCancelOnWeaponEquipped(AbilitySystem system)
        {
            if (system == null)
                return false;

            return system.CurrentExecSpec?.Definition?.animationChannel == AnimationChannel.Weapon;
        }

        public void TryPlayAnimationTriggerHash(int triggerHash, AbilityDefinition def)
        {
            if (triggerHash == 0)
                return;

            Animator target = ResolveAnimationTarget(def);
            if (target == null)
                return;

            target.SetTrigger(triggerHash);
        }

        public void PlayCastStart(AbilityDefinition def, AbilitySpec spec, GameObject target)
        {
            if (def == null)
                return;

            var p = BuildCueParamsForAbility(def, target);
            StartOrReplaceLoop(castingLoopHandles, spec, def.audioWhileCasting, p);
            WorldPresentationRuntime.PlayMerged(
                def.presentationOnCastStart,
                def.audioOnCastStart,
                def.cameraShakeOnCastStart,
                BuildWorldPresentationContext(p));
            WorldPresentationRuntime.PlayMerged(
                def.presentationWhileCasting,
                default,
                def.cameraShakeWhileCasting,
                BuildWorldPresentationContext(p));

            ExecuteCues(def.EnumerateCuesOnCastStart(), p);
            AddCues(def.EnumerateCuesWhileCasting(), p);
        }

        public void PlayCastCommit(AbilityDefinition def, AbilitySpec spec, GameObject target)
        {
            if (def == null)
                return;

            var p = BuildCueParamsForAbility(def, target);
            StopLoop(castingLoopHandles, spec);
            WorldPresentationRuntime.PlayMerged(
                def.presentationOnCommit,
                def.audioOnCommit,
                def.cameraShakeOnCommit,
                BuildWorldPresentationContext(p));

            RemoveCues(def.EnumerateCuesWhileCasting(), p);
            ExecuteCues(def.EnumerateCuesOnCommit(), p);
        }

        public void PlayCastCancelled(AbilityDefinition def, AbilitySpec spec, GameObject target)
        {
            if (def == null)
                return;

            var p = BuildCueParamsForAbility(def, target);
            StopLoop(castingLoopHandles, spec);
            WorldPresentationRuntime.PlayMerged(
                def.presentationOnCastCancelled,
                def.audioOnCastCancelled,
                def.cameraShakeOnCastCancelled,
                BuildWorldPresentationContext(p));

            RemoveCues(def.EnumerateCuesWhileCasting(), p);
            ExecuteCues(def.EnumerateCuesOnCastCancelled(), p);
        }

        public void PlayExecutionStart(AbilityDefinition def, AbilitySpec spec, GameObject target)
        {
            if (def == null)
                return;

            var p = BuildCueParamsForAbility(def, target);
            StartOrReplaceLoop(activeLoopHandles, spec, def.audioWhileActive, p);
            WorldPresentationRuntime.PlayMerged(
                def.presentationWhileActive,
                default,
                def.cameraShakeWhileActive,
                BuildWorldPresentationContext(p));

            AddCues(def.EnumerateCuesWhileActive(), p);
        }

        public void PlayExecutionEnd(AbilityDefinition def, AbilitySpec spec, GameObject target, bool cancelled)
        {
            if (def == null)
                return;

            var p = BuildCueParamsForAbility(def, target);
            StopLoop(activeLoopHandles, spec);

            RemoveCues(def.EnumerateCuesWhileActive(), p);

            if (cancelled)
            {
                WorldPresentationRuntime.PlayMerged(
                    def.presentationOnExecutionCancelled,
                    def.audioOnExecutionCancelled,
                    def.cameraShakeOnExecutionCancelled,
                    BuildWorldPresentationContext(p));

                ExecuteCues(def.EnumerateCuesOnExecutionCancelled(), p);
            }
            else
            {
                WorldPresentationRuntime.PlayMerged(
                    def.presentationOnEnd,
                    def.audioOnEnd,
                    def.cameraShakeOnEnd,
                    BuildWorldPresentationContext(p));

                ExecuteCues(def.EnumerateCuesOnEnd(), p);
            }
        }

        public GameplayCueParams BuildCueParamsForAbility(AbilityDefinition def, GameObject target)
        {
            bool hasSelfPosition = target == null && ownerTransform != null;
            Vector3 position = target != null
                ? target.transform.position
                : (ownerTransform != null ? ownerTransform.position : Vector3.zero);

            return new GameplayCueParams
            {
                Instigator = ownerObject,
                Causer = ownerObject,
                Target = target,
                Position = position,
                HasExplicitPosition = hasSelfPosition,
                Normal = Vector3.up,
                SourceObject = def,
                Magnitude = 1f
            };
        }

        private static WorldPresentationContext BuildWorldPresentationContext(in GameplayCueParams cueParams)
        {
            Vector3 normal = cueParams.Normal.sqrMagnitude > 0.0001f ? cueParams.Normal : Vector3.up;
            return WorldPresentationContext.AtWorld(
                instigator: cueParams.Instigator,
                position: cueParams.Position,
                fallbackDirection: normal,
                target: cueParams.Target,
                sourceObject: cueParams.SourceObject,
                rotation: Quaternion.LookRotation(Vector3.forward, normal),
                causer: cueParams.Causer);
        }

        private Animator ResolveAnimationTarget(AbilityDefinition def)
        {
            if (def != null && def.animationChannel == AnimationChannel.Weapon)
                return weaponAnimator != null ? weaponAnimator : playerAnimator;

            return playerAnimator != null ? playerAnimator : weaponAnimator;
        }

        private static SoundPlaybackContext BuildSoundContext(GameplayCueParams p)
        {
            return new SoundPlaybackContext
            {
                Instigator = p.Instigator,
                Causer = p.Causer,
                Target = p.Target,
                Position = p.Position,
                SourceObject = p.SourceObject
            };
        }

        private static void StartOrReplaceLoop(
            Dictionary<AbilitySpec, AudioHandle> handleMap,
            AbilitySpec spec,
            SoundRef soundRef,
            GameplayCueParams p)
        {
            if (spec == null)
                return;

            StopLoop(handleMap, spec);

            if (!soundRef.IsSet)
                return;

            AudioHandle handle = SoundManager.EnsureInstance().Play(soundRef, BuildSoundContext(p));
            if (handle.IsValid)
                handleMap[spec] = handle;
        }

        private static void StopLoop(Dictionary<AbilitySpec, AudioHandle> handleMap, AbilitySpec spec)
        {
            if (spec == null || !handleMap.TryGetValue(spec, out AudioHandle handle))
                return;

            SoundManager.EnsureInstance().Stop(handle);
            handleMap.Remove(spec);
        }

        private void ExecuteCues(IEnumerable<GameplayTag> cues, GameplayCueParams cueParams)
        {
            if (cueManager == null || cues == null)
                return;

            foreach (GameplayTag cue in cues)
            {
                if (cue != null)
                    cueManager.ExecuteCue(cue, cueParams);
            }
        }

        private void AddCues(IEnumerable<GameplayTag> cues, GameplayCueParams cueParams)
        {
            if (cueManager == null || cues == null)
                return;

            foreach (GameplayTag cue in cues)
            {
                if (cue != null)
                    cueManager.AddCue(cue, cueParams);
            }
        }

        private void RemoveCues(IEnumerable<GameplayTag> cues, GameplayCueParams cueParams)
        {
            if (cueManager == null || cues == null)
                return;

            foreach (GameplayTag cue in cues)
            {
                if (cue != null)
                    cueManager.RemoveCue(cue, cueParams);
            }
        }
    }
}

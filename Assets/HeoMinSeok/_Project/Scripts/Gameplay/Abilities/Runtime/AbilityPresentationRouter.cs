using System.Collections.Generic;
using CapstoneAudio;
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
            PlayOneShot(def.audioOnCastStart, p);
            StartOrReplaceLoop(castingLoopHandles, spec, def.audioWhileCasting, p);

            if (cueManager != null && def.cueOnCastStart != null)
                cueManager.ExecuteCue(def.cueOnCastStart, p);

            if (cueManager != null && def.cueWhileCasting != null)
                cueManager.AddCue(def.cueWhileCasting, p);
        }

        public void PlayCastCommit(AbilityDefinition def, AbilitySpec spec, GameObject target)
        {
            if (def == null)
                return;

            var p = BuildCueParamsForAbility(def, target);
            StopLoop(castingLoopHandles, spec);
            PlayOneShot(def.audioOnCommit, p);

            if (cueManager != null && def.cueWhileCasting != null)
                cueManager.RemoveCue(def.cueWhileCasting, p);

            if (cueManager != null && def.cueOnCommit != null)
                cueManager.ExecuteCue(def.cueOnCommit, p);
        }

        public void PlayCastCancelled(AbilityDefinition def, AbilitySpec spec, GameObject target)
        {
            if (def == null)
                return;

            var p = BuildCueParamsForAbility(def, target);
            StopLoop(castingLoopHandles, spec);
            PlayOneShot(def.audioOnCastCancelled, p);

            if (cueManager != null && def.cueWhileCasting != null)
                cueManager.RemoveCue(def.cueWhileCasting, p);

            if (cueManager != null && def.cueOnCastCancelled != null)
                cueManager.ExecuteCue(def.cueOnCastCancelled, p);
        }

        public void PlayExecutionStart(AbilityDefinition def, AbilitySpec spec, GameObject target)
        {
            if (def == null)
                return;

            var p = BuildCueParamsForAbility(def, target);
            StartOrReplaceLoop(activeLoopHandles, spec, def.audioWhileActive, p);

            if (cueManager != null && def.cueWhileActive != null)
                cueManager.AddCue(def.cueWhileActive, p);
        }

        public void PlayExecutionEnd(AbilityDefinition def, AbilitySpec spec, GameObject target, bool cancelled)
        {
            if (def == null)
                return;

            var p = BuildCueParamsForAbility(def, target);
            StopLoop(activeLoopHandles, spec);

            if (cueManager != null && def.cueWhileActive != null)
                cueManager.RemoveCue(def.cueWhileActive, p);

            if (cancelled)
            {
                PlayOneShot(def.audioOnExecutionCancelled, p);

                if (cueManager != null && def.cueOnExecutionCancelled != null)
                    cueManager.ExecuteCue(def.cueOnExecutionCancelled, p);
            }
            else
            {
                PlayOneShot(def.audioOnEnd, p);

                if (cueManager != null && def.cueOnEnd != null)
                    cueManager.ExecuteCue(def.cueOnEnd, p);
            }
        }

        public GameplayCueParams BuildCueParamsForAbility(AbilityDefinition def, GameObject target)
        {
            return new GameplayCueParams
            {
                Instigator = ownerObject,
                Causer = ownerObject,
                Target = target,
                Position = target != null
                    ? target.transform.position
                    : (ownerTransform != null ? ownerTransform.position : Vector3.zero),
                HasExplicitPosition = false,
                Normal = Vector3.up,
                SourceObject = def,
                Magnitude = 1f
            };
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

        private static void PlayOneShot(SoundRef soundRef, GameplayCueParams p)
        {
            if (!soundRef.IsSet)
                return;

            SoundManager.EnsureInstance().Play(soundRef, BuildSoundContext(p));
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
    }
}

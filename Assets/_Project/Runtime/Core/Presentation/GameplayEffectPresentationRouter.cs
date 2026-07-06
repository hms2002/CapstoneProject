using System;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// GameplayEffect 관련 GameplayCue 연출 전담 라우터.
    /// Runner는 "언제" 연출할지만 결정하고,
    /// 실제 Cue 실행 / Add / Remove / CueParams 생성은 이 라우터가 맡는다.
    /// </summary>
    public sealed class GameplayEffectPresentationRouter
    {
        private readonly GameplayCueManager cueManager;
        private readonly Dictionary<LoopKey, AudioHandle> activeLoopHandles = new();
        private readonly Dictionary<LoopKey, List<GameObject>> activeVisualHandles = new();

        private readonly struct LoopKey : IEquatable<LoopKey>
        {
            public readonly int EffectId;
            public readonly int TargetId;
            public readonly int SourceId;

            public LoopKey(int effectId, int targetId, int sourceId)
            {
                EffectId = effectId;
                TargetId = targetId;
                SourceId = sourceId;
            }

            public bool Equals(LoopKey other)
            {
                return EffectId == other.EffectId
                       && TargetId == other.TargetId
                       && SourceId == other.SourceId;
            }

            public override bool Equals(object obj)
            {
                return obj is LoopKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + EffectId;
                    hash = (hash * 31) + TargetId;
                    hash = (hash * 31) + SourceId;
                    return hash;
                }
            }
        }

        public GameplayEffectPresentationRouter(GameplayCueManager cueManager)
        {
            this.cueManager = cueManager;
        }

        public void PlayExecute(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            GameObject target,
            UnityEngine.Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
        {
            if (effect == null)
                return;

            GameplayPresentationPhase executePhase = effect.GetExecutePhase();
            GameplayCueParams cueParams = BuildCueParams(instigator, causer, target, sourceObject, magnitude, ctx);
            WorldPresentationPlayback.PlayMerged(
                executePhase.Presentation,
                executePhase.Sound,
                executePhase.CameraShake,
                BuildWorldPresentationContext(cueParams));

            if (cueManager != null)
            {
                foreach (GameplayTag cue in executePhase.EnumerateCues())
                {
                    if (cue != null)
                        cueManager.ExecuteCue(cue, cueParams);
                }
            }
        }

        public void AddWhileActive(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            GameObject target,
            UnityEngine.Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
        {
            if (effect == null)
                return;

            GameplayPresentationPhase activePhase = effect.GetWhileActivePhase();
            GameplayCueParams cueParams = BuildCueParams(instigator, causer, target, sourceObject, magnitude, ctx);
            LoopKey loopKey = MakeLoopKey(effect, target, sourceObject);
            StartLoop(loopKey, activePhase.Sound, cueParams);
            // Sustained effect phase:
            // - sound loops while the duration effect stays active
            // - presentation / shake fire once on enter
            // - cues are added here and removed in RemoveWhileActive(...)
            SpawnManualWhileActiveVisuals(loopKey, activePhase.Presentation, BuildWorldPresentationContext(cueParams));
            WorldPresentationHook autoReleasePresentation = BuildAutoReleasePresentation(activePhase.Presentation);
            WorldPresentationPlayback.PlayMerged(
                autoReleasePresentation,
                default,
                activePhase.CameraShake,
                BuildWorldPresentationContext(cueParams));

            if (cueManager != null)
            {
                foreach (GameplayTag cue in activePhase.EnumerateCues())
                {
                    if (cue != null)
                        cueManager.AddCue(cue, cueParams);
                }
            }
        }

        public void RemoveWhileActive(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            GameObject target,
            UnityEngine.Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
        {
            if (effect == null)
                return;

            GameplayPresentationPhase activePhase = effect.GetWhileActivePhase();
            GameplayCueParams cueParams = BuildCueParams(instigator, causer, target, sourceObject, magnitude, ctx);
            LoopKey loopKey = MakeLoopKey(effect, target, sourceObject);
            StopLoop(loopKey);
            ReleaseWhileActiveVisuals(loopKey);

            if (cueManager != null)
            {
                foreach (GameplayTag cue in activePhase.EnumerateCues())
                {
                    if (cue != null)
                        cueManager.RemoveCue(cue, cueParams);
                }
            }
        }

        public void PlayRemove(
            GameplayEffect effect,
            GameObject instigator,
            GameObject causer,
            GameObject target,
            UnityEngine.Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
        {
            if (effect == null)
                return;

            GameplayPresentationPhase removePhase = effect.GetRemovePhase();
            GameplayCueParams cueParams = BuildCueParams(instigator, causer, target, sourceObject, magnitude, ctx);
            StopLoop(MakeLoopKey(effect, target, sourceObject));
            WorldPresentationPlayback.PlayMerged(
                removePhase.Presentation,
                removePhase.Sound,
                removePhase.CameraShake,
                BuildWorldPresentationContext(cueParams));

            if (cueManager != null)
            {
                foreach (GameplayTag cue in removePhase.EnumerateCues())
                {
                    if (cue != null)
                        cueManager.ExecuteCue(cue, cueParams);
                }
            }
        }

        private GameplayCueParams BuildCueParams(
            GameObject instigator,
            GameObject causer,
            GameObject target,
            UnityEngine.Object sourceObject,
            float magnitude,
            GameplayEffectContext ctx)
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
                    RaycastHit h = ctx.Hit3D.Value;
                    p.Position = h.point;
                    p.HasExplicitPosition = true;
                    p.Normal = h.normal;
                    return p;
                }

                if (ctx.Hit2D.HasValue)
                {
                    RaycastHit2D h2 = ctx.Hit2D.Value;
                    p.Position = h2.point;
                    p.HasExplicitPosition = true;
                    p.Normal = h2.normal;
                    return p;
                }

                if (ctx.HasWorldPosition)
                {
                    p.Position = ctx.WorldPosition;
                    p.HasExplicitPosition = true;
                    p.Normal = ctx.WorldNormal.sqrMagnitude > 0.0001f ? ctx.WorldNormal : Vector3.up;
                    return p;
                }
            }

            p.Position = target != null ? target.transform.position : Vector3.zero;
            p.HasExplicitPosition = false;
            p.Normal = Vector3.up;
            return p;
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

        private static LoopKey MakeLoopKey(GameplayEffect effect, GameObject target, UnityEngine.Object sourceObject)
        {
            return new LoopKey(
                effect != null ? effect.GetInstanceID() : 0,
                target != null ? target.GetInstanceID() : 0,
                sourceObject != null ? sourceObject.GetInstanceID() : 0);
        }

        private static WorldPresentationHook BuildAutoReleasePresentation(in WorldPresentationHook presentation)
        {
            return new WorldPresentationHook
            {
                sound = presentation.sound,
                randomSounds = presentation.randomSounds,
                additionalSounds = presentation.additionalSounds,
                cameraShake = presentation.cameraShake,
                effect = ShouldSpawnManualWhileActive(presentation.effect) ? default : presentation.effect,
                particle = ShouldSpawnManualWhileActive(presentation.particle) ? default : presentation.particle
            };
        }

        private void SpawnManualWhileActiveVisuals(
            LoopKey key,
            in WorldPresentationHook presentation,
            in WorldPresentationContext context)
        {
            if (activeVisualHandles.ContainsKey(key))
                return;

            List<GameObject> handles = null;
            SpawnManualWhileActiveVisual(presentation.effect, context, ref handles);
            SpawnManualWhileActiveVisual(presentation.particle, context, ref handles);

            if (handles != null && handles.Count > 0)
                activeVisualHandles[key] = handles;
        }

        private void SpawnManualWhileActiveVisual(
            in SpawnedPresentationHook hook,
            in WorldPresentationContext context,
            ref List<GameObject> handles)
        {
            if (!ShouldSpawnManualWhileActive(hook))
                return;

            GameObject instance = WorldPresentationPlayback.SpawnPersistent(hook, context);
            if (instance == null)
                return;

            handles ??= new List<GameObject>(2);
            handles.Add(instance);
        }

        private void ReleaseWhileActiveVisuals(LoopKey key)
        {
            if (!activeVisualHandles.TryGetValue(key, out List<GameObject> handles))
                return;

            activeVisualHandles.Remove(key);
            for (int i = 0; i < handles.Count; i++)
            {
                GameObject handle = handles[i];
                if (handle != null)
                    WorldPresentationPlayback.Release(handle);
            }
        }

        private static bool ShouldSpawnManualWhileActive(in SpawnedPresentationHook hook)
        {
            return hook.HasContent && hook.lifetimeMode == PresentationLifetimeMode.ManualRelease;
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

        private void StartLoop(LoopKey key, SoundRef soundRef, GameplayCueParams p)
        {
            if (activeLoopHandles.TryGetValue(key, out AudioHandle existingHandle)
                && SoundPlaybackUtility.IsPlaying(existingHandle))
            {
                return;
            }

            StopLoop(key);

            if (!soundRef.IsSet)
                return;

            AudioHandle newHandle = SoundPlaybackUtility.Play(soundRef, BuildSoundContext(p));
            if (newHandle.IsValid)
                activeLoopHandles[key] = newHandle;
        }

        private void StopLoop(LoopKey key)
        {
            if (!activeLoopHandles.TryGetValue(key, out AudioHandle handle))
                return;

            SoundPlaybackUtility.Stop(handle);
            activeLoopHandles.Remove(key);
        }
    }
}

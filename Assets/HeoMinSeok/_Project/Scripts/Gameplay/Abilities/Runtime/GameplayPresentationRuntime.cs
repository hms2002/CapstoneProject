using CapstoneAudio;
using UnityEngine;
using System.Collections.Generic;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - 비GAS 수동 연출도 GE/Ability와 같은 execute/whileActive/remove 구조로 오디오와 Cue를 재생한다.
    /// - 걷기, 허브 인트로처럼 직접 구현한 연출이더라도 같은 SoundRef / GameplayTag 규칙을 재사용하게 한다.
    /// </summary>
    public sealed class GameplayPresentationRuntime
    {
        private readonly GameObject ownerObject;
        private GameplayCueManager cachedCueManager;
        private AudioHandle activeLoopHandle;
        private readonly HashSet<GameplayTag> activeCuesWhileActive = new();
        private readonly List<GameplayTag> cueScratchBuffer = new();
        private readonly List<GameplayTag> cueRemovalBuffer = new();

        public GameplayPresentationRuntime(GameObject ownerObject, GameplayCueManager cueManager = null)
        {
            this.ownerObject = ownerObject;
            cachedCueManager = cueManager;
            activeLoopHandle = AudioHandle.Invalid;
        }

        public GameplayCueParams BuildParams(
            GameObject target = null,
            Object sourceObject = null,
            Vector3? explicitPosition = null,
            bool hasExplicitPosition = false,
            Vector3? normal = null,
            float magnitude = 1f,
            GameObject causer = null)
        {
            GameObject resolvedTarget = target != null ? target : ownerObject;
            Vector3 resolvedPosition = explicitPosition
                ?? (resolvedTarget != null ? resolvedTarget.transform.position
                : ownerObject != null ? ownerObject.transform.position
                : Vector3.zero);

            return new GameplayCueParams
            {
                Instigator = ownerObject,
                Causer = causer != null ? causer : ownerObject,
                Target = resolvedTarget,
                Position = resolvedPosition,
                HasExplicitPosition = explicitPosition.HasValue || hasExplicitPosition,
                Normal = normal ?? Vector3.up,
                SourceObject = sourceObject,
                Magnitude = magnitude
            };
        }

        public void Start(in GameplayPresentationDefinition definition, in GameplayCueParams cueParams)
        {
            if (!definition.HasAnyContent)
                return;

            GameplayCueParams executeParams = WithMagnitude(cueParams, definition.EffectiveExecuteCueMagnitude);
            GameplayCueParams activeParams = WithMagnitude(cueParams, definition.EffectiveWhileActiveCueMagnitude);

            PlayOneShot(definition.audioOnExecute, executeParams);
            ExecuteCues(definition.EnumerateCuesOnExecute(), executeParams);
            EnsureLoop(definition.audioWhileActive, activeParams);
            EnsureActiveCues(definition.EnumerateCuesWhileActive(), activeParams);
        }

        public void ExecuteOnly(in GameplayPresentationDefinition definition, in GameplayCueParams cueParams)
        {
            if (!definition.HasAnyContent)
                return;

            GameplayCueParams executeParams = WithMagnitude(cueParams, definition.EffectiveExecuteCueMagnitude);
            PlayOneShot(definition.audioOnExecute, executeParams);
            ExecuteCues(definition.EnumerateCuesOnExecute(), executeParams);
        }

        public void Stop(in GameplayPresentationDefinition definition, in GameplayCueParams cueParams, bool playRemove)
        {
            StopLoop();
            RemoveActiveCue(cueParams);

            if (!playRemove || !definition.HasAnyContent)
                return;

            GameplayCueParams removeParams = WithMagnitude(cueParams, definition.EffectiveRemoveCueMagnitude);
            PlayOneShot(definition.audioOnRemove, removeParams);
            ExecuteCues(definition.EnumerateCuesOnRemove(), removeParams);
        }

        private static SoundPlaybackContext BuildSoundContext(in GameplayCueParams cueParams)
        {
            return new SoundPlaybackContext
            {
                Instigator = cueParams.Instigator,
                Causer = cueParams.Causer,
                Target = cueParams.Target,
                Position = cueParams.Position,
                SourceObject = cueParams.SourceObject
            };
        }

        private static void PlayOneShot(SoundRef soundRef, in GameplayCueParams cueParams)
        {
            if (!soundRef.IsSet)
                return;

            SoundManager.EnsureInstance().Play(soundRef, BuildSoundContext(cueParams));
        }

        private static GameplayCueParams WithMagnitude(in GameplayCueParams cueParams, float magnitude)
        {
            GameplayCueParams adjusted = cueParams;
            adjusted.Magnitude = Mathf.Max(0f, magnitude);
            return adjusted;
        }

        private void EnsureLoop(SoundRef soundRef, in GameplayCueParams cueParams)
        {
            if (!soundRef.IsSet)
                return;

            SoundManager manager = SoundManager.EnsureInstance();
            if (manager.IsPlaying(activeLoopHandle))
                return;

            StopLoop();
            activeLoopHandle = manager.Play(soundRef, BuildSoundContext(cueParams));
        }

        private void StopLoop()
        {
            if (!activeLoopHandle.IsValid)
                return;

            SoundManager.EnsureInstance().Stop(activeLoopHandle);
            activeLoopHandle = AudioHandle.Invalid;
        }

        private void ExecuteCues(IEnumerable<GameplayTag> cueTags, in GameplayCueParams cueParams)
        {
            GameplayCueManager cueManager = ResolveCueManager();
            if (cueManager == null || cueTags == null)
                return;

            foreach (GameplayTag cueTag in cueTags)
            {
                if (cueTag != null)
                    cueManager.ExecuteCue(cueTag, cueParams);
            }
        }

        private void EnsureActiveCues(IEnumerable<GameplayTag> cueTags, in GameplayCueParams cueParams)
        {
            GameplayCueManager cueManager = ResolveCueManager();
            if (cueManager == null)
                return;

            cueScratchBuffer.Clear();
            cueRemovalBuffer.Clear();
            if (cueTags != null)
            {
                foreach (GameplayTag cueTag in cueTags)
                {
                    if (cueTag != null && !cueScratchBuffer.Contains(cueTag))
                        cueScratchBuffer.Add(cueTag);
                }
            }

            if (activeCuesWhileActive.Count > 0)
            {
                foreach (GameplayTag activeCue in activeCuesWhileActive)
                {
                    if (!cueScratchBuffer.Contains(activeCue))
                        cueRemovalBuffer.Add(activeCue);
                }
            }

            for (int i = 0; i < cueRemovalBuffer.Count; i++)
            {
                GameplayTag cueTag = cueRemovalBuffer[i];
                cueManager.RemoveCue(cueTag, cueParams);
                activeCuesWhileActive.Remove(cueTag);
            }

            for (int i = 0; i < cueScratchBuffer.Count; i++)
            {
                GameplayTag cueTag = cueScratchBuffer[i];
                if (activeCuesWhileActive.Add(cueTag))
                    cueManager.AddCue(cueTag, cueParams);
            }

            cueScratchBuffer.Clear();
            cueRemovalBuffer.Clear();
        }

        private void RemoveActiveCue(in GameplayCueParams cueParams)
        {
            if (activeCuesWhileActive.Count == 0)
                return;

            GameplayCueManager cueManager = ResolveCueManager();
            if (cueManager == null)
            {
                activeCuesWhileActive.Clear();
                return;
            }

            cueScratchBuffer.Clear();
            foreach (GameplayTag cueTag in activeCuesWhileActive)
                cueScratchBuffer.Add(cueTag);

            for (int i = 0; i < cueScratchBuffer.Count; i++)
            {
                GameplayTag cueTag = cueScratchBuffer[i];
                if (cueTag != null)
                    cueManager.RemoveCue(cueTag, cueParams);
            }

            activeCuesWhileActive.Clear();
            cueScratchBuffer.Clear();
        }

        private GameplayCueManager ResolveCueManager()
        {
            if (cachedCueManager != null)
                return cachedCueManager;

#if UNITY_2023_1_OR_NEWER
            cachedCueManager = Object.FindAnyObjectByType<GameplayCueManager>();
#else
            cachedCueManager = Object.FindObjectOfType<GameplayCueManager>();
#endif
            return cachedCueManager;
        }
    }
}

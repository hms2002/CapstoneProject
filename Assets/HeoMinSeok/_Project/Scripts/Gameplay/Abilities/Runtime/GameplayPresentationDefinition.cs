using System;
using System.Collections.Generic;
using CapstoneAudio;
using UnityEngine;

namespace UnityGAS
{
    [Serializable]
    public struct GameplayPresentationDefinition
    {
        [Header("Audio (Optional)")]
        public SoundRef audioOnExecute;
        public SoundRef audioWhileActive;
        public SoundRef audioOnRemove;

        [Header("GameplayCue (Optional)")]
        public List<GameplayTag> cuesOnExecute;
        public List<GameplayTag> cuesWhileActive;
        public List<GameplayTag> cuesOnRemove;

        [Header("Cue Magnitude")]
        [Min(0f)] public float executeCueMagnitude;
        [Min(0f)] public float whileActiveCueMagnitude;
        [Min(0f)] public float removeCueMagnitude;

        [HideInInspector] public GameplayTag cueOnExecute;
        [HideInInspector] public GameplayTag cueWhileActive;
        [HideInInspector] public GameplayTag cueOnRemove;

        public bool HasAnyContent =>
            audioOnExecute.IsSet ||
            audioWhileActive.IsSet ||
            audioOnRemove.IsSet ||
            cueOnExecute != null ||
            cueWhileActive != null ||
            cueOnRemove != null ||
            HasCueEntries(cuesOnExecute) ||
            HasCueEntries(cuesWhileActive) ||
            HasCueEntries(cuesOnRemove);

        public IEnumerable<GameplayTag> EnumerateCuesOnExecute() => EnumerateCueTags(cuesOnExecute, cueOnExecute);
        public IEnumerable<GameplayTag> EnumerateCuesWhileActive() => EnumerateCueTags(cuesWhileActive, cueWhileActive);
        public IEnumerable<GameplayTag> EnumerateCuesOnRemove() => EnumerateCueTags(cuesOnRemove, cueOnRemove);

        public float EffectiveExecuteCueMagnitude => Mathf.Approximately(executeCueMagnitude, 0f) ? 1f : executeCueMagnitude;
        public float EffectiveWhileActiveCueMagnitude => Mathf.Approximately(whileActiveCueMagnitude, 0f) ? 1f : whileActiveCueMagnitude;
        public float EffectiveRemoveCueMagnitude => Mathf.Approximately(removeCueMagnitude, 0f) ? 1f : removeCueMagnitude;

        private static bool HasCueEntries(List<GameplayTag> cueList)
        {
            if (cueList == null)
                return false;

            for (int i = 0; i < cueList.Count; i++)
            {
                if (cueList[i] != null)
                    return true;
            }

            return false;
        }

        private static IEnumerable<GameplayTag> EnumerateCueTags(
            List<GameplayTag> cueList,
            GameplayTag legacyCue)
        {
            HashSet<GameplayTag> yielded = null;

            if (legacyCue != null)
            {
                yielded = new HashSet<GameplayTag> { legacyCue };
                yield return legacyCue;
            }

            if (cueList == null)
                yield break;

            for (int i = 0; i < cueList.Count; i++)
            {
                GameplayTag cue = cueList[i];
                if (cue == null)
                    continue;

                yielded ??= new HashSet<GameplayTag>();
                if (yielded.Add(cue))
                    yield return cue;
            }
        }
    }
}

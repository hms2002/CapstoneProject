using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Shared presentation contract for execute / while-active / remove style phases.
    ///
    /// Authoring rule:
    /// - Sound on sustained phases is treated as a loop that starts once on phase enter and stops on phase exit.
    /// - Camera shake on sustained phases is an enter pulse, not a per-frame continuous shake.
    /// - Spawned presentation on sustained phases is spawned once on phase enter.
    /// - Cue lists on sustained phases are added once on phase enter and removed once on phase exit.
    /// </summary>
    public readonly struct GameplayPresentationPhase
    {
        private readonly List<GameplayTag> cues;
        private readonly GameplayTag legacyCue;
        private readonly List<GameplayTag> legacyExtraCues;

        public readonly SoundRef Sound;
        public readonly CameraShakeHook CameraShake;
        public readonly WorldPresentationHook Presentation;
        public readonly float CueMagnitude;

        private GameplayPresentationPhase(
            SoundRef sound,
            CameraShakeHook cameraShake,
            WorldPresentationHook presentation,
            List<GameplayTag> cues,
            GameplayTag legacyCue,
            List<GameplayTag> legacyExtraCues,
            float cueMagnitude)
        {
            Sound = sound;
            CameraShake = cameraShake;
            Presentation = presentation;
            this.cues = cues;
            this.legacyCue = legacyCue;
            this.legacyExtraCues = legacyExtraCues;
            CueMagnitude = cueMagnitude;
        }

        public bool HasAnyContent =>
            Sound.IsSet ||
            CameraShake.amplitude > 0f ||
            Presentation.HasAnyContent ||
            legacyCue != null ||
            HasCueEntries(cues) ||
            HasCueEntries(legacyExtraCues);

        public float EffectiveCueMagnitude => Mathf.Approximately(CueMagnitude, 0f) ? 1f : CueMagnitude;

        public static GameplayPresentationPhase Create(
            SoundRef sound,
            CameraShakeHook cameraShake,
            WorldPresentationHook presentation,
            List<GameplayTag> cues,
            GameplayTag legacyCue = null,
            List<GameplayTag> legacyExtraCues = null,
            float cueMagnitude = 0f)
        {
            return new GameplayPresentationPhase(sound, cameraShake, presentation, cues, legacyCue, legacyExtraCues, cueMagnitude);
        }

        public IEnumerable<GameplayTag> EnumerateCues()
        {
            HashSet<GameplayTag> yielded = null;

            if (legacyCue != null)
            {
                yielded = new HashSet<GameplayTag> { legacyCue };
                yield return legacyCue;
            }

            if (legacyExtraCues != null)
            {
                for (int i = 0; i < legacyExtraCues.Count; i++)
                {
                    GameplayTag cue = legacyExtraCues[i];
                    if (cue == null)
                        continue;

                    yielded ??= new HashSet<GameplayTag>();
                    if (yielded.Add(cue))
                        yield return cue;
                }
            }

            if (cues == null)
                yield break;

            for (int i = 0; i < cues.Count; i++)
            {
                GameplayTag cue = cues[i];
                if (cue == null)
                    continue;

                yielded ??= new HashSet<GameplayTag>();
                if (yielded.Add(cue))
                    yield return cue;
            }
        }

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
    }
}

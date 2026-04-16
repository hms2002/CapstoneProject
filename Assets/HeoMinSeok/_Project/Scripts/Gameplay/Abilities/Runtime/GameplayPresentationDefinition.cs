using System;
using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Reusable execute / active / remove presentation bundle.
    ///
    /// Authoring rule for the "While Active" slot:
    /// - Audio loops once from enter to exit.
    /// - Camera shake fires once on enter.
    /// - Spawned presentation fires once on enter.
    /// - Cues are added on enter and removed on exit.
    /// </summary>
    [Serializable]
    public struct GameplayPresentationDefinition
    {
        [Header("Audio (Optional)")]
        public SoundRef audioOnExecute;
        [Tooltip("Loops once on enter and stays alive until Stop(...). Not retriggered every frame.")]
        public SoundRef audioWhileActive;
        public SoundRef audioOnRemove;

        [Header("Camera Shake (Optional)")]
        public CameraShakeHook cameraShakeOnExecute;
        [Tooltip("Played once on active enter. Use a looping sound or a custom persistent camera solution for continuous intensity.")]
        public CameraShakeHook cameraShakeWhileActive;
        public CameraShakeHook cameraShakeOnRemove;

        [Header("Spawned Presentation (Optional)")]
        public WorldPresentationHook presentationOnExecute;
        [Tooltip("Spawned once on active enter. Use a looping prefab or ManualRelease lifetime if it should persist.")]
        public WorldPresentationHook presentationWhileActive;
        public WorldPresentationHook presentationOnRemove;

        [Header("GameplayCue (Optional)")]
        public List<GameplayTag> cuesOnExecute;
        [Tooltip("Added once on active enter and removed on active exit.")]
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
            GetExecutePhase().HasAnyContent ||
            GetWhileActivePhase().HasAnyContent ||
            GetRemovePhase().HasAnyContent;

        public GameplayPresentationPhase GetExecutePhase() => GameplayPresentationPhase.Create(
            audioOnExecute,
            cameraShakeOnExecute,
            presentationOnExecute,
            cuesOnExecute,
            cueOnExecute,
            cueMagnitude: executeCueMagnitude);

        public GameplayPresentationPhase GetWhileActivePhase() => GameplayPresentationPhase.Create(
            audioWhileActive,
            cameraShakeWhileActive,
            presentationWhileActive,
            cuesWhileActive,
            cueWhileActive,
            cueMagnitude: whileActiveCueMagnitude);

        public GameplayPresentationPhase GetRemovePhase() => GameplayPresentationPhase.Create(
            audioOnRemove,
            cameraShakeOnRemove,
            presentationOnRemove,
            cuesOnRemove,
            cueOnRemove,
            cueMagnitude: removeCueMagnitude);

        public IEnumerable<GameplayTag> EnumerateCuesOnExecute() => GetExecutePhase().EnumerateCues();
        public IEnumerable<GameplayTag> EnumerateCuesWhileActive() => GetWhileActivePhase().EnumerateCues();
        public IEnumerable<GameplayTag> EnumerateCuesOnRemove() => GetRemovePhase().EnumerateCues();

        public float EffectiveExecuteCueMagnitude => GetExecutePhase().EffectiveCueMagnitude;
        public float EffectiveWhileActiveCueMagnitude => GetWhileActivePhase().EffectiveCueMagnitude;
        public float EffectiveRemoveCueMagnitude => GetRemovePhase().EffectiveCueMagnitude;
    }
}

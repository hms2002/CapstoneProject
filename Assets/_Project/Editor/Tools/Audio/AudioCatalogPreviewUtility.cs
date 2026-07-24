#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CapstoneAudio.EditorTools
{
    internal static class AudioCatalogPreviewUtility
    {
        private static GameObject previewRoot;
        private static AudioSource previewSource;
        private static readonly List<AudioSource> previewSources = new();
        private static AudioClip originalPreviewClip;
        private static double fadeStartTime;
        private static float fadeStartVolume;
        private static float fadeTargetVolume;
        private static float fadeDurationSeconds;
        private static bool isFading;
        private static bool stopWhenFadeCompletes;

        static AudioCatalogPreviewUtility()
        {
            EditorApplication.update += HandleEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorApplication.quitting += StopPreview;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        public static bool CanPreview => true;
        public static bool HasActivePreview => HasPlayingPreviewSource() || isFading;

        public static bool IsPreviewing(AudioClip clip)
        {
            return clip != null &&
                   originalPreviewClip == clip &&
                   (HasPlayingPreviewSource() || isFading);
        }

        public static void PlayVariant(
            AudioClip clip,
            float volume,
            float playbackSpeed,
            float pitchMin,
            float pitchMax,
            bool loop = false,
            float fadeInSeconds = 0f)
        {
            if (clip == null)
                return;

            StopPreviewImmediate();
            previewSource = CreatePreviewSource("Preview_0");
            if (previewSource == null)
                return;

            ResetFadeState();
            float targetVolume = Mathf.Clamp01(volume);
            ConfigurePreviewSource(
                previewSource,
                clip,
                fadeInSeconds > 0f ? 0f : targetVolume,
                playbackSpeed,
                pitchMin,
                pitchMax,
                loop);
            originalPreviewClip = clip;
            previewSource.Play();

            if (fadeInSeconds > 0f)
                BeginFade(targetVolume, fadeInSeconds, stopOnComplete: false);
        }

        public static void PlayVariants(
            IReadOnlyList<AudioClip> clips,
            float volume,
            float playbackSpeed,
            float pitchMin,
            float pitchMax)
        {
            if (clips == null || clips.Count == 0)
                return;

            StopPreviewImmediate();
            ResetFadeState();

            int playableIndex = 0;
            AudioClip firstPlayableClip = null;
            for (int i = 0; i < clips.Count; i++)
            {
                AudioClip clip = clips[i];
                if (clip == null)
                    continue;

                firstPlayableClip ??= clip;

                AudioSource source = CreatePreviewSource($"Preview_{playableIndex}");
                if (source == null)
                    continue;

                ConfigurePreviewSource(
                    source,
                    clip,
                    Mathf.Clamp01(volume),
                    playbackSpeed,
                    pitchMin,
                    pitchMax,
                    loop: false);
                source.Play();
                playableIndex++;
            }

            originalPreviewClip = playableIndex == 1 ? firstPlayableClip : null;

            if (playableIndex == 0)
                CleanupPreviewSource();
        }

        public static void StopPreview()
        {
            StopPreview(0f);
        }

        public static void StopPreview(float fadeOutSeconds)
        {
            if (!HasPreviewSource())
            {
                originalPreviewClip = null;
                ResetFadeState();
                return;
            }

            if (fadeOutSeconds > 0f && previewSource.isPlaying)
            {
                BeginFade(0f, fadeOutSeconds, stopOnComplete: true);
                return;
            }

            StopPreviewImmediate();
        }

        private static void StopPreviewImmediate()
        {
            for (int i = 0; i < previewSources.Count; i++)
            {
                if (previewSources[i] != null)
                    previewSources[i].Stop();
            }

            originalPreviewClip = null;
            CleanupPreviewSource();
        }

        private static void HandleEditorUpdate()
        {
            if (!HasPreviewSource())
                return;

            if (isFading)
            {
                UpdateFade();
                if (!HasPreviewSource())
                    return;
            }

            if (HasPlayingPreviewSource())
                return;

            originalPreviewClip = null;
            CleanupPreviewSource();
        }

        private static void HandlePlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode ||
                change == PlayModeStateChange.ExitingPlayMode)
            {
                StopPreview();
            }
        }

        private static AudioSource CreatePreviewSource(string sourceName)
        {
            EnsurePreviewRoot();
            if (previewRoot == null)
                return null;

            GameObject sourceRoot = new GameObject(sourceName);
            sourceRoot.hideFlags = HideFlags.HideAndDontSave;
            sourceRoot.transform.SetParent(previewRoot.transform, false);

            AudioSource source = sourceRoot.AddComponent<AudioSource>();
            source.hideFlags = HideFlags.HideAndDontSave;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            previewSources.Add(source);

            if (previewSource == null)
                previewSource = source;

            return source;
        }

        private static void EnsurePreviewRoot()
        {
            if (previewRoot != null)
                return;

            previewRoot = new GameObject("[AudioCatalogPreview]");
            previewRoot.hideFlags = HideFlags.HideAndDontSave;
        }

        private static void ConfigurePreviewSource(
            AudioSource source,
            AudioClip clip,
            float volume,
            float playbackSpeed,
            float pitchMin,
            float pitchMax,
            bool loop)
        {
            if (source == null || clip == null)
                return;

            source.Stop();
            source.clip = clip;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = Mathf.Clamp01(volume);
            float clampedPitchMin = Mathf.Clamp(Mathf.Min(pitchMin, pitchMax), 0.1f, 3f);
            float clampedPitchMax = Mathf.Clamp(Mathf.Max(pitchMin, pitchMax), 0.1f, 3f);
            float runtimePitch = Mathf.Approximately(clampedPitchMin, clampedPitchMax)
                ? clampedPitchMin
                : Random.Range(clampedPitchMin, clampedPitchMax);
            source.pitch = runtimePitch * Mathf.Clamp(playbackSpeed, 0.1f, 3f);
            source.time = 0f;
        }

        private static void CleanupPreviewSource()
        {
            ResetFadeState();

            if (previewRoot != null)
                Object.DestroyImmediate(previewRoot);

            previewRoot = null;
            previewSource = null;
            previewSources.Clear();
        }

        private static bool HasPreviewSource()
        {
            return previewRoot != null && previewSources.Count > 0;
        }

        private static bool HasPlayingPreviewSource()
        {
            for (int i = 0; i < previewSources.Count; i++)
            {
                AudioSource source = previewSources[i];
                if (source != null && source.isPlaying)
                    return true;
            }

            return false;
        }

        private static void BeginFade(float targetVolume, float durationSeconds, bool stopOnComplete)
        {
            if (previewSource == null)
                return;

            fadeStartTime = EditorApplication.timeSinceStartup;
            fadeStartVolume = previewSource.volume;
            fadeTargetVolume = Mathf.Clamp01(targetVolume);
            fadeDurationSeconds = Mathf.Max(0f, durationSeconds);
            stopWhenFadeCompletes = stopOnComplete;
            isFading = fadeDurationSeconds > 0f &&
                       !Mathf.Approximately(fadeStartVolume, fadeTargetVolume);

            if (!isFading)
                CompleteFade();
        }

        private static void UpdateFade()
        {
            if (previewSource == null)
            {
                ResetFadeState();
                return;
            }

            if (fadeDurationSeconds <= 0f)
            {
                CompleteFade();
                return;
            }

            float progress = Mathf.Clamp01(
                (float)((EditorApplication.timeSinceStartup - fadeStartTime) / fadeDurationSeconds));
            previewSource.volume = Mathf.Lerp(fadeStartVolume, fadeTargetVolume, progress);

            if (progress >= 1f)
                CompleteFade();
        }

        private static void CompleteFade()
        {
            if (previewSource != null)
                previewSource.volume = fadeTargetVolume;

            bool shouldStop = stopWhenFadeCompletes;
            ResetFadeState();

            if (shouldStop)
                StopPreviewImmediate();
        }

        private static void ResetFadeState()
        {
            fadeStartTime = 0d;
            fadeStartVolume = 0f;
            fadeTargetVolume = 0f;
            fadeDurationSeconds = 0f;
            isFading = false;
            stopWhenFadeCompletes = false;
        }
    }
}
#endif

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CapstoneAudio.EditorTools
{
    internal static class AudioCatalogPreviewUtility
    {
        private static GameObject previewRoot;
        private static AudioSource previewSource;
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
        public static bool HasActivePreview => previewSource != null && (previewSource.isPlaying || isFading);

        public static bool IsPreviewing(AudioClip clip)
        {
            return clip != null &&
                   originalPreviewClip == clip &&
                   previewSource != null &&
                   (previewSource.isPlaying || isFading);
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

            EnsurePreviewSource();
            if (previewSource == null)
                return;

            ResetFadeState();
            previewSource.Stop();
            previewSource.clip = clip;
            previewSource.loop = loop;
            previewSource.playOnAwake = false;
            previewSource.spatialBlend = 0f;
            float targetVolume = Mathf.Clamp01(volume);
            previewSource.volume = fadeInSeconds > 0f ? 0f : targetVolume;
            float clampedPitchMin = Mathf.Clamp(Mathf.Min(pitchMin, pitchMax), 0.1f, 3f);
            float clampedPitchMax = Mathf.Clamp(Mathf.Max(pitchMin, pitchMax), 0.1f, 3f);
            float runtimePitch = Mathf.Approximately(clampedPitchMin, clampedPitchMax)
                ? clampedPitchMin
                : Random.Range(clampedPitchMin, clampedPitchMax);
            previewSource.pitch = runtimePitch * Mathf.Clamp(playbackSpeed, 0.1f, 3f);
            previewSource.time = 0f;

            originalPreviewClip = clip;
            previewSource.Play();

            if (fadeInSeconds > 0f)
                BeginFade(targetVolume, fadeInSeconds, stopOnComplete: false);
        }

        public static void StopPreview()
        {
            StopPreview(0f);
        }

        public static void StopPreview(float fadeOutSeconds)
        {
            if (previewSource == null)
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
            if (previewSource != null)
                previewSource.Stop();

            originalPreviewClip = null;
            CleanupPreviewSource();
        }

        private static void HandleEditorUpdate()
        {
            if (previewSource == null)
                return;

            if (isFading)
            {
                UpdateFade();
                if (previewSource == null)
                    return;
            }

            if (previewSource.isPlaying)
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

        private static void EnsurePreviewSource()
        {
            if (previewSource != null)
                return;

            previewRoot = new GameObject("[AudioCatalogPreview]");
            previewRoot.hideFlags = HideFlags.HideAndDontSave;
            previewSource = previewRoot.AddComponent<AudioSource>();
            previewSource.hideFlags = HideFlags.HideAndDontSave;
        }

        private static void CleanupPreviewSource()
        {
            ResetFadeState();

            if (previewRoot != null)
                Object.DestroyImmediate(previewRoot);

            previewRoot = null;
            previewSource = null;
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

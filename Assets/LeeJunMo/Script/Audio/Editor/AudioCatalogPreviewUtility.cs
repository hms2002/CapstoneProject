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

        static AudioCatalogPreviewUtility()
        {
            EditorApplication.update += HandleEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorApplication.quitting += StopPreview;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        public static bool CanPreview => true;

        public static bool IsPreviewing(AudioClip clip)
        {
            return clip != null &&
                   originalPreviewClip == clip &&
                   previewSource != null &&
                   previewSource.isPlaying;
        }

        public static void PlayVariant(
            AudioClip clip,
            float volume,
            float playbackSpeed,
            float pitchMin,
            float pitchMax)
        {
            if (clip == null)
                return;

            EnsurePreviewSource();
            if (previewSource == null)
                return;

            previewSource.Stop();
            previewSource.clip = clip;
            previewSource.loop = false;
            previewSource.playOnAwake = false;
            previewSource.spatialBlend = 0f;
            previewSource.volume = Mathf.Clamp01(volume);
            float clampedPitchMin = Mathf.Clamp(Mathf.Min(pitchMin, pitchMax), 0.1f, 3f);
            float clampedPitchMax = Mathf.Clamp(Mathf.Max(pitchMin, pitchMax), 0.1f, 3f);
            float runtimePitch = Mathf.Approximately(clampedPitchMin, clampedPitchMax)
                ? clampedPitchMin
                : Random.Range(clampedPitchMin, clampedPitchMax);
            previewSource.pitch = runtimePitch * Mathf.Clamp(playbackSpeed, 0.1f, 3f);
            previewSource.time = 0f;

            originalPreviewClip = clip;
            previewSource.Play();
        }

        public static void StopPreview()
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
            if (previewRoot != null)
                Object.DestroyImmediate(previewRoot);

            previewRoot = null;
            previewSource = null;
        }
    }
}
#endif

using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace CapstoneAudio
{
    public sealed class SoundManager : MonoBehaviour
    {
        public const string DefaultCatalogResourcesPath = "Audio/DefaultAudioCatalog";
        private const string MasterVolumePrefKey = "settings.audio.master";
        private const string MusicVolumePrefKey = "settings.audio.music";
        private const string SfxVolumePrefKey = "settings.audio.sfx";

        public static SoundManager Instance { get; private set; }

        [Header("Catalogs")]
        [SerializeField] private AudioCatalogSO defaultCatalog;
        [SerializeField] private List<AudioCatalogSO> additionalCatalogs = new();

        [Header("Pooling")]
        [SerializeField] private int sfxPoolSize = 18;
        [SerializeField] private int importantSfxPoolSize = 8;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float masterMusicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float masterSfxVolume = 1f;
        [SerializeField, Min(0f)] private float bgmFadeDuration = 0.5f;

        private readonly List<AudioSource> normalSfxSources = new();
        private readonly List<AudioSource> importantSfxSources = new();
        private readonly Stack<AudioSource> idleLoopSources = new();
        private readonly Dictionary<int, AudioSource> activeLoopSources = new();
        private readonly Dictionary<string, float> nextPlayableTimes =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> missingKeyWarnings =
            new(StringComparer.OrdinalIgnoreCase);

        private Transform oneShotRoot;
        private Transform loopRoot;
        private AudioSource musicSource;
        private bool initialized;
        private int nextHandleId = 1;
        private float currentMusicBaseVolume = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static SoundManager EnsureInstance()
        {
            if (Instance != null)
                return Instance;

#if UNITY_2023_1_OR_NEWER
            SoundManager existing = FindAnyObjectByType<SoundManager>();
#else
            SoundManager existing = FindObjectOfType<SoundManager>();
#endif
            if (existing != null)
            {
                Instance = existing;
                existing.EnsureInitialized();
                return existing;
            }

            GameObject root = new GameObject("[SoundManager]");
            return root.AddComponent<SoundManager>();
        }

        public bool IsPlaying(AudioHandle handle)
        {
            if (!handle.IsValid)
                return false;

            return activeLoopSources.TryGetValue(handle.Id, out AudioSource source)
                   && source != null
                   && source.isPlaying;
        }

        public AudioHandle Play(in SoundRef soundRef, in SoundPlaybackContext context)
        {
            if (!soundRef.IsSet)
                return AudioHandle.Invalid;

            EnsureInitialized();

            if (!TryResolveEntry(soundRef.key, out AudioCatalogEntry entry))
            {
                WarnMissingKey(soundRef.key);
                return AudioHandle.Invalid;
            }

            if (!entry.HasPlayableClip)
                return AudioHandle.Invalid;

            if (entry.bus == AudioBus.BGM)
            {
                PlayMusicInternal(entry, soundRef);
                return AudioHandle.Invalid;
            }

            if (IsOnCooldown(soundRef.key, entry.cooldown))
                return AudioHandle.Invalid;

            return entry.loop
                ? PlayLoopInternal(entry, soundRef, context)
                : PlayOneShotInternal(entry, soundRef, context);
        }

        public void PlayLegacyClip(
            AudioClip clip,
            Vector3 worldPosition,
            float volume = 1f,
            bool spatial = true,
            bool important = false)
        {
            if (clip == null)
                return;

            EnsureInitialized();

            AudioSource source = GetOneShotSource(important);
            ConfigureDirectClip(source, clip, volume, spatial, worldPosition);
            source.Play();
        }

        public void PlayMusic(string key, float fadeDuration = -1f)
        {
            PlayMusic(SoundRef.FromKey(key), fadeDuration);
        }

        public void PlayMusic(in SoundRef soundRef, float fadeDuration = -1f)
        {
            if (!soundRef.IsSet)
                return;

            EnsureInitialized();

            if (!TryResolveEntry(soundRef.key, out AudioCatalogEntry entry))
            {
                WarnMissingKey(soundRef.key);
                return;
            }

            PlayMusicInternal(entry, soundRef, fadeDuration);
        }

        public void StopMusic(float fadeDuration = -1f)
        {
            EnsureInitialized();

            if (musicSource == null || !musicSource.isPlaying)
                return;

            float duration = fadeDuration >= 0f ? fadeDuration : bgmFadeDuration;
            musicSource.DOKill();

            if (duration <= 0f)
            {
                musicSource.Stop();
                musicSource.clip = null;
                musicSource.volume = 0f;
                return;
            }

            musicSource.DOFade(0f, duration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (musicSource == null)
                        return;

                    musicSource.Stop();
                    musicSource.clip = null;
                    musicSource.volume = 0f;
                });
        }

        public void Stop(AudioHandle handle, float fadeOutDuration = 0f)
        {
            if (!handle.IsValid)
                return;

            EnsureInitialized();

            if (!activeLoopSources.TryGetValue(handle.Id, out AudioSource source) || source == null)
                return;

            activeLoopSources.Remove(handle.Id);
            source.DOKill();

            if (fadeOutDuration <= 0f)
            {
                ReleaseLoopSource(source);
                return;
            }

            source.DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() => ReleaseLoopSource(source));
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MasterVolumePrefKey, masterVolume);

            if (musicSource == null)
                return;

            musicSource.DOKill();
            musicSource.volume = currentMusicBaseVolume * masterMusicVolume * masterVolume;
        }

        public void SetMusicVolume(float volume)
        {
            masterMusicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MusicVolumePrefKey, masterMusicVolume);
            if (musicSource == null)
                return;

            musicSource.DOKill();
            musicSource.volume = currentMusicBaseVolume * masterMusicVolume * masterVolume;
        }

        public void SetSfxVolume(float volume)
        {
            masterSfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(SfxVolumePrefKey, masterSfxVolume);
        }

        public float GetMasterVolume()
        {
            EnsureInitialized();
            return masterVolume;
        }

        public float GetMusicVolume()
        {
            EnsureInitialized();
            return masterMusicVolume;
        }

        public float GetSfxVolume()
        {
            EnsureInitialized();
            return masterSfxVolume;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void EnsureInitialized()
        {
            if (initialized)
                return;

            initialized = true;
            LoadVolumePreferences();

            if (defaultCatalog == null)
                defaultCatalog = Resources.Load<AudioCatalogSO>(DefaultCatalogResourcesPath);

            oneShotRoot = CreateRoot("OneShot");
            loopRoot = CreateRoot("Loops");
            musicSource = CreateAudioSource("Music", transform);
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;

            normalSfxSources.Clear();
            importantSfxSources.Clear();
            idleLoopSources.Clear();
            activeLoopSources.Clear();

            CreateOneShotPool(normalSfxSources, sfxPoolSize, "SFX");
            CreateOneShotPool(importantSfxSources, importantSfxPoolSize, "ImportantSFX");
        }

        private void LoadVolumePreferences()
        {
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePrefKey, masterVolume));
            masterMusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefKey, masterMusicVolume));
            masterSfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefKey, masterSfxVolume));
        }

        private Transform CreateRoot(string rootName)
        {
            GameObject root = new GameObject(rootName);
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        private void CreateOneShotPool(List<AudioSource> pool, int size, string prefix)
        {
            for (int i = 0; i < Mathf.Max(1, size); i++)
            {
                pool.Add(CreateAudioSource($"{prefix}_{i}", oneShotRoot));
            }
        }

        private AudioSource CreateAudioSource(string sourceName, Transform parent)
        {
            GameObject root = new GameObject(sourceName);
            root.transform.SetParent(parent, false);

            AudioSource source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.minDistance = 1f;
            source.maxDistance = 20f;
            return source;
        }

        private AudioHandle PlayOneShotInternal(
            AudioCatalogEntry entry,
            SoundRef soundRef,
            SoundPlaybackContext context)
        {
            if (!entry.TryPickClip(out AudioClip clip))
                return AudioHandle.Invalid;

            AudioSource source = GetOneShotSource(entry.important);
            ConfigureCatalogSource(source, clip, entry, soundRef, context);
            source.loop = false;
            source.Play();
            return AudioHandle.Invalid;
        }

        private AudioHandle PlayLoopInternal(
            AudioCatalogEntry entry,
            SoundRef soundRef,
            SoundPlaybackContext context)
        {
            if (!entry.TryPickClip(out AudioClip clip))
                return AudioHandle.Invalid;

            AudioSource source = idleLoopSources.Count > 0
                ? idleLoopSources.Pop()
                : CreateAudioSource($"Loop_{nextHandleId}", loopRoot);

            ConfigureCatalogSource(source, clip, entry, soundRef, context);
            source.loop = true;
            source.Play();

            AudioHandle handle = new AudioHandle(nextHandleId++);
            activeLoopSources[handle.Id] = source;
            return handle;
        }

        private void ReleaseLoopSource(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
            source.loop = false;
            source.volume = 1f;
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.transform.SetParent(loopRoot, false);
            source.transform.localPosition = Vector3.zero;
            idleLoopSources.Push(source);
        }

        private void PlayMusicInternal(
            AudioCatalogEntry entry,
            SoundRef soundRef,
            float fadeDuration = -1f)
        {
            if (!entry.TryPickClip(out AudioClip clip))
                return;

            EnsureInitialized();

            float duration = fadeDuration >= 0f ? fadeDuration : bgmFadeDuration;
            float targetVolume = entry.volume * soundRef.EffectiveVolumeMultiplier * masterMusicVolume * masterVolume;
            currentMusicBaseVolume = entry.volume * soundRef.EffectiveVolumeMultiplier;

            if (musicSource.clip == clip && musicSource.isPlaying)
            {
                musicSource.DOKill();
                musicSource.DOFade(targetVolume, duration).SetUpdate(true);
                return;
            }

            Action playNewClip = () =>
            {
                if (musicSource == null)
                    return;

                musicSource.clip = clip;
                musicSource.pitch = entry.PickAudioSourcePitch();
                musicSource.loop = entry.loop || entry.bus == AudioBus.BGM;
                musicSource.spatialBlend = 0f;
                musicSource.volume = duration > 0f ? 0f : targetVolume;
                musicSource.Play();

                if (duration > 0f)
                    musicSource.DOFade(targetVolume, duration).SetUpdate(true);
            };

            musicSource.DOKill();

            if (!musicSource.isPlaying || duration <= 0f)
            {
                playNewClip();
                return;
            }

            musicSource.DOFade(0f, duration)
                .SetUpdate(true)
                .OnComplete(() => playNewClip());
        }

        private AudioSource GetOneShotSource(bool important)
        {
            List<AudioSource> pool = important ? importantSfxSources : normalSfxSources;

            for (int i = 0; i < pool.Count; i++)
            {
                AudioSource source = pool[i];
                if (source != null && !source.isPlaying)
                    return source;
            }

            AudioSource recycled = pool[0];
            pool.RemoveAt(0);
            pool.Add(recycled);
            recycled.DOKill();
            recycled.Stop();
            return recycled;
        }

        private void ConfigureCatalogSource(
            AudioSource source,
            AudioClip clip,
            AudioCatalogEntry entry,
            SoundRef soundRef,
            SoundPlaybackContext context)
        {
            if (source == null || entry == null || clip == null)
                return;

            source.DOKill();
            source.clip = clip;
            source.pitch = entry.PickAudioSourcePitch();
            source.volume = entry.volume * soundRef.EffectiveVolumeMultiplier * masterSfxVolume * masterVolume;
            source.minDistance = Mathf.Max(0.01f, entry.minDistance);
            source.maxDistance = Mathf.Max(source.minDistance, entry.maxDistance);

            bool playAs2D = soundRef.anchorPolicy == SoundAnchorPolicy.TwoD || !entry.spatial;
            if (playAs2D)
            {
                source.spatialBlend = 0f;
                source.transform.SetParent(oneShotRoot, false);
                source.transform.localPosition = Vector3.zero;
                return;
            }

            source.spatialBlend = 1f;

            Transform follow = ResolveFollowTarget(soundRef.anchorPolicy, context);
            if (follow != null)
            {
                source.transform.SetParent(follow, false);
                source.transform.localPosition = soundRef.localOffset;
            }
            else
            {
                source.transform.SetParent(oneShotRoot, false);
                source.transform.position = ResolveWorldPosition(soundRef.anchorPolicy, context) + soundRef.localOffset;
            }
        }

        private void ConfigureDirectClip(
            AudioSource source,
            AudioClip clip,
            float volume,
            bool spatial,
            Vector3 worldPosition)
        {
            if (source == null || clip == null)
                return;

            source.DOKill();
            source.clip = clip;
            source.pitch = 1f;
            source.loop = false;
            source.volume = Mathf.Clamp01(volume) * masterSfxVolume * masterVolume;

            if (spatial)
            {
                source.spatialBlend = 1f;
                source.transform.SetParent(oneShotRoot, false);
                source.transform.position = worldPosition;
            }
            else
            {
                source.spatialBlend = 0f;
                source.transform.SetParent(oneShotRoot, false);
                source.transform.localPosition = Vector3.zero;
            }
        }

        private bool TryResolveEntry(string key, out AudioCatalogEntry entry)
        {
            entry = null;

            if (defaultCatalog != null && defaultCatalog.TryGetEntry(key, out entry))
                return true;

            for (int i = 0; i < additionalCatalogs.Count; i++)
            {
                AudioCatalogSO catalog = additionalCatalogs[i];
                if (catalog != null && catalog.TryGetEntry(key, out entry))
                    return true;
            }

            return false;
        }

        private bool IsOnCooldown(string key, float cooldown)
        {
            if (cooldown <= 0f || string.IsNullOrWhiteSpace(key))
                return false;

            float now = Time.unscaledTime;
            if (nextPlayableTimes.TryGetValue(key, out float nextPlayableTime) && now < nextPlayableTime)
                return true;

            nextPlayableTimes[key] = now + cooldown;
            return false;
        }

        private void WarnMissingKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !missingKeyWarnings.Add(key))
                return;

            Debug.LogWarning($"[SoundManager] Audio key '{key}' was not found in any loaded catalog.", this);
        }

        private static Transform ResolveFollowTarget(SoundAnchorPolicy policy, SoundPlaybackContext context)
        {
            switch (policy)
            {
                case SoundAnchorPolicy.Instigator:
                    return context.Instigator != null ? context.Instigator.transform : null;
                case SoundAnchorPolicy.Causer:
                    return context.Causer != null ? context.Causer.transform : null;
                case SoundAnchorPolicy.Target:
                    return context.Target != null ? context.Target.transform : null;
                default:
                    return null;
            }
        }

        private static Vector3 ResolveWorldPosition(SoundAnchorPolicy policy, SoundPlaybackContext context)
        {
            switch (policy)
            {
                case SoundAnchorPolicy.Instigator:
                    return context.Instigator != null
                        ? context.Instigator.transform.position
                        : context.Position;
                case SoundAnchorPolicy.Causer:
                    return context.Causer != null
                        ? context.Causer.transform.position
                        : context.Position;
                case SoundAnchorPolicy.Target:
                    return context.Target != null
                        ? context.Target.transform.position
                        : context.Position;
                case SoundAnchorPolicy.CuePosition:
                case SoundAnchorPolicy.CatalogDefault:
                default:
                    return context.Position;
            }
        }
    }
}

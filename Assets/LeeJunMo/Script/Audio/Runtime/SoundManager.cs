using System;
using System.Collections.Generic;
using CapstoneRuntime;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CapstoneAudio
{
    public sealed class SoundManager : MonoBehaviour
    {
        // 이 클래스의 책임:
        // 카탈로그 기반 사운드 재생, 루프 핸들 관리, 오디오 풀링, 볼륨/피치 런타임 제어를 총괄한다.

        /// <summary>
        /// 책임:
        /// 사용자 볼륨 설정과 별개로 런타임 ducking을 다시 계산할 수 있도록 AudioSource별 원본 재생 정보를 보관한다.
        /// </summary>
        private sealed class RuntimeSoundState
        {
            public AudioCatalogSO Catalog;
            public string SoundKey;
            public SoundRef SoundRef;
            public SoundPlaybackContext Context;
            public AudioCategory Category;
            public float BaseVolume;
            public float PitchRoll;
            public bool LoopPlayback;

            public bool IsCatalogBacked =>
                Catalog != null &&
                !string.IsNullOrWhiteSpace(SoundKey);
        }

        private readonly struct SameSourceOneShotKey : IEquatable<SameSourceOneShotKey>
        {
            public SameSourceOneShotKey(string soundKey, int sourceId)
            {
                SoundKey = soundKey;
                SourceId = sourceId;
            }

            public string SoundKey { get; }
            public int SourceId { get; }

            public bool Equals(SameSourceOneShotKey other)
            {
                return SourceId == other.SourceId &&
                       string.Equals(SoundKey, other.SoundKey, StringComparison.OrdinalIgnoreCase);
            }

            public override bool Equals(object obj)
            {
                return obj is SameSourceOneShotKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + SourceId;
                    hash = (hash * 31) + StringComparer.OrdinalIgnoreCase.GetHashCode(SoundKey ?? string.Empty);
                    return hash;
                }
            }
        }

        public const string DefaultCatalogResourcesPath = "Audio/DefaultAudioCatalog";
        private const string MasterVolumePrefKey = "settings.audio.master";
        private const string MusicVolumePrefKey = "settings.audio.music";
        private const string SfxVolumePrefKey = "settings.audio.sfx";
        private const float SameSourceOneShotSuppressSeconds = 0.05f;
        private const int SameSourceOneShotPruneThreshold = 256;

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
        [HideInInspector]
        [SerializeField, Min(0f)] private float bgmFadeDuration = 0.5f;

        private readonly List<AudioSource> normalSfxSources = new();
        private readonly List<AudioSource> importantSfxSources = new();
        private readonly Stack<AudioSource> idleLoopSources = new();
        private readonly Dictionary<int, AudioSource> activeLoopSources = new();
        private readonly Dictionary<int, AudioSource> activeTrackedOneShotSources = new();
        private readonly Dictionary<AudioSource, RuntimeSoundState> runtimeSoundStates = new();
        private readonly Dictionary<string, float> nextPlayableTimes =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<SameSourceOneShotKey, float> nextSameSourceOneShotTimes = new();
        private readonly List<SameSourceOneShotKey> expiredSameSourceOneShotKeys = new();
        private readonly List<int> expiredTrackedOneShotIds = new();
        private readonly List<AudioClip> simultaneousOneShotClips = new();
        private readonly HashSet<string> missingKeyWarnings =
            new(StringComparer.OrdinalIgnoreCase);

        private Transform oneShotRoot;
        private Transform loopRoot;
        private AudioSource musicSource;
        private bool initialized;
        private int nextHandleId = 1;
        private AudioCatalogSO currentMusicCatalog;
        private string currentMusicKey;
        private float currentMusicBaseVolume = 1f;
        private float currentMusicVolumeMultiplier = 1f;
        private float currentMusicPitchRoll;
        private bool musicStopPending;
        private float combatSfxDuckVolume = 1f;
        private Tween combatSfxDuckTween;

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
            SoundManager existing = RuntimeServiceOwnership.FindExistingService<SoundManager>();
#else
            SoundManager existing = RuntimeServiceOwnership.FindExistingService<SoundManager>();
#endif
            if (existing != null)
            {
                Instance = existing;
                existing.EnsureInitialized();
                return existing;
            }

            GameObject root = RuntimeServiceOwnership.CreateServiceHost("[SoundManager]");
            return root.AddComponent<SoundManager>();
        }

        public bool IsPlaying(AudioHandle handle)
        {
            if (!handle.IsValid)
                return false;

            PruneTrackedOneShotSources();

            return (activeLoopSources.TryGetValue(handle.Id, out AudioSource source) ||
                    activeTrackedOneShotSources.TryGetValue(handle.Id, out source)) &&
                   source != null &&
                   source.isPlaying;
        }

        public AudioHandle Play(in SoundRef soundRef, in SoundPlaybackContext context)
        {
            if (!soundRef.IsSet)
                return AudioHandle.Invalid;

            EnsureInitialized();

            if (!TryResolveEntry(soundRef.key, out AudioCatalogEntry entry, out AudioCatalogSO catalog))
            {
                WarnMissingKey(soundRef.key);
                return AudioHandle.Invalid;
            }

            if (!entry.HasPlayableClip)
                return AudioHandle.Invalid;

            if (entry.bus == AudioBus.BGM)
            {
                PlayMusicInternal(catalog, entry, soundRef);
                return AudioHandle.Invalid;
            }

            if (!entry.loop && IsSameSourceOneShotSuppressed(entry, context))
                return AudioHandle.Invalid;

            if (IsOnCooldown(soundRef.key, entry.cooldown))
                return AudioHandle.Invalid;

            return entry.loop
                ? PlayLoopInternal(catalog, entry, soundRef, context)
                : PlayOneShotInternal(catalog, entry, soundRef, context);
        }

        public AudioHandle PlayTrackedOneShot(in SoundRef soundRef, in SoundPlaybackContext context)
        {
            if (!soundRef.IsSet)
                return AudioHandle.Invalid;

            EnsureInitialized();
            PruneTrackedOneShotSources();

            if (!TryResolveEntry(soundRef.key, out AudioCatalogEntry entry, out AudioCatalogSO catalog))
            {
                WarnMissingKey(soundRef.key);
                return AudioHandle.Invalid;
            }

            if (!entry.HasPlayableClip || entry.bus == AudioBus.BGM || entry.loop)
                return AudioHandle.Invalid;

            if (entry.UsesSimultaneousOneShotPlayback)
                return AudioHandle.Invalid;

            if (IsSameSourceOneShotSuppressed(entry, context))
                return AudioHandle.Invalid;

            if (IsOnCooldown(soundRef.key, entry.cooldown))
                return AudioHandle.Invalid;

            return PlayTrackedOneShotInternal(catalog, entry, soundRef, context);
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

        public void PlayMusic(string key)
        {
            PlayMusic(SoundRef.FromKey(key));
        }

        public void PlayMusic(in SoundRef soundRef)
        {
            if (!soundRef.IsSet)
                return;

            EnsureInitialized();

            if (!TryResolveEntry(soundRef.key, out AudioCatalogEntry entry, out AudioCatalogSO catalog))
            {
                WarnMissingKey(soundRef.key);
                return;
            }

            PlayMusicInternal(catalog, entry, soundRef);
        }

        public void StopMusic()
        {
            EnsureInitialized();

            if (musicSource == null || !musicSource.isPlaying)
                return;

            float duration = ResolveBgmFadeOutSeconds();
            musicSource.DOKill();

            if (duration <= 0f)
            {
                musicSource.Stop();
                musicSource.clip = null;
                musicSource.volume = 0f;
                ClearCurrentMusicTracking();
                return;
            }

            musicStopPending = true;
            musicSource.DOFade(0f, duration)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (musicSource == null)
                    {
                        ClearCurrentMusicTracking();
                        return;
                    }

                    musicSource.Stop();
                    musicSource.clip = null;
                    musicSource.volume = 0f;
                    ClearCurrentMusicTracking();
                });
        }

        public void Stop(AudioHandle handle, float fadeOutDuration = 0f)
        {
            if (!handle.IsValid)
                return;

            EnsureInitialized();

            if (activeLoopSources.TryGetValue(handle.Id, out AudioSource loopSource))
            {
                activeLoopSources.Remove(handle.Id);
                StopLoopSource(loopSource, fadeOutDuration);
                return;
            }

            PruneTrackedOneShotSources();
            if (!activeTrackedOneShotSources.TryGetValue(handle.Id, out AudioSource source) || source == null)
                return;

            activeTrackedOneShotSources.Remove(handle.Id);
            source.DOKill();

            if (fadeOutDuration <= 0f || !source.isPlaying)
            {
                ReleaseOneShotSource(source);
                return;
            }

            source.DOFade(0f, fadeOutDuration)
                .SetUpdate(true)
                .OnComplete(() => ReleaseOneShotSource(source));
        }

        public void SetPitch(AudioHandle handle, float pitch)
        {
            if (!handle.IsValid)
                return;

            EnsureInitialized();

            PruneTrackedOneShotSources();
            if ((!activeLoopSources.TryGetValue(handle.Id, out AudioSource source) &&
                 !activeTrackedOneShotSources.TryGetValue(handle.Id, out source)) ||
                source == null)
                return;

            source.pitch = Mathf.Clamp(pitch, 0.05f, 3f);
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MasterVolumePrefKey, masterVolume);
            RefreshAllRuntimeSfxVolumes();

            if (musicSource == null)
                return;

            musicSource.DOKill();
            musicSource.volume = ResolveCurrentMusicTargetVolume();
        }

        public void SetMusicVolume(float volume)
        {
            masterMusicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MusicVolumePrefKey, masterMusicVolume);
            if (musicSource == null)
                return;

            musicSource.DOKill();
            musicSource.volume = ResolveCurrentMusicTargetVolume();
        }

        public void SetSfxVolume(float volume)
        {
            masterSfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(SfxVolumePrefKey, masterSfxVolume);
            RefreshAllRuntimeSfxVolumes();
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

        public void RefreshCatalogRuntime(AudioCatalogSO catalog)
        {
            if (catalog == null)
                return;

            EnsureInitialized();
            if (!UsesCatalog(catalog))
                return;

            catalog.MarkLookupDirty();
            RefreshCatalogBackedSfx(catalog);

            bool defaultCatalogChanged = defaultCatalog == catalog;
            if (currentMusicCatalog == catalog)
                RefreshCurrentMusicFromCatalog(catalog);
            else if (defaultCatalogChanged)
                RefreshCurrentMusicVolume();

            if (defaultCatalogChanged)
                RefreshAllRuntimeSfxVolumes();
        }

        public void DuckCombatSfx(float targetVolume, float fadeSeconds)
        {
            EnsureInitialized();

            combatSfxDuckTween?.Kill();
            targetVolume = Mathf.Clamp01(targetVolume);
            fadeSeconds = Mathf.Max(0f, fadeSeconds);

            if (fadeSeconds <= 0f)
            {
                ApplyCombatSfxDuckVolume(targetVolume);
                return;
            }

            combatSfxDuckTween = DOTween
                .To(() => combatSfxDuckVolume, ApplyCombatSfxDuckVolume, targetVolume, fadeSeconds)
                .SetUpdate(true);
        }

        public void ResetCombatSfxDuck(float fadeSeconds = 0f)
        {
            DuckCombatSfx(1f, fadeSeconds);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            RuntimeServiceOwnership.Adopt(this);
            EnsureInitialized();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            combatSfxDuckTween?.Kill();

            if (Instance == this)
                Instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetCombatSfxDuck(0f);
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
            activeTrackedOneShotSources.Clear();
            runtimeSoundStates.Clear();

            CreateOneShotPool(normalSfxSources, sfxPoolSize, "SFX");
            CreateOneShotPool(importantSfxSources, importantSfxPoolSize, "ImportantSFX");
        }

        private void LoadVolumePreferences()
        {
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePrefKey, masterVolume));
            masterMusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePrefKey, masterMusicVolume));
            masterSfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefKey, masterSfxVolume));
        }

        private float ResolveGlobalVolumeMultiplier()
        {
            return defaultCatalog != null
                ? defaultCatalog.GlobalVolumeMultiplier
                : 1f;
        }

        private float ResolveBgmFadeInSeconds()
        {
            return defaultCatalog != null
                ? defaultCatalog.BgmFadeInSeconds
                : Mathf.Max(0f, bgmFadeDuration);
        }

        private float ResolveBgmFadeOutSeconds()
        {
            return defaultCatalog != null
                ? defaultCatalog.BgmFadeOutSeconds
                : Mathf.Max(0f, bgmFadeDuration);
        }

        private float ResolveCurrentMusicTargetVolume()
        {
            return ResolveMusicTargetVolume(currentMusicBaseVolume);
        }

        private float ResolveMusicTargetVolume(float baseVolume)
        {
            return baseVolume
                   * ResolveGlobalVolumeMultiplier()
                   * masterMusicVolume
                   * masterVolume;
        }

        private void TrackCurrentMusic(
            AudioCatalogSO catalog,
            string key,
            float baseVolume,
            float volumeMultiplier,
            float pitchRoll)
        {
            currentMusicCatalog = catalog;
            currentMusicKey = key;
            currentMusicBaseVolume = Mathf.Max(0f, baseVolume);
            currentMusicVolumeMultiplier = Mathf.Max(0f, volumeMultiplier);
            currentMusicPitchRoll = Mathf.Clamp01(pitchRoll);
        }

        private void ClearCurrentMusicTracking()
        {
            currentMusicCatalog = null;
            currentMusicKey = null;
            currentMusicBaseVolume = 1f;
            currentMusicVolumeMultiplier = 1f;
            currentMusicPitchRoll = 0f;
            musicStopPending = false;
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
            AudioCatalogSO catalog,
            AudioCatalogEntry entry,
            SoundRef soundRef,
            SoundPlaybackContext context)
        {
            if (entry.UsesSimultaneousOneShotPlayback)
                return PlaySimultaneousOneShotInternal(catalog, entry, soundRef, context);

            if (!entry.TryPickClip(out AudioClip clip))
                return AudioHandle.Invalid;

            AudioSource source = GetOneShotSource(entry.important);
            ConfigureCatalogSource(source, clip, catalog, entry, soundRef, context, loopPlayback: false);
            source.loop = false;
            source.Play();
            return AudioHandle.Invalid;
        }

        private AudioHandle PlayTrackedOneShotInternal(
            AudioCatalogSO catalog,
            AudioCatalogEntry entry,
            SoundRef soundRef,
            SoundPlaybackContext context)
        {
            if (!entry.TryPickClip(out AudioClip clip))
                return AudioHandle.Invalid;

            AudioSource source = GetOneShotSource(entry.important);
            ConfigureCatalogSource(source, clip, catalog, entry, soundRef, context, loopPlayback: false);
            source.loop = false;
            source.Play();

            AudioHandle handle = new AudioHandle(nextHandleId++);
            activeTrackedOneShotSources[handle.Id] = source;
            return handle;
        }

        private AudioHandle PlaySimultaneousOneShotInternal(
            AudioCatalogSO catalog,
            AudioCatalogEntry entry,
            SoundRef soundRef,
            SoundPlaybackContext context)
        {
            if (!entry.TryGetPlayableClips(simultaneousOneShotClips))
                return AudioHandle.Invalid;

            for (int i = 0; i < simultaneousOneShotClips.Count; i++)
            {
                AudioClip clip = simultaneousOneShotClips[i];
                if (clip == null)
                    continue;

                AudioSource source = GetOneShotSource(entry.important);
                ConfigureCatalogSource(source, clip, catalog, entry, soundRef, context, loopPlayback: false);
                source.loop = false;
                source.Play();
            }

            simultaneousOneShotClips.Clear();
            return AudioHandle.Invalid;
        }

        private AudioHandle PlayLoopInternal(
            AudioCatalogSO catalog,
            AudioCatalogEntry entry,
            SoundRef soundRef,
            SoundPlaybackContext context)
        {
            if (!entry.TryPickClip(out AudioClip clip))
                return AudioHandle.Invalid;

            AudioSource source = idleLoopSources.Count > 0
                ? idleLoopSources.Pop()
                : CreateAudioSource($"Loop_{nextHandleId}", loopRoot);

            ConfigureCatalogSource(source, clip, catalog, entry, soundRef, context, loopPlayback: true);
            source.loop = true;
            source.Play();

            AudioHandle handle = new AudioHandle(nextHandleId++);
            activeLoopSources[handle.Id] = source;
            return handle;
        }

        private void StopLoopSource(AudioSource source, float fadeOutDuration)
        {
            if (source == null)
                return;

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
            runtimeSoundStates.Remove(source);
            idleLoopSources.Push(source);
        }

        private void ReleaseOneShotSource(AudioSource source)
        {
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
            source.loop = false;
            source.volume = 1f;
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.transform.SetParent(oneShotRoot, false);
            source.transform.localPosition = Vector3.zero;
            runtimeSoundStates.Remove(source);
            UntrackOneShotSource(source);
        }

        private void PlayMusicInternal(
            AudioCatalogSO catalog,
            AudioCatalogEntry entry,
            SoundRef soundRef)
        {
            if (!entry.TryPickClip(out AudioClip clip))
                return;

            EnsureInitialized();
            musicStopPending = false;

            float fadeInSeconds = ResolveBgmFadeInSeconds();
            float fadeOutSeconds = ResolveBgmFadeOutSeconds();
            bool isSameClipPlaying = musicSource.clip == clip && musicSource.isPlaying;
            float nextMusicVolumeMultiplier = soundRef.EffectiveVolumeMultiplier;
            float nextMusicBaseVolume = entry.volume * nextMusicVolumeMultiplier;
            float nextMusicPitchRoll = isSameClipPlaying ? currentMusicPitchRoll : UnityEngine.Random.value;
            float targetVolume = ResolveMusicTargetVolume(nextMusicBaseVolume);

            if (isSameClipPlaying)
            {
                TrackCurrentMusic(catalog, entry.key, nextMusicBaseVolume, nextMusicVolumeMultiplier, nextMusicPitchRoll);
                musicSource.DOKill();
                if (fadeInSeconds <= 0f)
                    musicSource.volume = targetVolume;
                else
                    musicSource.DOFade(targetVolume, fadeInSeconds).SetUpdate(true);
                return;
            }

            Action playNewClip = () =>
            {
                if (musicSource == null)
                    return;

                TrackCurrentMusic(catalog, entry.key, nextMusicBaseVolume, nextMusicVolumeMultiplier, nextMusicPitchRoll);
                musicSource.clip = clip;
                musicSource.pitch = entry.ResolveAudioSourcePitch(nextMusicPitchRoll);
                musicSource.loop = entry.loop || entry.bus == AudioBus.BGM;
                musicSource.spatialBlend = 0f;
                musicSource.volume = fadeInSeconds > 0f ? 0f : targetVolume;
                musicSource.Play();

                if (fadeInSeconds > 0f)
                    musicSource.DOFade(targetVolume, fadeInSeconds).SetUpdate(true);
            };

            musicSource.DOKill();

            if (!musicSource.isPlaying || fadeOutSeconds <= 0f)
            {
                playNewClip();
                return;
            }

            musicSource.DOFade(0f, fadeOutSeconds)
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
                {
                    UntrackOneShotSource(source);
                    return source;
                }
            }

            AudioSource recycled = pool[0];
            pool.RemoveAt(0);
            pool.Add(recycled);
            recycled.DOKill();
            recycled.Stop();
            runtimeSoundStates.Remove(recycled);
            UntrackOneShotSource(recycled);
            return recycled;
        }

        private void PruneTrackedOneShotSources()
        {
            expiredTrackedOneShotIds.Clear();
            foreach (KeyValuePair<int, AudioSource> pair in activeTrackedOneShotSources)
            {
                AudioSource source = pair.Value;
                if (source == null || !source.isPlaying)
                    expiredTrackedOneShotIds.Add(pair.Key);
            }

            for (int i = 0; i < expiredTrackedOneShotIds.Count; i++)
            {
                if (!activeTrackedOneShotSources.TryGetValue(expiredTrackedOneShotIds[i], out AudioSource source))
                    continue;

                if (source != null && !source.isPlaying)
                    runtimeSoundStates.Remove(source);

                activeTrackedOneShotSources.Remove(expiredTrackedOneShotIds[i]);
            }

            expiredTrackedOneShotIds.Clear();
        }

        private void UntrackOneShotSource(AudioSource source)
        {
            if (source == null || activeTrackedOneShotSources.Count == 0)
                return;

            expiredTrackedOneShotIds.Clear();
            foreach (KeyValuePair<int, AudioSource> pair in activeTrackedOneShotSources)
            {
                if (pair.Value == source)
                    expiredTrackedOneShotIds.Add(pair.Key);
            }

            for (int i = 0; i < expiredTrackedOneShotIds.Count; i++)
                activeTrackedOneShotSources.Remove(expiredTrackedOneShotIds[i]);

            expiredTrackedOneShotIds.Clear();
        }

        private void ConfigureCatalogSource(
            AudioSource source,
            AudioClip clip,
            AudioCatalogSO catalog,
            AudioCatalogEntry entry,
            SoundRef soundRef,
            SoundPlaybackContext context,
            bool loopPlayback)
        {
            if (source == null || entry == null || clip == null)
                return;

            source.DOKill();
            source.clip = clip;
            RuntimeSoundState state = TrackCatalogRuntimeSoundState(
                source,
                catalog,
                entry,
                soundRef,
                context,
                loopPlayback);
            ApplyCatalogRuntimeProperties(source, entry, state);
        }

        private void ApplyCatalogRuntimeProperties(
            AudioSource source,
            AudioCatalogEntry entry,
            RuntimeSoundState state)
        {
            if (source == null || entry == null || state == null)
                return;

            state.Category = entry.category;
            state.BaseVolume = Mathf.Max(0f, entry.volume * state.SoundRef.EffectiveVolumeMultiplier);

            source.pitch = entry.ResolveAudioSourcePitch(state.PitchRoll);
            source.volume = ResolveRuntimeSfxVolume(source);
            source.minDistance = Mathf.Max(0.01f, entry.minDistance);
            source.maxDistance = Mathf.Max(source.minDistance, entry.maxDistance);

            if (state.LoopPlayback)
                source.loop = entry.loop;

            bool playAs2D = state.SoundRef.anchorPolicy == SoundAnchorPolicy.TwoD || !entry.spatial;
            if (playAs2D)
            {
                source.spatialBlend = 0f;
                source.transform.SetParent(oneShotRoot, false);
                source.transform.localPosition = Vector3.zero;
                return;
            }

            source.spatialBlend = 1f;

            Transform follow = ResolveFollowTarget(state.SoundRef.anchorPolicy, state.Context);
            if (follow != null)
            {
                source.transform.SetParent(follow, false);
                source.transform.localPosition = state.SoundRef.localOffset;
            }
            else
            {
                source.transform.SetParent(oneShotRoot, false);
                source.transform.position =
                    ResolveWorldPosition(state.SoundRef.anchorPolicy, state.Context) + state.SoundRef.localOffset;
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
            TrackRuntimeSoundState(source, AudioCategory.Other, Mathf.Max(0f, volume));
            source.volume = ResolveRuntimeSfxVolume(source);

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

        private bool TryResolveEntry(
            string key,
            out AudioCatalogEntry entry,
            out AudioCatalogSO owningCatalog)
        {
            entry = null;
            owningCatalog = null;

            if (defaultCatalog != null && defaultCatalog.TryGetEntry(key, out entry))
            {
                owningCatalog = defaultCatalog;
                return true;
            }

            for (int i = 0; i < additionalCatalogs.Count; i++)
            {
                AudioCatalogSO catalog = additionalCatalogs[i];
                if (catalog != null && catalog.TryGetEntry(key, out entry))
                {
                    owningCatalog = catalog;
                    return true;
                }
            }

            return false;
        }

        private RuntimeSoundState TrackCatalogRuntimeSoundState(
            AudioSource source,
            AudioCatalogSO catalog,
            AudioCatalogEntry entry,
            SoundRef soundRef,
            SoundPlaybackContext context,
            bool loopPlayback)
        {
            if (source == null || entry == null)
                return null;

            RuntimeSoundState state = new RuntimeSoundState
            {
                Catalog = catalog,
                SoundKey = entry.key,
                SoundRef = soundRef,
                Context = context,
                Category = entry.category,
                BaseVolume = Mathf.Max(0f, entry.volume * soundRef.EffectiveVolumeMultiplier),
                PitchRoll = UnityEngine.Random.value,
                LoopPlayback = loopPlayback
            };

            runtimeSoundStates[source] = state;
            return state;
        }

        private void TrackRuntimeSoundState(AudioSource source, AudioCategory category, float baseVolume)
        {
            if (source == null)
                return;

            runtimeSoundStates[source] = new RuntimeSoundState
            {
                Category = category,
                BaseVolume = Mathf.Max(0f, baseVolume),
                PitchRoll = 0f,
                LoopPlayback = false
            };
        }

        private float ResolveRuntimeSfxVolume(AudioSource source)
        {
            if (source == null || !runtimeSoundStates.TryGetValue(source, out RuntimeSoundState state))
                return ResolveGlobalVolumeMultiplier() * masterSfxVolume * masterVolume;

            float duckMultiplier = ShouldDuckCombatCategory(state.Category) ? combatSfxDuckVolume : 1f;
            return state.BaseVolume
                   * ResolveGlobalVolumeMultiplier()
                   * masterSfxVolume
                   * masterVolume
                   * duckMultiplier;
        }

        private void ApplyCombatSfxDuckVolume(float value)
        {
            combatSfxDuckVolume = Mathf.Clamp01(value);
            RefreshAllRuntimeSfxVolumes();
        }

        private void RefreshAllRuntimeSfxVolumes()
        {
            foreach (KeyValuePair<AudioSource, RuntimeSoundState> pair in runtimeSoundStates)
            {
                AudioSource source = pair.Key;
                if (source == null)
                    continue;

                source.volume = ResolveRuntimeSfxVolume(source);
            }
        }

        private void RefreshCatalogBackedSfx(AudioCatalogSO catalog)
        {
            if (catalog == null)
                return;

            PruneTrackedOneShotSources();

            foreach (KeyValuePair<AudioSource, RuntimeSoundState> pair in runtimeSoundStates)
            {
                AudioSource source = pair.Key;
                RuntimeSoundState state = pair.Value;
                if (source == null ||
                    state == null ||
                    !source.isPlaying ||
                    !state.IsCatalogBacked ||
                    state.Catalog != catalog)
                {
                    continue;
                }

                if (!catalog.TryGetEntry(state.SoundKey, out AudioCatalogEntry entry) || entry == null)
                    continue;

                ApplyCatalogRuntimeProperties(source, entry, state);
            }
        }

        private void RefreshCurrentMusicFromCatalog(AudioCatalogSO catalog)
        {
            if (catalog == null ||
                musicSource == null ||
                !musicSource.isPlaying ||
                musicStopPending ||
                string.IsNullOrWhiteSpace(currentMusicKey))
            {
                return;
            }

            if (!catalog.TryGetEntry(currentMusicKey, out AudioCatalogEntry entry) ||
                entry == null ||
                entry.bus != AudioBus.BGM)
            {
                RefreshCurrentMusicVolume();
                return;
            }

            currentMusicBaseVolume = entry.volume * currentMusicVolumeMultiplier;
            musicSource.DOKill();
            musicSource.pitch = entry.ResolveAudioSourcePitch(currentMusicPitchRoll);
            musicSource.loop = entry.loop || entry.bus == AudioBus.BGM;
            musicSource.volume = ResolveCurrentMusicTargetVolume();
        }

        private void RefreshCurrentMusicVolume()
        {
            if (musicSource == null || !musicSource.isPlaying || musicStopPending)
                return;

            musicSource.DOKill();
            musicSource.volume = ResolveCurrentMusicTargetVolume();
        }

        private bool UsesCatalog(AudioCatalogSO catalog)
        {
            if (catalog == null)
                return false;

            if (defaultCatalog == catalog)
                return true;

            for (int i = 0; i < additionalCatalogs.Count; i++)
            {
                if (additionalCatalogs[i] == catalog)
                    return true;
            }

            return false;
        }

        private static bool ShouldDuckCombatCategory(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Ability:
                case AudioCategory.Effect:
                case AudioCategory.Enemy:
                case AudioCategory.Boss:
                case AudioCategory.World:
                    return true;
                default:
                    return false;
            }
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

        private bool IsSameSourceOneShotSuppressed(AudioCatalogEntry entry, in SoundPlaybackContext context)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key) || SameSourceOneShotSuppressSeconds <= 0f)
                return false;

            int sourceId = ResolveSameSourceSuppressionId(context);
            if (sourceId == 0)
                return false;

            PruneSameSourceOneShotTimesIfNeeded();

            var key = new SameSourceOneShotKey(entry.key, sourceId);
            float now = Time.unscaledTime;
            if (nextSameSourceOneShotTimes.TryGetValue(key, out float nextPlayableTime) && now < nextPlayableTime)
                return true;

            nextSameSourceOneShotTimes[key] = now + SameSourceOneShotSuppressSeconds;
            return false;
        }

        private void PruneSameSourceOneShotTimesIfNeeded()
        {
            if (nextSameSourceOneShotTimes.Count < SameSourceOneShotPruneThreshold)
                return;

            float now = Time.unscaledTime;
            expiredSameSourceOneShotKeys.Clear();
            foreach (KeyValuePair<SameSourceOneShotKey, float> pair in nextSameSourceOneShotTimes)
            {
                if (pair.Value <= now)
                    expiredSameSourceOneShotKeys.Add(pair.Key);
            }

            for (int i = 0; i < expiredSameSourceOneShotKeys.Count; i++)
                nextSameSourceOneShotTimes.Remove(expiredSameSourceOneShotKeys[i]);

            expiredSameSourceOneShotKeys.Clear();
        }

        private static int ResolveSameSourceSuppressionId(in SoundPlaybackContext context)
        {
            if (context.Causer != null)
                return context.Causer.GetInstanceID();

            if (context.Instigator != null)
                return context.Instigator.GetInstanceID();

            if (context.Target != null)
                return context.Target.GetInstanceID();

            return context.SourceObject != null ? context.SourceObject.GetInstanceID() : 0;
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

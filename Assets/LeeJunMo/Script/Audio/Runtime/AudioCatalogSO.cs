using System;
using System.Collections.Generic;
using UnityEngine;

namespace CapstoneAudio
{
    public enum AudioBus
    {
        Sfx,
        BGM
    }

    public enum AudioCategory
    {
        Other,
        UI,
        Dialogue,
        Ability,
        Effect,
        Player,
        Enemy,
        Boss,
        World,
        Merchant,
        Door,
        Music
    }

    [Serializable]
    public sealed class AudioCatalogEntry
    {
        public string key;
        public AudioBus bus = AudioBus.Sfx;
        public AudioCategory category = AudioCategory.Other;
        public AudioClip[] variants;

        [Range(0f, 1f)]
        public float volume = 1f;

        [Range(0.1f, 3f)]
        public float playbackSpeed = 1f;

        [Range(0.1f, 3f)]
        public float pitchMin = 1f;

        [Range(0.1f, 3f)]
        public float pitchMax = 1f;

        public bool loop;
        public bool spatial = true;
        public bool important;

        [Min(0f)]
        public float cooldown;

        [Min(0f)]
        public float minDistance = 1f;

        [Min(0f)]
        public float maxDistance = 20f;

        public bool HasPlayableClip
        {
            get
            {
                if (variants == null || variants.Length == 0)
                    return false;

                for (int i = 0; i < variants.Length; i++)
                {
                    if (variants[i] != null)
                        return true;
                }

                return false;
            }
        }

        public bool TryPickClip(out AudioClip clip)
        {
            clip = null;
            if (!HasPlayableClip)
                return false;

            int startIndex = UnityEngine.Random.Range(0, variants.Length);
            for (int i = 0; i < variants.Length; i++)
            {
                int index = (startIndex + i) % variants.Length;
                if (variants[index] == null)
                    continue;

                clip = variants[index];
                return true;
            }

            return false;
        }

        public float PickPitch()
        {
            float min = Mathf.Min(pitchMin, pitchMax);
            float max = Mathf.Max(pitchMin, pitchMax);
            return Mathf.Approximately(min, max)
                ? min
                : UnityEngine.Random.Range(min, max);
        }

        public float PickAudioSourcePitch()
        {
            return PickPitch() * Mathf.Max(0.1f, playbackSpeed);
        }

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                key = string.Empty;
            }
            else
            {
                key = key.Trim().ToLowerInvariant();
            }

            if (pitchMin <= 0f)
                pitchMin = 1f;

            if (pitchMax <= 0f)
                pitchMax = 1f;

            if (playbackSpeed <= 0f)
                playbackSpeed = 1f;

            if (maxDistance < minDistance)
                maxDistance = minDistance;
        }
    }

    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "Audio/Audio Catalog")]
    public sealed class AudioCatalogSO : ScriptableObject
    {
        [Header("Runtime Defaults")]
        [SerializeField, Min(0f)] private float globalVolumeMultiplier = 1f;
        [SerializeField, Min(0f)] private float bgmFadeInSeconds = 0.5f;
        [SerializeField, Min(0f)] private float bgmFadeOutSeconds = 0.5f;

        [SerializeField] private List<AudioCatalogEntry> entries = new();

        private readonly Dictionary<string, AudioCatalogEntry> lookup =
            new(StringComparer.OrdinalIgnoreCase);

        private bool lookupDirty = true;

        public IReadOnlyList<AudioCatalogEntry> Entries => entries;
        public float GlobalVolumeMultiplier => Mathf.Max(0f, globalVolumeMultiplier);
        public float BgmFadeInSeconds => Mathf.Max(0f, bgmFadeInSeconds);
        public float BgmFadeOutSeconds => Mathf.Max(0f, bgmFadeOutSeconds);

        public bool TryGetEntry(string key, out AudioCatalogEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            EnsureLookup();
            return lookup.TryGetValue(key.Trim(), out entry);
        }

        public List<string> GetDuplicateKeys()
        {
            var duplicates = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < entries.Count; i++)
            {
                AudioCatalogEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    continue;

                string normalizedKey = entry.key.Trim();
                if (!seen.Add(normalizedKey) && !duplicates.Contains(normalizedKey))
                    duplicates.Add(normalizedKey);
            }

            duplicates.Sort(StringComparer.OrdinalIgnoreCase);
            return duplicates;
        }

        public void MarkLookupDirty()
        {
            lookupDirty = true;
        }

        private void OnEnable()
        {
            NormalizeRuntimeDefaults();
            lookupDirty = true;
        }

        private void OnValidate()
        {
            NormalizeRuntimeDefaults();

            for (int i = 0; i < entries.Count; i++)
            {
                entries[i]?.Normalize();
            }

            lookupDirty = true;
        }

        private void NormalizeRuntimeDefaults()
        {
            globalVolumeMultiplier = Mathf.Max(0f, globalVolumeMultiplier);
            bgmFadeInSeconds = Mathf.Max(0f, bgmFadeInSeconds);
            bgmFadeOutSeconds = Mathf.Max(0f, bgmFadeOutSeconds);
        }

        private void EnsureLookup()
        {
            if (!lookupDirty)
                return;

            lookup.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                AudioCatalogEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    continue;

                entry.Normalize();
                lookup[entry.key] = entry;
            }

            lookupDirty = false;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CapstonePresentation
{
    [Serializable]
    public sealed class CueCatalogEntry
    {
        public string key;
        public PresentationCueSO cue;

        public void Normalize()
        {
            if (string.IsNullOrWhiteSpace(key))
                key = string.Empty;
            else
                key = key.Trim().ToLowerInvariant();
        }
    }

    [CreateAssetMenu(fileName = "CueCatalog", menuName = "Presentation/Cue Catalog")]
    public sealed class CueCatalogSO : ScriptableObject
    {
        [SerializeField] private List<CueCatalogEntry> entries = new();

        private readonly Dictionary<string, PresentationCueSO> lookup =
            new(StringComparer.OrdinalIgnoreCase);

        private bool lookupDirty = true;

        public IReadOnlyList<CueCatalogEntry> Entries => entries;

        public bool TryGetCue(string key, out PresentationCueSO cue)
        {
            cue = null;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            EnsureLookup();
            return lookup.TryGetValue(key.Trim(), out cue);
        }

        public bool TryGetPresentation(string key, out WorldPresentationHook presentation)
        {
            presentation = default;
            if (!TryGetCue(key, out PresentationCueSO cue) || cue == null || !cue.HasAnyContent)
                return false;

            presentation = cue.Presentation;
            return true;
        }

        public List<string> GetDuplicateKeys()
        {
            var duplicates = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < entries.Count; i++)
            {
                CueCatalogEntry entry = entries[i];
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
            lookupDirty = true;
        }

        private void OnValidate()
        {
            for (int i = 0; i < entries.Count; i++)
                entries[i]?.Normalize();

            lookupDirty = true;
        }

        private void EnsureLookup()
        {
            if (!lookupDirty)
                return;

            lookup.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                CueCatalogEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key) || entry.cue == null)
                    continue;

                entry.Normalize();
                lookup[entry.key] = entry.cue;
            }

            lookupDirty = false;
        }
    }
}

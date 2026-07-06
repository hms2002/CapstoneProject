using System;
using System.Collections.Generic;
using UnityEngine;

namespace CapstonePresentation
{
    /// <summary>
    /// 책임: catalog key와 presentation cue 에셋 참조의 직렬화된 한 행을 보관한다.
    /// </summary>
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

    /// <summary>
    /// 책임: presentation cue key를 실제 PresentationCueSO 에셋으로 해석하는 catalog 데이터를 보관한다.
    /// </summary>
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

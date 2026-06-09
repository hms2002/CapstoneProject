#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace CapstonePresentation.EditorTools
{
    internal static class CueCatalogEditorUtility
    {
        private static readonly List<CueCatalogSO> cachedCatalogs = new();
        private static readonly List<string> cachedKeys = new();
        private static readonly Dictionary<string, CueCatalogSO> ownerByKey =
            new(StringComparer.OrdinalIgnoreCase);

        private static double lastRefreshTime = -1d;

        static CueCatalogEditorUtility()
        {
            EditorApplication.projectChanged += InvalidateCache;
        }

        public static IReadOnlyList<CueCatalogSO> FindCatalogs(bool forceRefresh = false)
        {
            EnsureCache(forceRefresh);
            return cachedCatalogs;
        }

        public static IReadOnlyList<string> GetAllKeys(bool forceRefresh = false)
        {
            EnsureCache(forceRefresh);
            return cachedKeys;
        }

        public static bool KeyExists(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            EnsureCache(false);
            return ownerByKey.ContainsKey(key.Trim());
        }

        public static CueCatalogSO FindOwningCatalog(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            EnsureCache(false);
            ownerByKey.TryGetValue(key.Trim(), out CueCatalogSO catalog);
            return catalog;
        }

        public static void InvalidateCache()
        {
            cachedCatalogs.Clear();
            cachedKeys.Clear();
            ownerByKey.Clear();
            lastRefreshTime = -1d;
        }

        private static void EnsureCache(bool forceRefresh)
        {
            double now = EditorApplication.timeSinceStartup;
            if (!forceRefresh && lastRefreshTime >= 0d && (now - lastRefreshTime) < 0.75d)
                return;

            RefreshCache();
        }

        private static void RefreshCache()
        {
            cachedCatalogs.Clear();
            cachedKeys.Clear();
            ownerByKey.Clear();

            string[] guids = AssetDatabase.FindAssets("t:CueCatalogSO");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                CueCatalogSO catalog = AssetDatabase.LoadAssetAtPath<CueCatalogSO>(path);
                if (catalog == null)
                    continue;

                cachedCatalogs.Add(catalog);

                IReadOnlyList<CueCatalogEntry> entries = catalog.Entries;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    CueCatalogEntry entry = entries[entryIndex];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                        continue;

                    string normalizedKey = entry.key.Trim();
                    if (!ownerByKey.ContainsKey(normalizedKey))
                        ownerByKey[normalizedKey] = catalog;
                }
            }

            cachedKeys.AddRange(ownerByKey.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
            lastRefreshTime = EditorApplication.timeSinceStartup;
        }
    }
}
#endif

using System.Collections.Generic;
using CapstonePresentation;
using UnityEngine;

[System.Serializable]
public struct PrewarmPrefabEntry
{
    public GameObject prefab;
    [Min(1)] public int count;

    public bool IsValid => prefab != null && count > 0;
    public int EffectiveCount => Mathf.Max(1, count);
}

[CreateAssetMenu(
    fileName = "LoadManifest",
    menuName = "Capstone/Loading/Load Manifest")]
public sealed class LoadManifestSO : ScriptableObject
{
    [SerializeField] private LoadScopeKind scopeKind = LoadScopeKind.RouteSet;

    [Header("Prefabs")]
    [SerializeField] private List<GameObject> prefabAssets = new();

    [Header("Cue Assets")]
    [SerializeField] private List<PresentationCueSO> cueAssets = new();

    [Header("Data Assets")]
    [SerializeField] private List<ScriptableObject> dataAssets = new();

    [Header("Extra Assets")]
    [SerializeField] private List<Object> extraAssets = new();

    [Header("Optional Prewarm")]
    [SerializeField] private List<PrewarmPrefabEntry> prewarmPrefabs = new();

    public LoadScopeKind ScopeKind => scopeKind;
    public IReadOnlyList<GameObject> PrefabAssets => prefabAssets;
    public IReadOnlyList<PresentationCueSO> CueAssets => cueAssets;
    public IReadOnlyList<ScriptableObject> DataAssets => dataAssets;
    public IReadOnlyList<Object> ExtraAssets => extraAssets;
    public IReadOnlyList<PrewarmPrefabEntry> PrewarmPrefabs => prewarmPrefabs;

    public bool HasAnyReferences =>
        HasAny(prefabAssets) ||
        HasAny(cueAssets) ||
        HasAny(dataAssets) ||
        HasAny(extraAssets) ||
        HasAnyPrewarm(prewarmPrefabs);

    public IEnumerable<Object> EnumerateReferencedAssets()
    {
        var seen = new HashSet<int>();

        foreach (GameObject prefab in prefabAssets)
        {
            if (prefab == null || !seen.Add(prefab.GetInstanceID()))
                continue;

            yield return prefab;
        }

        foreach (PresentationCueSO cue in cueAssets)
        {
            if (cue == null || !seen.Add(cue.GetInstanceID()))
                continue;

            yield return cue;
        }

        foreach (ScriptableObject dataAsset in dataAssets)
        {
            if (dataAsset == null || !seen.Add(dataAsset.GetInstanceID()))
                continue;

            yield return dataAsset;
        }

        foreach (Object extraAsset in extraAssets)
        {
            if (extraAsset == null || !seen.Add(extraAsset.GetInstanceID()))
                continue;

            yield return extraAsset;
        }

        foreach (PrewarmPrefabEntry prewarmEntry in prewarmPrefabs)
        {
            if (!prewarmEntry.IsValid || !seen.Add(prewarmEntry.prefab.GetInstanceID()))
                continue;

            yield return prewarmEntry.prefab;
        }
    }

    public IEnumerable<PrewarmPrefabEntry> EnumeratePrewarmEntries()
    {
        if (prewarmPrefabs == null)
            yield break;

        for (int i = 0; i < prewarmPrefabs.Count; i++)
        {
            PrewarmPrefabEntry entry = prewarmPrefabs[i];
            if (!entry.IsValid)
                continue;

            yield return entry;
        }
    }

    private static bool HasAny<T>(List<T> assets) where T : Object
    {
        if (assets == null || assets.Count == 0)
            return false;

        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i] != null)
                return true;
        }

        return false;
    }

    private static bool HasAnyPrewarm(List<PrewarmPrefabEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].IsValid)
                return true;
        }

        return false;
    }
}

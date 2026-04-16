using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RouteSetLoadManifest",
    menuName = "Capstone/Loading/Route Set Load Manifest")]
public sealed class RouteSetLoadManifestSO : ScriptableObject
{
    [Header("Shared Across Corridor + Boss")]
    [SerializeField] private LoadManifestSO sharedManifest;

    [Header("Corridor")]
    [SerializeField] private LoadManifestSO corridorManifest;

    [Header("Boss")]
    [SerializeField] private LoadManifestSO bossManifest;

    public LoadManifestSO SharedManifest => sharedManifest;
    public LoadManifestSO CorridorManifest => corridorManifest;
    public LoadManifestSO BossManifest => bossManifest;

    public bool HasAnyReferences =>
        (sharedManifest != null && sharedManifest.HasAnyReferences) ||
        (corridorManifest != null && corridorManifest.HasAnyReferences) ||
        (bossManifest != null && bossManifest.HasAnyReferences);

    public IEnumerable<LoadManifestSO> EnumerateManifests(
        bool includeShared = true,
        bool includeCorridor = true,
        bool includeBoss = true)
    {
        if (includeShared && sharedManifest != null)
            yield return sharedManifest;

        if (includeCorridor && corridorManifest != null)
            yield return corridorManifest;

        if (includeBoss && bossManifest != null)
            yield return bossManifest;
    }

    public IEnumerable<Object> EnumerateReferencedAssets(
        bool includeShared = true,
        bool includeCorridor = true,
        bool includeBoss = true)
    {
        var seen = new HashSet<int>();

        foreach (LoadManifestSO manifest in EnumerateManifests(includeShared, includeCorridor, includeBoss))
        {
            if (manifest == null)
                continue;

            foreach (Object asset in manifest.EnumerateReferencedAssets())
            {
                if (asset == null || !seen.Add(asset.GetInstanceID()))
                    continue;

                yield return asset;
            }
        }
    }
}

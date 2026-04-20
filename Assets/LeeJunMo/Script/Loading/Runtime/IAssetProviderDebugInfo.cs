public interface IAssetProviderDebugInfo
{
    int LoadedManifestCount { get; }
    int LoadedRouteManifestCount { get; }
    int RetainedAssetCount { get; }
    int PrewarmedPrefabCount { get; }

    PresentationAssetProvider.DebugCountEntry[] GetManifestSnapshot();
    PresentationAssetProvider.DebugCountEntry[] GetRouteManifestSnapshot();
    PresentationAssetProvider.DebugCountEntry[] GetAssetSnapshot(int maxCount);
    PresentationAssetProvider.DebugCountEntry[] GetPrewarmSnapshot(int maxCount);
    PresentationAssetProvider.DebugEventEntry[] GetDebugHistorySnapshot(int maxCount);
}

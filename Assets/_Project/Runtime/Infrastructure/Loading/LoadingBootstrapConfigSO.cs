using UnityEngine;

[CreateAssetMenu(
    fileName = "LoadingBootstrapConfig",
    menuName = "Capstone/Loading/Bootstrap Config")]
public sealed class LoadingBootstrapConfigSO : ScriptableObject
{
    public const string SourceAssetPath = "Assets/_Project/Data/SceneFlow/LoadingManifests/LoadingBootstrapConfig.asset";

    [Header("Primary Loading")]
    [SerializeField] private LoadManifestSO bootManifest;

    [Header("Optional Async Backend")]
    [SerializeField] private LoadingAssetProviderMode assetProviderMode = LoadingAssetProviderMode.DirectReference;
    [SerializeField] private LoadingAddressableRegistrySO addressableRegistry;

    public LoadManifestSO BootManifest => bootManifest;
    public LoadingAssetProviderMode AssetProviderMode => assetProviderMode;
    public LoadingAddressableRegistrySO AddressableRegistry => addressableRegistry;
}

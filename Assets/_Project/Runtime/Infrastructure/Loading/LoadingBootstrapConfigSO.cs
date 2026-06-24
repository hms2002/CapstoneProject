using UnityEngine;

[CreateAssetMenu(
    fileName = "LoadingBootstrapConfig",
    menuName = "Capstone/Loading/Bootstrap Config")]
public sealed class LoadingBootstrapConfigSO : ScriptableObject
{
    public const string SourceAssetPath = "Assets/_Project/Data/SceneFlow/LoadingManifests/LoadingBootstrapConfig.asset";

    [Header("Primary Loading")]
    [SerializeField] private LoadManifestSO bootManifest;

    [Header("One-Time Profile Intro")]
    [SerializeField] private LoadManifestSO firstRunIntroManifest;
    [SerializeField] private string firstRunIntroCompletionTutorialId = "hub_intro_after_darklord_seen";

    [Header("Optional Async Backend")]
    [SerializeField] private LoadingAssetProviderMode assetProviderMode = LoadingAssetProviderMode.DirectReference;
    [SerializeField] private LoadingAddressableRegistrySO addressableRegistry;

    public LoadManifestSO BootManifest => bootManifest;
    public LoadManifestSO FirstRunIntroManifest => firstRunIntroManifest;
    public string FirstRunIntroCompletionTutorialId =>
        string.IsNullOrWhiteSpace(firstRunIntroCompletionTutorialId)
            ? "hub_intro_after_darklord_seen"
            : firstRunIntroCompletionTutorialId.Trim();
    public LoadingAssetProviderMode AssetProviderMode => assetProviderMode;
    public LoadingAddressableRegistrySO AddressableRegistry => addressableRegistry;
}

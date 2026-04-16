using UnityEngine;

[CreateAssetMenu(
    fileName = "LoadingBootstrapConfig",
    menuName = "Capstone/Loading/Bootstrap Config")]
public sealed class LoadingBootstrapConfigSO : ScriptableObject
{
    public const string DefaultResourcesPath = "Loading/LoadingBootstrapConfig";

    [SerializeField] private LoadManifestSO bootManifest;

    public LoadManifestSO BootManifest => bootManifest;
}

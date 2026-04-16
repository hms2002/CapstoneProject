using UnityEngine;

[CreateAssetMenu(
    fileName = "LoadingBootstrapConfig",
    menuName = "Capstone/Loading/Bootstrap Config")]
public sealed class LoadingBootstrapConfigSO : ScriptableObject
{
    public const string SourceAssetPath = "Assets/LeeJunMo/Datas/Loading/LoadingBootstrapConfig.asset";

    [SerializeField] private LoadManifestSO bootManifest;

    public LoadManifestSO BootManifest => bootManifest;
}

using UnityEngine;

public sealed class PlayerSpawnPoint : MonoBehaviour
{
    public string pointId;
    public bool isDefault;
    public PlayerSpawnRuntimePolicy runtimePolicy = PlayerSpawnRuntimePolicy.RestorePendingState;
}

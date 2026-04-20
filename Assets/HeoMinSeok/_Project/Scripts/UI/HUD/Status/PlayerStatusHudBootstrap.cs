using UnityEngine;

/// <summary>
/// 책임 :
/// - 현재 플레이어 등록 상태를 감시해 PlayerStatusRuntime과 PlayerStatusHudSource를 자동으로 부착한다.
/// - 지역 디버프나 유물 버프 같은 일반 상태 UI가 씬/프리팹 수작업 없이도 플레이어 등록만으로 동작하게 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStatusHudBootstrap : MonoBehaviour
{
    private static PlayerStatusHudBootstrap instance;

    public static PlayerStatusHudBootstrap EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<PlayerStatusHudBootstrap>();
        if (instance != null)
            return instance;

        GameObject root = new("PlayerStatusHudBootstrap");
        instance = root.AddComponent<PlayerStatusHudBootstrap>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        GlobalUIRoot.AdoptService(transform);
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        EnsureCurrentPlayerSource();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        EnsureSource(player);
    }

    private void EnsureCurrentPlayerSource()
    {
        PlayerInteractor2D player = PlayerRuntimeRegistry.CurrentPlayer ?? PlayerInteractor2D.Instance;
        EnsureSource(player);
    }

    private static void EnsureSource(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        PlayerStatusRuntime runtime = PlayerStatusRuntime.GetOrAdd(player.gameObject);
        if (runtime == null)
            return;

        PlayerStatusHudSource.GetOrAdd(runtime.gameObject);
    }
}

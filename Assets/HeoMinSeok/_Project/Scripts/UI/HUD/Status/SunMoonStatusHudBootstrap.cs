using UnityEngine;

/// <summary>
/// 책임 :
/// - 현재 플레이어 등록 상태를 감시해 SunMoonStatusHudSource를 플레이어 인벤토리에 자동 부착한다.
/// - 태양도/월영도 상태 HUD가 씬/프리팹 수작업 없이도 플레이어 등록만으로 동작하게 만드는 부트스트랩 계층이다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SunMoonStatusHudBootstrap : MonoBehaviour
{
    private static SunMoonStatusHudBootstrap instance;

    public static SunMoonStatusHudBootstrap EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<SunMoonStatusHudBootstrap>();
        if (instance != null)
            return instance;

        GameObject root = new("SunMoonStatusHudBootstrap");
        instance = root.AddComponent<SunMoonStatusHudBootstrap>();
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

        WeaponInventory2D inventory = player.GetComponent<WeaponInventory2D>();
        if (inventory == null)
            return;

        SunMoonStatusHudSource.GetOrAdd(inventory.gameObject);
    }
}

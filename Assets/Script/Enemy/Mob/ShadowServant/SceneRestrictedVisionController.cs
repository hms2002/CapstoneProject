using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(GlobalVisionMaskController))]
public sealed class SceneRestrictedVisionController : MonoBehaviour
{
    // 이 클래스의 책임:
    // - 현재 씬이 제공하는 상시 시야 제한 상태를 플레이어 등록 시점에 적용한다.
    // - 전역 시야 마스크 컨트롤러와 플레이어 상태 허브를 함께 갱신한다.
    // - 플레이어 교체, 씬 종료, 비활성화 시 자신이 건 상태만 안전하게 회수한다.

    [SerializeField] private StatusHudDefinition restrictedVisionDefinition;
    [SerializeField] private bool logFlow = true;
    [SerializeField] private GlobalVisionMaskController visionMaskController;

    private PlayerInteractor2D currentPlayer;
    private PlayerStatusRuntime currentRuntime;
    private StatusHandle activeStatusHandle;
    private string ownerKey;

    public StatusHudDefinition RestrictedVisionDefinition => restrictedVisionDefinition;

    private void Awake()
    {
        EnsureController();
        ownerKey = $"scene.restrictedVision.{gameObject.scene.name}.{GetInstanceID()}";
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;

        if (PlayerRuntimeRegistry.CurrentPlayer != null)
            ApplyToPlayer(PlayerRuntimeRegistry.CurrentPlayer);
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
        ClearCurrentPlayerState();
    }

    private void HandlePlayerRegistered(PlayerInteractor2D player)
    {
        ApplyToPlayer(player);
    }

    private void HandlePlayerUnregistered(PlayerInteractor2D player)
    {
        if (player == null || currentPlayer != player)
            return;

        ClearCurrentPlayerState();
    }

    private void ApplyToPlayer(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        if (currentPlayer == player && activeStatusHandle.IsValid)
            return;

        ClearCurrentPlayerState();

        EnsureController();
        currentPlayer = player;
        currentRuntime = PlayerStatusRuntime.GetOrAdd(player.gameObject);

        visionMaskController?.AttachToPlayer(player.transform);

        if (restrictedVisionDefinition == null)
        {
            if (logFlow)
                Debug.LogWarning("[SceneRestrictedVisionController] Restricted vision definition was missing.", this);
            return;
        }

        StatusApplyRequest request = new(
            restrictedVisionDefinition,
            ownerKey,
            isVisible: true,
            showStacksOverride: false,
            showDurationOverride: false);

        activeStatusHandle = currentRuntime.Apply(request);

        if (logFlow)
        {
            Debug.Log(
                $"[SceneRestrictedVisionController] Applied restricted vision to '{player.name}'. " +
                $"statusId={restrictedVisionDefinition.StatusId}, handle={activeStatusHandle.RuntimeId}",
                this);
        }
    }

    private void ClearCurrentPlayerState()
    {
        if (activeStatusHandle.IsValid)
        {
            if (logFlow)
            {
                Debug.Log(
                    $"[SceneRestrictedVisionController] Releasing restricted vision handle {activeStatusHandle.RuntimeId} " +
                    $"from '{(currentPlayer != null ? currentPlayer.name : "null")}'.",
                    this);
            }

            activeStatusHandle.Release();
            activeStatusHandle = default;
        }

        currentRuntime = null;
        currentPlayer = null;
    }

    private void EnsureController()
    {
        if (visionMaskController != null)
            return;

        visionMaskController = GetComponent<GlobalVisionMaskController>();
        if (visionMaskController == null)
            visionMaskController = GlobalVisionMaskController.Instance;

        if (visionMaskController == null)
            visionMaskController = FindFirstObjectByType<GlobalVisionMaskController>();
    }
}

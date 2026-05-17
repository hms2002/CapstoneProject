using UnityEngine;
using CapstoneAudio;

/// <summary>
/// 책임 : 플레이어의 장비 인벤토리와 월드 loot를 InventoryScreen에 바인딩하고 열기/닫기를 제어한다.
/// </summary>
public class InventoryUIManager : MonoBehaviour
{
    /// <summary>
    /// 책임 :
    /// - 인벤토리 열기 시 재생할 UI 사운드 키를 한 곳에 고정한다.
    /// - 호출부가 문자열 리터럴을 반복하지 않게 해 authoring 변경을 쉽게 만든다.
    /// </summary>
    private static readonly SoundRef OpenInventorySound = SoundRef.FromKey("ui.inventory.open");

    public static InventoryUIManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private InventoryScreen inventoryScreen;

    [Header("(Optional) Player reference")]
    [Tooltip("If null, uses the current PlayerRuntimeRegistry player as the loot origin.")]
    [SerializeField] private Transform lootOriginOverride;

    public bool CanOpen =>
        inventoryScreen != null &&
        !IsInputBlockedByLoadingOrTransition() &&
        CanOpenThroughUiManager() &&
        HasCurrentPlayerInventoryContext();
    public bool IsOpen => inventoryScreen != null && inventoryScreen.IsActive;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // 이제 SetActive 대신 UIManager가 알아서 처리하겠지만, 시작 시 꺼두는 건 뷰에서 담당하거나 여기서 안전하게 꺼둡니다.
        if (inventoryScreen != null && inventoryScreen.gameObject.activeSelf)
        {
            inventoryScreen.gameObject.SetActive(false);
        }

    }

    private void Update()
    {
        if (InputBindingService.EnsureInstance().WasPressedThisFrame(InputActionId.InventoryToggle))
        {
            if (UIManager.Instance != null && UIManager.Instance.IsExternalUiInputBlocked)
                return;

            // [수정] 인터페이스 프로퍼티(IsActive)를 통해 상태 확인
            if (inventoryScreen != null && inventoryScreen.IsActive)
                Close();
            else
                TryOpen();
        }
    }

    public void Open()
    {
        if (inventoryScreen == null) return;

        if (!TryResolveCurrentPlayerInventories(
                out PlayerInteractor2D currentPlayer,
                out PlayerConsumableInventory consumableInv,
                out WeaponInventory2D weaponInv,
                out RelicInventory relicInv,
                out Transform playerRoot,
                out Transform lootOrigin))
        {
            inventoryScreen.CancelPreparedOpen();
            return;
        }

        inventoryScreen.Bind(consumableInv, weaponInv, relicInv, lootOrigin, playerRoot);

        if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();

        // [핵심] 직접 켜지 않고 UIManager의 스택에 밀어넣음!
        bool opened = false;
        if (UIManager.Instance != null)
            opened = UIManager.Instance.TryPushUI(inventoryScreen);
        else
        {
            inventoryScreen.OpenUI();
            opened = true;
        }

        if (!opened)
            inventoryScreen.CancelPreparedOpen();

        if (opened)
            PlayOpenInventorySound(currentPlayer != null ? currentPlayer.gameObject : null);
    }

    /// <summary>
    /// 책임 :
    /// - 외부 HUD/버튼 계층이 중복 열림을 직접 판단하지 않고 안전하게 인벤토리 열기를 요청할 수 있게 한다.
    /// - 이미 열린 상태, 참조 누락, UI push 실패를 하나의 bool 결과로 감싼다.
    /// </summary>
    public bool TryOpen()
    {
        if (IsOpen)
            return false;

        if (!CanOpen)
            return false;

        Open();
        return IsOpen;
    }

    public void Close()
    {
        if (inventoryScreen == null) return;

        if (inventoryScreen is ICloseRequestHandler closeHandler && closeHandler.TryHandleCloseRequest())
            return;

        // [핵심] 직접 끄지 않고 UIManager의 스택에서 빼달라고 요청!
        if (UIManager.Instance != null) UIManager.Instance.PopUI(inventoryScreen);
        else inventoryScreen.CloseUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private bool TryResolveCurrentPlayerInventories(
        out PlayerInteractor2D currentPlayer,
        out PlayerConsumableInventory consumableInventory,
        out WeaponInventory2D weaponInventory,
        out RelicInventory relicInventory,
        out Transform playerRoot,
        out Transform lootOrigin)
    {
        currentPlayer = PlayerRuntimeRegistry.CurrentPlayer;
        consumableInventory = null;
        weaponInventory = null;
        relicInventory = null;
        playerRoot = currentPlayer != null ? currentPlayer.transform : null;
        lootOrigin = lootOriginOverride != null ? lootOriginOverride : playerRoot;

        if (currentPlayer == null)
        {
            Debug.LogWarning("[InventoryUIManager] Cannot open inventory because PlayerRuntimeRegistry.CurrentPlayer is missing.", this);
            return false;
        }

        consumableInventory = currentPlayer.GetComponent<PlayerConsumableInventory>();
        weaponInventory = currentPlayer.GetComponent<WeaponInventory2D>();
        relicInventory = currentPlayer.GetComponent<RelicInventory>();

        if (consumableInventory != null && weaponInventory != null && relicInventory != null)
            return true;

        Debug.LogWarning(
            "[InventoryUIManager] Cannot open inventory because the current player is missing one or more inventory components.",
            currentPlayer);
        return false;
    }

    /// <summary>
    /// 책임 :
    /// - HUD 안내 버튼이 경고 로그 없이 인벤토리 열기 가능 여부만 확인할 수 있게 한다.
    /// - 플레이어 생성/해제 타이밍에 따라 버튼 표시 상태가 자연스럽게 따라가도록 현재 플레이어 문맥을 얕게 검사한다.
    /// </summary>
    private bool HasCurrentPlayerInventoryContext()
    {
        PlayerInteractor2D currentPlayer = PlayerRuntimeRegistry.CurrentPlayer;
        if (currentPlayer == null)
            return false;

        return currentPlayer.GetComponent<PlayerConsumableInventory>() != null
            && currentPlayer.GetComponent<WeaponInventory2D>() != null
            && currentPlayer.GetComponent<RelicInventory>() != null;
    }

    private bool CanOpenThroughUiManager()
    {
        return UIManager.Instance == null || UIManager.Instance.CanOpenUI(inventoryScreen);
    }

    /// <summary>
    /// 책임 :
    /// - 인벤토리 단축키와 HUD 버튼이 씬 전환/로딩 중 같은 기준으로 열림을 거부하게 한다.
    /// - 전환 중 UI 스택이 흔들리거나 플레이어 문맥이 사라진 상태에서 인벤토리가 열리는 것을 방지한다.
    /// </summary>
    private static bool IsInputBlockedByLoadingOrTransition()
    {
        if (SceneTransitionCoordinator.Instance != null &&
            SceneTransitionCoordinator.Instance.IsTransitionActive)
        {
            return true;
        }

        return LoadingOverlayController.Instance != null &&
               LoadingOverlayController.Instance.IsActiveLoadingPresentation;
    }

    /// <summary>
    /// 책임 :
    /// - 인벤토리 UI가 실제로 열렸을 때만 오픈 사운드를 1회 재생한다.
    /// - UI push 실패나 중복 열림 상황에서 불필요한 사운드 중첩을 막는다.
    /// </summary>
    private static void PlayOpenInventorySound(GameObject playerObject)
    {
        SoundManager.EnsureInstance().Play(OpenInventorySound, new SoundPlaybackContext
        {
            Instigator = playerObject,
            Causer = playerObject,
            Target = playerObject,
            Position = playerObject != null ? playerObject.transform.position : Vector3.zero,
            SourceObject = playerObject
        });
    }

}

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
    [Tooltip("If null, will fallback to PlayerInteractor2D.Instance")]
    [SerializeField] private Transform lootOriginOverride;

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
            // [수정] 인터페이스 프로퍼티(IsActive)를 통해 상태 확인
            if (inventoryScreen != null && inventoryScreen.IsActive)
                Close();
            else
                Open();
        }
    }

    public void Open()
    {
        if (inventoryScreen == null) return;

        var playerTransform = lootOriginOverride != null
            ? lootOriginOverride
            : PlayerRuntimeRegistry.GetPlayerTransform();

        var currentPlayer = PlayerRuntimeRegistry.CurrentPlayer != null
            ? PlayerRuntimeRegistry.CurrentPlayer
            : PlayerInteractor2D.Instance;

        var consumableInv = currentPlayer != null ? PlayerConsumableInventory.GetOrAdd(currentPlayer.transform) : FindFirstObjectByType<PlayerConsumableInventory>();
        var weaponInv = currentPlayer != null ? currentPlayer.GetComponent<WeaponInventory2D>() : FindFirstObjectByType<WeaponInventory2D>();
        var relicInv = currentPlayer != null ? currentPlayer.GetComponent<RelicInventory>() : FindFirstObjectByType<RelicInventory>();
        inventoryScreen.Bind(consumableInv, weaponInv, relicInv, playerTransform, currentPlayer != null ? currentPlayer.transform : null);

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

        if (opened)
            PlayOpenInventorySound(currentPlayer != null ? currentPlayer.gameObject : null);
    }

    public void Close()
    {
        if (inventoryScreen == null) return;

        // [핵심] 직접 끄지 않고 UIManager의 스택에서 빼달라고 요청!
        if (UIManager.Instance != null) UIManager.Instance.PopUI(inventoryScreen);
        else inventoryScreen.CloseUI();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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

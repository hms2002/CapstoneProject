using UnityEngine;

/// <summary>
/// 책임 : 플레이어의 장비 인벤토리와 월드 loot를 InventoryScreen에 바인딩하고 열기/닫기를 제어한다.
/// </summary>
public class InventoryUIManager : MonoBehaviour
{
    public static InventoryUIManager Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private InventoryScreen inventoryScreen;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;

    [Header("(Optional) Player reference")]
    [Tooltip("If null, will fallback to SampleTopDownPlayer.Instance")]
    [SerializeField] private Transform lootOriginOverride;

    private void Awake()
    {
        Instance = this;
        // 이제 SetActive 대신 UIManager가 알아서 처리하겠지만, 시작 시 꺼두는 건 뷰에서 담당하거나 여기서 안전하게 꺼둡니다.
        if (inventoryScreen != null && inventoryScreen.gameObject.activeSelf)
        {
            inventoryScreen.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
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
            : SampleTopDownPlayer.Instance;

        var weaponInv = currentPlayer != null ? currentPlayer.GetComponent<WeaponInventory2D>() : FindFirstObjectByType<WeaponInventory2D>();
        var relicInv = currentPlayer != null ? currentPlayer.GetComponent<RelicInventory>() : FindFirstObjectByType<RelicInventory>();
        inventoryScreen.Bind(weaponInv, relicInv, playerTransform);

        if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();

        // [핵심] 직접 켜지 않고 UIManager의 스택에 밀어넣음!
        if (UIManager.Instance != null) UIManager.Instance.PushUI(inventoryScreen);
        else inventoryScreen.OpenUI();
    }

    public void Close()
    {
        if (inventoryScreen == null) return;

        // [핵심] 직접 끄지 않고 UIManager의 스택에서 빼달라고 요청!
        if (UIManager.Instance != null) UIManager.Instance.PopUI(inventoryScreen);
        else inventoryScreen.CloseUI();
    }
}

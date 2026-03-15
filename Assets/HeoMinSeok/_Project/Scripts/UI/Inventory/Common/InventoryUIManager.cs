using UnityEngine;

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

        var player = lootOriginOverride != null
            ? lootOriginOverride
            : (SampleTopDownPlayer.Instance != null ? SampleTopDownPlayer.Instance.transform : null);
        var weaponInv = FindFirstObjectByType<WeaponInventory2D>();
        var relicInv = FindFirstObjectByType<RelicInventory>();
        var backpack = FindFirstObjectByType<PlayerBackpackInventory>();

        inventoryScreen.Bind(backpack, weaponInv, relicInv, player);

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
using UnityEngine;

/// <summary>
/// 이 클래스의 책임:
/// InventoryUIManager를 IInventoryOpenRequestHandler로 감싸 HUD 버튼과 인벤토리 UI 구현 사이의 직접 의존을 끊는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class InventoryUIOpenRequestHandler : MonoBehaviour, IInventoryOpenRequestHandler
{
    [SerializeField] private InventoryUIManager inventoryUIManager;

    public bool CanOpenInventory
    {
        get
        {
            InventoryUIManager manager = ResolveManager();
            return manager != null
                && manager.CanOpen
                && !manager.IsOpen;
        }
    }

    public bool IsInventoryOpen => ResolveManager() != null && ResolveManager().IsOpen;

    public bool TryOpenInventory()
    {
        InventoryUIManager manager = ResolveManager();
        if (manager == null || manager.IsOpen)
            return false;

        return manager.TryOpen();
    }

    private InventoryUIManager ResolveManager()
    {
        if (inventoryUIManager != null)
            return inventoryUIManager;

        inventoryUIManager = InventoryUIManager.Instance;
        if (inventoryUIManager != null)
            return inventoryUIManager;

#if UNITY_2023_1_OR_NEWER
        inventoryUIManager = FindAnyObjectByType<InventoryUIManager>(FindObjectsInactive.Include);
#else
        inventoryUIManager = FindObjectOfType<InventoryUIManager>(true);
#endif
        return inventoryUIManager;
    }

}

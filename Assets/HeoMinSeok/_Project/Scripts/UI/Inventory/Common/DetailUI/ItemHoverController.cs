using UnityEngine;

public class ItemHoverController : MonoBehaviour
{
    public static ItemHoverController Instance { get; private set; }

    [Header("View Reference")]
    [SerializeField] private ItemDetailPanel detailPanel;

    [Header("Context Provider")]
    [SerializeField] private PlayerDetailContextProvider contextProviderBehaviour;
    private IItemDetailContextProvider _contextProvider;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (detailPanel == null) detailPanel = FindFirstObjectByType<ItemDetailPanel>();
        _contextProvider = contextProviderBehaviour as IItemDetailContextProvider;
    }

    // 슬롯에서 마우스 엔터 시 호출됨
    public void HoverSlot(RectTransform slotRect, ScriptableObject itemDef, IItemContainer container = null, int index = -1)
    {
        if (itemDef == null)
        {
            if (UIManager.Instance != null) UIManager.Instance.HideHoverImmediate();
            return;
        }

        // 1. 컨텍스트 데이터 조립 (비즈니스 로직)
        var ctx = _contextProvider != null ? _contextProvider.BuildContext() : null;
        if (ctx != null)
        {
            ctx.sourceContainer = container;
            ctx.sourceIndex = index;

            if (itemDef is RelicDefinition && container is IRelicLevelProvider p && index >= 0)
            {
                if (p.TryGetRelicLevel(index, out var lvl))
                    ctx.relicLevelOverride = lvl;
            }
        }

        // 2. UIManager에게 "이 데이터로 Hover UI 띄워줘!" 지시
        if (UIManager.Instance != null && detailPanel != null)
        {
            UIManager.Instance.ShowHover(detailPanel, slotRect, itemDef, ctx);
        }
    }

    // 슬롯에서 마우스 엑시트 시 호출됨
    public void UnhoverSlot(RectTransform slotRect)
    {
        // [수정] UIManager에게 "이 슬롯(slotRect)에서 마우스가 나갔으니 확인해 줘!" 라고 지시
        if (UIManager.Instance != null && detailPanel != null)
        {
            UIManager.Instance.HideHover(detailPanel, slotRect);
        }
    }
}
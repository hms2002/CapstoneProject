using UnityEngine;

public class ItemHoverController : MonoBehaviour
{
    public static ItemHoverController Instance { get; private set; }

    [Header("View Reference")]
    [SerializeField] private ItemDetailPanel detailPanel;

    [Header("Context Provider")]
    [SerializeField] private PlayerDetailContextProvider contextProviderBehaviour;
    private IItemDetailContextProvider _contextProvider;
    private RectTransform currentSlotRect;
    private IItemContainer currentSourceContainer;
    private int currentSourceIndex = -1;
    private ItemDetailContext currentContext;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (detailPanel == null)
            detailPanel = FindDetailPanelInLoadedScenes();

        _contextProvider = contextProviderBehaviour as IItemDetailContextProvider;
    }

    private void Update()
    {
        HandleFixedInventoryDropInput();
    }

    public void HoverSlot(RectTransform slotRect, ScriptableObject itemDef, IItemContainer container = null, int index = -1)
    {
        if (itemDef == null)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.HideHoverImmediate();
            ClearCurrentHover();
            return;
        }

        if (detailPanel == null)
            detailPanel = FindDetailPanelInLoadedScenes();

        var ctx = BuildContext();
        ctx.sourceContainer = container;
        ctx.sourceIndex = index;

        if (itemDef is RelicDefinition && container is IRelicLevelProvider relicLevelProvider && index >= 0)
        {
            if (relicLevelProvider.TryGetRelicLevel(index, out var level))
                ctx.relicLevelOverride = level;
        }

        currentSlotRect = slotRect;
        currentSourceContainer = container;
        currentSourceIndex = index;
        currentContext = ctx;

        if (UIManager.Instance != null && detailPanel != null)
            UIManager.Instance.ShowHover(detailPanel, slotRect, itemDef, ctx);
    }

    public void HoverWorldTarget(RectTransform targetRect, ScriptableObject itemDef, int relicLevelOverride = 0)
    {
        if (itemDef == null)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.HideHoverImmediate();
            ClearCurrentHover();
            return;
        }

        if (detailPanel == null)
            detailPanel = FindDetailPanelInLoadedScenes();

        ItemDetailContext ctx = BuildContext();
        if (itemDef is RelicDefinition && relicLevelOverride > 0)
            ctx.relicLevelOverride = relicLevelOverride;

        ClearCurrentHover();

        if (UIManager.Instance != null && detailPanel != null)
            UIManager.Instance.ShowHover(detailPanel, targetRect, itemDef, ctx);
    }

    public void UnhoverSlot(RectTransform slotRect)
    {
        if (slotRect == currentSlotRect)
            ClearCurrentHover();

        if (UIManager.Instance != null && detailPanel != null)
            UIManager.Instance.HideHover(detailPanel, slotRect);
    }

    /// <summary>
    /// 책임 :
    /// - 키 설정에 등록되지 않는 고정 F 입력으로 현재 hover 중인 플레이어 인벤토리 아이템을 월드에 버린다.
    /// - 상자 UI, 도감, 상점, 월드 아이템처럼 버리기 대상이 아닌 상세 컨텍스트는 무시한다.
    /// </summary>
    private void HandleFixedInventoryDropInput()
    {
        if (!InputKeyCompatibility.WasPressedThisFrame(KeyCode.F))
            return;

        if (!CanDropCurrentHoverToWorld())
            return;

        DropZoneUI dropZone = DropZoneUI.ActiveInstance;
        if (dropZone == null)
            return;

        bool dropped = dropZone.TryDropSourceToWorld(currentSourceContainer, currentSourceIndex);
        if (!dropped)
            return;

        ItemDragContext.CancelActiveDragSession();
        ClearCurrentHover();
        UIManager.Instance?.HideHoverImmediate();
    }

    private bool CanDropCurrentHoverToWorld()
    {
        if (ItemDragContext.Active)
            return false;

        if (currentContext == null ||
            currentSourceContainer == null ||
            currentSourceIndex < 0 ||
            currentSourceIndex >= currentSourceContainer.SlotCount)
        {
            return false;
        }

        if (currentSourceContainer.Get(currentSourceIndex) == null)
            return false;

        ItemDetailActionHint hint = currentContext.ResolvePrimaryActionHint();
        return hint.Visible && hint.Key == KeyCode.F;
    }

    private void ClearCurrentHover()
    {
        currentSlotRect = null;
        currentSourceContainer = null;
        currentSourceIndex = -1;
        currentContext = null;
    }

    private ItemDetailContext BuildContext()
    {
        if (_contextProvider == null && contextProviderBehaviour != null)
            _contextProvider = contextProviderBehaviour as IItemDetailContextProvider;

        if (_contextProvider != null)
        {
            var providedContext = _contextProvider.BuildContext();
            if (providedContext != null)
                return providedContext;
        }

        if (PlayerRuntimeRegistry.CurrentPlayer != null)
            return ItemDetailContext.FromOwner(PlayerRuntimeRegistry.CurrentPlayer.gameObject);

        if (PlayerInteractor2D.Instance != null)
            return ItemDetailContext.FromOwner(PlayerInteractor2D.Instance.gameObject);

        return new ItemDetailContext();
    }

    private static ItemDetailPanel FindDetailPanelInLoadedScenes()
    {
        if (ItemDetailPanel.Instance != null && ItemDetailPanel.Instance.gameObject.scene.isLoaded)
            return ItemDetailPanel.Instance;

        var panels = Resources.FindObjectsOfTypeAll<ItemDetailPanel>();
        foreach (var panel in panels)
        {
            if (panel == null)
                continue;

            var scene = panel.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            return panel;
        }

        return null;
    }
}

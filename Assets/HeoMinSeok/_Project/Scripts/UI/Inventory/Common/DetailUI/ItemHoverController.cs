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

    public void HoverSlot(RectTransform slotRect, ScriptableObject itemDef, IItemContainer container = null, int index = -1)
    {
        if (itemDef == null)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.HideHoverImmediate();
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

        if (UIManager.Instance != null && detailPanel != null)
            UIManager.Instance.ShowHover(detailPanel, slotRect, itemDef, ctx);
    }

    public void UnhoverSlot(RectTransform slotRect)
    {
        if (UIManager.Instance != null && detailPanel != null)
            UIManager.Instance.HideHover(detailPanel, slotRect);
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

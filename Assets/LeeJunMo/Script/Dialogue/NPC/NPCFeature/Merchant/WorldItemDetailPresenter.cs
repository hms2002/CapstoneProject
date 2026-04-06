using UnityEngine;

public sealed class WorldItemDetailPresenter : MonoBehaviour
{
    public static WorldItemDetailPresenter Instance { get; private set; }

    private static bool s_isQuitting;

    [SerializeField] private Vector2 proxySize = new Vector2(72f, 72f);
    [SerializeField] private Canvas hoverCanvas;
    [SerializeField] private Camera worldCamera;

    private RectTransform proxyRect;
    private Transform currentWorldAnchor;
    private ScriptableObject currentItem;
    private int currentRelicLevelOverride;
    private bool isShowing;
    private bool isHoverBound;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        var root = new GameObject(nameof(WorldItemDetailPresenter));
        root.AddComponent<WorldItemDetailPresenter>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        GlobalUIRoot.AdoptService(transform);
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!isShowing)
            return;

        if (currentWorldAnchor == null || currentItem == null)
        {
            HideCurrent();
            return;
        }

        if (!EnsureProxyRect())
            return;

        UpdateProxyPosition();
        TryBindHover();
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        HideCurrent();
    }

    public void Show(Transform worldAnchor, ScriptableObject itemDefinition, int relicLevelOverride = 0)
    {
        if (worldAnchor == null || itemDefinition == null)
        {
            HideCurrent();
            return;
        }

        currentWorldAnchor = worldAnchor;
        currentItem = itemDefinition;
        currentRelicLevelOverride = Mathf.Max(0, relicLevelOverride);
        isShowing = true;
        isHoverBound = false;

        if (!EnsureProxyRect())
            return;

        UpdateProxyPosition();
        TryBindHover();
    }

    public void Hide(Transform worldAnchor = null)
    {
        if (worldAnchor != null && currentWorldAnchor != worldAnchor)
            return;

        HideCurrent();
    }

    private void HideCurrent()
    {
        if (isHoverBound && ItemHoverController.Instance != null && proxyRect != null)
            ItemHoverController.Instance.UnhoverSlot(proxyRect);

        isShowing = false;
        isHoverBound = false;
        currentWorldAnchor = null;
        currentItem = null;
        currentRelicLevelOverride = 0;
    }

    private bool EnsureProxyRect()
    {
        Canvas canvas = ResolveHoverCanvas();
        if (canvas == null)
            return false;

        if (proxyRect == null)
        {
            var go = new GameObject("WorldItemHoverProxy", typeof(RectTransform));
            go.hideFlags = HideFlags.HideAndDontSave;
            proxyRect = go.GetComponent<RectTransform>();
            proxyRect.anchorMin = new Vector2(0.5f, 0.5f);
            proxyRect.anchorMax = new Vector2(0.5f, 0.5f);
            proxyRect.pivot = new Vector2(0.5f, 0.5f);
            proxyRect.sizeDelta = proxySize;
        }

        if (proxyRect.parent != canvas.transform)
            proxyRect.SetParent(canvas.transform, false);

        proxyRect.sizeDelta = proxySize;
        return true;
    }

    private void TryBindHover()
    {
        if (!isShowing || isHoverBound || proxyRect == null || currentItem == null)
            return;

        if (ItemHoverController.Instance == null)
            return;

        ItemHoverController.Instance.HoverWorldTarget(proxyRect, currentItem, currentRelicLevelOverride);
        isHoverBound = true;
    }

    private void UpdateProxyPosition()
    {
        Canvas canvas = ResolveHoverCanvas();
        if (canvas == null || currentWorldAnchor == null || proxyRect == null)
            return;

        Camera resolvedWorldCamera = ResolveWorldCamera();
        if (resolvedWorldCamera == null)
            return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(resolvedWorldCamera, currentWorldAnchor.position);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
            proxyRect.anchoredPosition = localPoint;
    }

    private Canvas ResolveHoverCanvas()
    {
        if (hoverCanvas != null)
            return hoverCanvas;

        hoverCanvas = GlobalUIRoot.GetCanvas(GlobalCanvasLayer.Hover);
        if (hoverCanvas != null)
            return hoverCanvas;

        if (ItemDetailPanel.Instance != null)
            hoverCanvas = ItemDetailPanel.Instance.GetComponentInParent<Canvas>();

        return hoverCanvas;
    }

    private Camera ResolveWorldCamera()
    {
        if (worldCamera != null)
            return worldCamera;

        if (Camera.main != null)
            return Camera.main;

        return FindFirstObjectByType<Camera>();
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeTreeUI : MonoBehaviour, IStackableUI, IMouseCursorDomainSource
{
    public static UpgradeTreeUI EnsureInstance()
    {
        UpgradeTreeUI[] existing = Resources.FindObjectsOfTypeAll<UpgradeTreeUI>();
        for (int i = 0; i < existing.Length; i++)
        {
            UpgradeTreeUI candidate = existing[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;

            return candidate;
        }

        return null;
    }

    [Header("UI References")]
    public RectTransform contentRect;
    public Transform slotParent;
    public Transform lineParent;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private RectTransform viewportRect;

    [Header("Prefabs")]
    public GameObject slotPrefab;
    public GameObject linePrefab;

    [Header("Graph Layout")]
    [SerializeField] private Vector2 gridCellSize = new Vector2(UpgradeNodeSO.DefaultGridCellWidth, UpgradeNodeSO.DefaultGridCellHeight);
    [SerializeField] private Vector2 contentPadding = new Vector2(520f, 360f);
    [SerializeField] private Vector2 minimumContentSize = new Vector2(2200f, 1400f);
    [SerializeField] private float lineThickness = 4f;
    [SerializeField] private bool rebuildOnOpen = true;
    [SerializeField] private bool centerOnOpen = true;
    [SerializeField] private bool forceFullScreenLayout = true;

    private readonly List<UpgradeSlotUI> allSlots = new List<UpgradeSlotUI>();
    private readonly List<GameObject> allLines = new List<GameObject>();
    private bool hasBuilt;
    private bool isRightMousePanning;
    private Vector2 lastPointerLocalPosition;

    public bool IsActive => gameObject.activeSelf;
    public bool CanCloseOnEscape => true;
    public UIOpenGroup OpenGroup => UIOpenGroup.ExclusiveModal;
    public UIOpenGroup BlockedOpenGroups => UIOpenGroup.ExclusiveModal;
    public UIGameplayLockProfile GameplayLockProfile => UIGameplayLockProfile.FreezeAndBlockControl;
    public MouseCursorDomain CursorDomain => MouseCursorDomain.NpcUi;

    public void OpenUI()
    {
        gameObject.SetActive(true);
        PrepareLayout();

        if (rebuildOnOpen || !hasBuilt)
            BuildUI();

        if (centerOnOpen)
            CenterContent();

        RefreshAll();
    }

    public void CloseUI()
    {
        gameObject.SetActive(false);

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnUIClosed?.Invoke();
    }

    private void Start()
    {
        PrepareLayout();

        if (!hasBuilt)
            BuildUI();

        if (centerOnOpen)
            CenterContent();
    }

    private void Update()
    {
        HandleRightMousePan();
    }

    private void LateUpdate()
    {
        ClampContentPosition();
    }

    private void OnRectTransformDimensionsChange()
    {
        ClampContentPosition();
    }

    private void OnEnable()
    {
        MouseCursorService.EnsureInstance().SetDomain(this, MouseCursorDomain.NpcUi, priority: 100);

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnDataChanged += RefreshAll;

        if (UIManager.Instance != null)
            UIManager.Instance.SetGameplayHudCurrencyHidden(this, true);
    }

    private void OnDisable()
    {
        MouseCursorService.Instance?.ClearDomain(this);

        if (UpgradeManager.Instance != null)
            UpgradeManager.Instance.OnDataChanged -= RefreshAll;

        if (UIManager.Instance != null)
            UIManager.Instance.SetGameplayHudCurrencyHidden(this, false);
    }

    public void BuildUI()
    {
        PrepareLayout();
        ClearGeneratedChildren();

        allSlots.Clear();
        allLines.Clear();
        hasBuilt = true;

        Dictionary<int, UpgradeSlotUI> slotDict = new Dictionary<int, UpgradeSlotUI>();
        List<UpgradeNodeSO> allUpgrades = UpgradeManager.Instance != null ? UpgradeManager.Instance.GetAllUpgrades() : null;
        if (allUpgrades == null || slotParent == null || slotPrefab == null)
            return;

        Dictionary<UpgradeNodeSO, Vector2> nodePositions = CalculateNodePositions(allUpgrades);

        foreach (var node in allUpgrades)
        {
            if (node == null)
                continue;

            GameObject slotObj = Instantiate(slotPrefab, slotParent);
            UpgradeSlotUI slotUI = slotObj.GetComponent<UpgradeSlotUI>();
            RectTransform rect = slotObj.GetComponent<RectTransform>();
            if (slotUI == null || rect == null)
            {
                Destroy(slotObj);
                continue;
            }

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            if (nodePositions.TryGetValue(node, out Vector2 position))
                rect.anchoredPosition = position;

            slotUI.assignedNode = node;
            slotUI.InitSlot(n => UpgradeManager.Instance.TryBuyUpgrade(n.nodeID));

            allSlots.Add(slotUI);
            if (!slotDict.ContainsKey(node.nodeID))
                slotDict.Add(node.nodeID, slotUI);
        }

        HashSet<string> drawnEdges = new HashSet<string>();
        foreach (var node in allUpgrades)
        {
            if (node == null || !slotDict.ContainsKey(node.nodeID))
                continue;

            if (node.unlockedNodeIDs == null)
                continue;

            foreach (var nextId in node.unlockedNodeIDs)
            {
                if (!slotDict.TryGetValue(nextId, out var targetSlot))
                    continue;

                string edgeKey = GetEdgeKey(node.nodeID, nextId);
                if (!drawnEdges.Add(edgeKey))
                    continue;

                DrawLine(
                    slotDict[node.nodeID].GetComponent<RectTransform>(),
                    targetSlot.GetComponent<RectTransform>());
            }
        }

        ClampContentPosition();
    }

    private void DrawLine(RectTransform start, RectTransform end)
    {
        Vector2 s = start.anchoredPosition;
        Vector2 e = end.anchoredPosition;

        if (Vector2.Distance(s, e) < 1f)
            return;

        CreateLineSegment(s, e);
    }

    private void CreateLineSegment(Vector2 start, Vector2 end)
    {
        if (Vector2.Distance(start, end) < 0.1f || lineParent == null || linePrefab == null)
            return;

        GameObject line = Instantiate(linePrefab, lineParent);
        RectTransform rect = line.GetComponent<RectTransform>();
        if (rect == null)
        {
            Destroy(line);
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);

        Vector2 dir = end - start;
        float dist = dir.magnitude;

        rect.sizeDelta = new Vector2(dist, lineThickness);
        rect.anchoredPosition = start;
        rect.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        allLines.Add(line);
    }

    private void PrepareLayout()
    {
        ResolveReferences();
        ConfigureFullScreenLayout();
        ConfigureScrollRect();

        if (contentRect != null)
        {
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
        }

        StretchLayer(slotParent as RectTransform);
        StretchLayer(lineParent as RectTransform);
    }

    private void ResolveReferences()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();

        if (scrollRect == null)
            scrollRect = GetComponentInChildren<ScrollRect>(true);

        if (contentRect == null && scrollRect != null)
            contentRect = scrollRect.content;

        if (viewportRect == null && scrollRect != null)
            viewportRect = scrollRect.viewport;

        if (viewportRect == null && contentRect != null)
            viewportRect = contentRect.parent as RectTransform;
    }

    private void ConfigureFullScreenLayout()
    {
        if (!forceFullScreenLayout)
            return;

        RectTransform rootRect = transform as RectTransform;
        StretchLayer(rootRect);

        if (viewportRect != null && viewportRect != rootRect)
            StretchLayer(viewportRect);
    }

    private void ConfigureScrollRect()
    {
        if (scrollRect == null)
            return;

        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = true;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;
    }

    private void StretchLayer(RectTransform rect)
    {
        if (rect == null)
            return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private void ClearGeneratedChildren()
    {
        ClearChildren(slotParent);
        ClearChildren(lineParent);
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    private Dictionary<UpgradeNodeSO, Vector2> CalculateNodePositions(List<UpgradeNodeSO> allUpgrades)
    {
        Dictionary<UpgradeNodeSO, Vector2> positions = new Dictionary<UpgradeNodeSO, Vector2>();
        bool hasNode = false;
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.zero;

        foreach (UpgradeNodeSO node in allUpgrades)
        {
            if (node == null)
                continue;

            Vector2 rawPosition = node.GetUiPosition(gridCellSize);
            if (!hasNode)
            {
                min = rawPosition;
                max = rawPosition;
                hasNode = true;
            }
            else
            {
                min = Vector2.Min(min, rawPosition);
                max = Vector2.Max(max, rawPosition);
            }
        }

        if (!hasNode)
        {
            ApplyContentSize(minimumContentSize);
            return positions;
        }

        Vector2 graphSize = max - min;
        Vector2 requiredSize = graphSize + (contentPadding * 2f);
        ApplyContentSize(requiredSize);

        Vector2 graphCenter = (min + max) * 0.5f;
        foreach (UpgradeNodeSO node in allUpgrades)
        {
            if (node == null)
                continue;

            positions[node] = node.GetUiPosition(gridCellSize) - graphCenter;
        }

        return positions;
    }

    private void ApplyContentSize(Vector2 requiredSize)
    {
        if (contentRect == null)
            return;

        Vector2 viewportSize = GetViewportSize();
        float width = Mathf.Max(requiredSize.x, minimumContentSize.x, viewportSize.x);
        float height = Mathf.Max(requiredSize.y, minimumContentSize.y, viewportSize.y);

        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        contentRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

    private Vector2 GetViewportSize()
    {
        if (viewportRect != null)
            return viewportRect.rect.size;

        RectTransform rootRect = transform as RectTransform;
        if (rootRect != null)
            return rootRect.rect.size;

        return new Vector2(Screen.width, Screen.height);
    }

    private void CenterContent()
    {
        if (contentRect == null)
            return;

        contentRect.anchoredPosition = Vector2.zero;
        if (scrollRect != null)
            scrollRect.StopMovement();
    }

    private void HandleRightMousePan()
    {
        if (contentRect == null || viewportRect == null)
            return;

        Camera eventCamera = GetEventCamera();

        if (Input.GetMouseButtonDown(1))
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(viewportRect, Input.mousePosition, eventCamera)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, Input.mousePosition, eventCamera, out lastPointerLocalPosition))
            {
                isRightMousePanning = true;
                if (scrollRect != null)
                    scrollRect.StopMovement();
            }
        }

        if (isRightMousePanning && Input.GetMouseButton(1))
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, Input.mousePosition, eventCamera, out Vector2 currentLocalPosition))
            {
                Vector2 delta = currentLocalPosition - lastPointerLocalPosition;
                contentRect.anchoredPosition += delta;
                lastPointerLocalPosition = currentLocalPosition;
                ClampContentPosition();
            }
        }

        if (Input.GetMouseButtonUp(1))
            isRightMousePanning = false;
    }

    private Camera GetEventCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        if (canvas.worldCamera != null)
            return canvas.worldCamera;

        return Camera.main;
    }

    private void ClampContentPosition()
    {
        if (contentRect == null || viewportRect == null)
            return;

        Vector2 contentSize = contentRect.rect.size;
        Vector2 viewportSize = viewportRect.rect.size;
        Vector2 position = contentRect.anchoredPosition;

        position.x = ClampAxis(position.x, contentSize.x, viewportSize.x);
        position.y = ClampAxis(position.y, contentSize.y, viewportSize.y);
        contentRect.anchoredPosition = position;
    }

    private float ClampAxis(float position, float contentSize, float viewportSize)
    {
        if (contentSize <= viewportSize)
            return 0f;

        float limit = (contentSize - viewportSize) * 0.5f;
        return Mathf.Clamp(position, -limit, limit);
    }

    private string GetEdgeKey(int fromId, int toId)
    {
        return fromId < toId ? $"{fromId}:{toId}" : $"{toId}:{fromId}";
    }

    public void OnClickClose()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.PopUI(this);
        else
            CloseUI();
    }

    public void RefreshAll()
    {
        foreach (var slot in allSlots)
        {
            if (slot != null)
                slot.RefreshUI();
        }
    }
}

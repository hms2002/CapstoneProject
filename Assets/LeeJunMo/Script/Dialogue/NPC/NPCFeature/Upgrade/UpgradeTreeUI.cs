using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField, Tooltip("If assigned, this node is placed at the center of the initial viewport when the tree opens. If empty or unavailable, the graph bounds center is used.")]
    private UpgradeNodeSO initialFocusNode;
    [SerializeField] private float lineThickness = 4f;
    [SerializeField] private bool rebuildOnOpen = true;
    [SerializeField] private bool centerOnOpen = true;
    [SerializeField] private bool forceFullScreenLayout = true;

    [Header("Lake Presentation")]
    [Tooltip("Optional explicit Image that receives the lake material. Leave empty to use the hidden internal lake surface layer.")]
    [SerializeField] private Image lakeSurfaceImage;
    [Tooltip("Visual tuning material for the lake background. Edit this asset directly for the fastest preview workflow.")]
    [SerializeField] private Material lakeSurfaceMaterial;
    [Tooltip("When enabled, static lake surface values come from the material. Runtime-only interaction values still come from the settings below.")]
    [SerializeField] private bool useLakeSurfaceMaterialSettings = true;
    [Tooltip("When enabled, changing this component in Edit Mode refreshes the lake surface preview target.")]
    [SerializeField] private bool previewLakeSurfaceInEditMode = true;
    [Tooltip("Allows the lake surface Test Preview buttons to animate in Edit Mode.")]
    [SerializeField] private bool animateLakeSurfaceInEditMode = true;
    [SerializeField] private UpgradeLakePresentationSettings lakePresentationSettings = UpgradeLakePresentationSettings.CreateDefault();
    [SerializeField] private UpgradeLakePresentation lakePresentation;
#if UNITY_EDITOR
    [System.NonSerialized] private bool lakePreviewTestActiveInEditor;
#endif

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

    private void Awake()
    {
        ResolveReferences();
        EnsureLakePresentation();
    }

    public void OpenUI()
    {
        gameObject.SetActive(true);
        PrepareLayout();
        EnsureLakePresentation();

        if (rebuildOnOpen || !hasBuilt)
            BuildUI();

        if (centerOnOpen)
            CenterContent();

        RefreshAll();
        lakePresentation?.PlayOpen();
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
        EnsureLakePresentation();

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
            UpgradeSlotUI capturedSlot = slotUI;
            slotUI.InitSlot(n =>
            {
                EmitPurchaseRipple(capturedSlot);
                UpgradeManager.Instance.TryBuyUpgrade(n.nodeID);
            });
            slotUI.SetPresentationCallbacks(HandleSlotPointerEnter, HandleSlotPointerExit);

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
        EnsureLakePresentation();

        if (contentRect != null)
        {
            contentRect.anchorMin = new Vector2(0.5f, 0.5f);
            contentRect.anchorMax = new Vector2(0.5f, 0.5f);
            contentRect.pivot = new Vector2(0.5f, 0.5f);
        }

        StretchLayer(slotParent as RectTransform);
        StretchLayer(lineParent as RectTransform);
    }

    private void EnsureLakePresentation()
    {
        ResolveReferences();
        if (viewportRect == null)
            return;

        if (lakePresentation == null)
            lakePresentation = GetComponent<UpgradeLakePresentation>();

        if (lakePresentation == null && lakePresentationSettings.enabled)
            lakePresentation = gameObject.AddComponent<UpgradeLakePresentation>();

        if (lakePresentation != null)
        {
            Image surfaceTarget = lakeSurfaceImage;
            if (surfaceTarget != null && surfaceTarget.rectTransform == viewportRect)
                surfaceTarget = null;

            lakePresentation.Initialize(
                viewportRect,
                contentRect,
                surfaceTarget,
                lakePresentationSettings,
                lakeSurfaceMaterial,
                useLakeSurfaceMaterialSettings,
                ShouldUseAnimatedLakePreview());
        }
    }

    private bool ShouldUseAnimatedLakePreview()
    {
#if UNITY_EDITOR
        return !Application.isPlaying &&
               previewLakeSurfaceInEditMode &&
               animateLakeSurfaceInEditMode &&
               lakePreviewTestActiveInEditor &&
               lakePresentationSettings.enabled;
#else
        return false;
#endif
    }

#if UNITY_EDITOR
    public bool IsLakePreviewTestActiveInEditor => lakePreviewTestActiveInEditor;

    public bool ShouldAnimateLakePreviewInEditor =>
        ShouldUseAnimatedLakePreview();

    private void OnValidate()
    {
        lakePresentationSettings.Sanitize();

        if (!Application.isPlaying && previewLakeSurfaceInEditMode)
            UnityEditor.EditorApplication.delayCall += RefreshLakePreviewInEditor;
    }

    public void RefreshLakePreviewInEditor()
    {
        if (this == null || Application.isPlaying || !gameObject.scene.IsValid())
            return;

        ResolveLakeSurfaceMaterialAssetInEditor();
        PrepareLayout();
        EnsureLakePresentation();
        lakePresentation?.TickEditorPreview();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    public void TickLakePreviewInEditor()
    {
        if (this == null ||
            Application.isPlaying ||
            !gameObject.scene.IsValid() ||
            !ShouldAnimateLakePreviewInEditor)
        {
            return;
        }

        ResolveReferences();
        if (lakeSurfaceMaterial != null && !UnityEditor.EditorUtility.IsPersistent(lakeSurfaceMaterial))
            ResolveLakeSurfaceMaterialAssetInEditor();

        EnsureLakePresentation();
        lakePresentation?.TickEditorPreview();
    }

    public void StartLakePreviewTestInEditor()
    {
        if (this == null || Application.isPlaying || !gameObject.scene.IsValid())
            return;

        UnityEditor.Undo.RecordObject(this, "Start Lake Test Preview");
        previewLakeSurfaceInEditMode = true;
        animateLakeSurfaceInEditMode = true;
        lakePreviewTestActiveInEditor = true;
        ResolveLakeSurfaceMaterialAssetInEditor();
        PrepareLayout();
        EnsureLakePresentation();
        lakePresentation?.TickEditorPreview();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditor.SceneView.RepaintAll();
    }

    public void StopLakePreviewTestInEditor()
    {
        if (this == null || Application.isPlaying)
            return;

        lakePreviewTestActiveInEditor = false;
        Material restoredMaterial = ResolveLakeSurfaceMaterialAssetInEditor();
        EnsureLakePresentation();
        lakePresentation?.ClearEditorInteractionPreview();
        lakePresentation?.RestoreEditorPreviewMaterial(restoredMaterial);
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditor.SceneView.RepaintAll();
    }

    public void TestLakeRippleInEditor()
    {
        if (this == null || Application.isPlaying || !gameObject.scene.IsValid())
            return;

        RefreshLakePreviewInEditor();
        lakePresentation?.EmitEditorRipplePreview();
    }

    public void TestLakeWakeInEditor()
    {
        if (this == null || Application.isPlaying || !gameObject.scene.IsValid())
            return;

        RefreshLakePreviewInEditor();
        lakePresentation?.EmitEditorWakePreview();
    }

    public void ClearLakeInteractionPreviewInEditor()
    {
        if (this == null || Application.isPlaying || !gameObject.scene.IsValid())
            return;

        RefreshLakePreviewInEditor();
        lakePresentation?.ClearEditorInteractionPreview();
    }

    public void RestoreLakePreviewMaterialInEditor()
    {
        RestoreLakePreviewMaterialInEditor(disableAnimation: false);
    }

    public void RestoreLakePreviewMaterialInEditor(bool disableAnimation)
    {
        if (this == null || Application.isPlaying)
            return;

        if (disableAnimation)
        {
            UnityEditor.Undo.RecordObject(this, "Disable Lake Preview Animation");
            animateLakeSurfaceInEditMode = false;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        Material restoredMaterial = ResolveLakeSurfaceMaterialAssetInEditor();
        EnsureLakePresentation();
        lakePresentation?.RestoreEditorPreviewMaterial(restoredMaterial);
    }

    public void ApplyLakeSettingsToMaterial()
    {
        Material targetMaterial = ResolveLakeSurfaceMaterialAssetInEditor();
        if (targetMaterial == null)
            return;

        UnityEditor.Undo.RecordObject(targetMaterial, "Apply Lake Settings To Material");
        lakePresentationSettings.ApplySurfaceSettingsTo(targetMaterial);
        UnityEditor.EditorUtility.SetDirty(targetMaterial);
        RefreshLakePreviewInEditor();
    }

    public void ReadLakeSettingsFromMaterial()
    {
        Material sourceMaterial = ResolveLakeSurfaceMaterialAssetInEditor();
        if (sourceMaterial == null)
            return;

        UnityEditor.Undo.RecordObject(this, "Read Lake Settings From Material");
        lakePresentationSettings.ReadSurfaceSettingsFrom(sourceMaterial);
        lakePresentationSettings.Sanitize();
        UnityEditor.EditorUtility.SetDirty(this);
        RefreshLakePreviewInEditor();
    }

    private Material ResolveLakeSurfaceMaterialAssetInEditor()
    {
        Material resolvedMaterial = ResolvePersistentMaterialAsset(lakeSurfaceMaterial);
        if (resolvedMaterial == lakeSurfaceMaterial)
            return lakeSurfaceMaterial;

        UnityEditor.Undo.RecordObject(this, "Restore Lake Surface Material Asset");
        lakeSurfaceMaterial = resolvedMaterial;
        UnityEditor.EditorUtility.SetDirty(this);
        return lakeSurfaceMaterial;
    }

    private static Material ResolvePersistentMaterialAsset(Material material)
    {
        if (material == null)
            return FindMaterialAssetByName("M_UpgradeLakeSurface");

        if (UnityEditor.EditorUtility.IsPersistent(material))
            return material;

        string materialName = NormalizePreviewMaterialName(material.name);
        Material resolvedMaterial = FindMaterialAssetByName(materialName);
        if (resolvedMaterial != null)
            return resolvedMaterial;

        return FindMaterialAssetByName("M_UpgradeLakeSurface");
    }

    private static string NormalizePreviewMaterialName(string materialName)
    {
        if (string.IsNullOrEmpty(materialName))
            return string.Empty;

        string normalizedName = materialName.Replace(" (Instance)", string.Empty);
        while (normalizedName.StartsWith("M_EditorPreview", System.StringComparison.Ordinal))
            normalizedName = normalizedName.Substring("M_EditorPreview".Length);

        while (normalizedName.StartsWith("M_Runtime", System.StringComparison.Ordinal))
            normalizedName = normalizedName.Substring("M_Runtime".Length);

        return normalizedName;
    }

    private static Material FindMaterialAssetByName(string materialName)
    {
        if (string.IsNullOrEmpty(materialName))
            return null;

        if (materialName == "M_UpgradeLakeSurface")
        {
            Material defaultLakeMaterial =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Shader/M_UpgradeLakeSurface.mat");
            if (defaultLakeMaterial != null)
                return defaultLakeMaterial;
        }

        string[] guids = UnityEditor.AssetDatabase.FindAssets($"{materialName} t:Material");
        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            Material candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (candidate != null && candidate.name == materialName)
                return candidate;
        }

        return null;
    }
#endif

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

        Vector2 layoutCenter = ResolveInitialLayoutCenter(allUpgrades, min, max);
        Vector2 minFromCenter = min - layoutCenter;
        Vector2 maxFromCenter = max - layoutCenter;
        Vector2 requiredHalfSize = new Vector2(
            Mathf.Max(Mathf.Abs(minFromCenter.x), Mathf.Abs(maxFromCenter.x)),
            Mathf.Max(Mathf.Abs(minFromCenter.y), Mathf.Abs(maxFromCenter.y)));
        Vector2 requiredSize = (requiredHalfSize * 2f) + (contentPadding * 2f);
        ApplyContentSize(requiredSize);

        foreach (UpgradeNodeSO node in allUpgrades)
        {
            if (node == null)
                continue;

            positions[node] = node.GetUiPosition(gridCellSize) - layoutCenter;
        }

        return positions;
    }

    private Vector2 ResolveInitialLayoutCenter(List<UpgradeNodeSO> allUpgrades, Vector2 min, Vector2 max)
    {
        if (initialFocusNode != null && allUpgrades != null && allUpgrades.Contains(initialFocusNode))
            return initialFocusNode.GetUiPosition(gridCellSize);

        return (min + max) * 0.5f;
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

    private void HandleSlotPointerEnter(UpgradeSlotUI slot, PointerEventData eventData)
    {
        if (lakePresentation == null || eventData == null)
            return;

        lakePresentation.EmitHoverRipple(eventData.position, eventData.enterEventCamera ?? GetEventCamera());
    }

    private void HandleSlotPointerExit(UpgradeSlotUI slot, PointerEventData eventData)
    {
    }

    private void EmitPurchaseRipple(UpgradeSlotUI slot)
    {
        if (lakePresentation == null || slot == null)
            return;

        lakePresentation.EmitPurchaseRipple(slot.transform as RectTransform);
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

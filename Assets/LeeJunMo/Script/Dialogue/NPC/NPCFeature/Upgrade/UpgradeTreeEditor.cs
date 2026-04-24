#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class UpgradeTreeEditor : EditorWindow
{
    private const float SidebarWidth = 360f;
    private const float NodeWidth = 132f;
    private const float NodeHeight = 84f;
    private const float CanvasPadding = 220f;
    private const string NodeFolder = "Assets/Resources/Upgrades/Nodes";
    private const string DefaultUpgradeTreePrefabPath = "Assets/LeeJunMo/Prefab/UI/Upgrade/UpgradeTreePanel.prefab";

    private static readonly Vector2 CellSize = new Vector2(UpgradeNodeSO.DefaultGridCellWidth, UpgradeNodeSO.DefaultGridCellHeight);

    private enum EditorTab
    {
        GraphEditor,
        RuntimePreview
    }

    private struct PreviewLayoutSettings
    {
        public Vector2 gridCellSize;
        public Vector2 contentPadding;
        public Vector2 minimumContentSize;
        public Vector2 viewportSize;
        public Vector2 slotSize;
        public GameObject slotPrefab;
        public GameObject linePrefab;
        public float lineThickness;
    }

    private sealed class PreviewLayout
    {
        public readonly Dictionary<UpgradeNodeSO, Vector2> nodePositions = new Dictionary<UpgradeNodeSO, Vector2>();
        public Vector2 contentSize;
        public Vector2 graphMin;
        public Vector2 graphMax;
        public Vector2 graphCenter;
        public bool hasNode;
    }

    private UpgradeDatabase selectedDatabase;
    private Vector2 graphScrollPos;
    private Vector2 inspectorScrollPos;
    private Vector2 previewPan;
    private UpgradeNodeSO selectedNode;
    private Editor cachedNodeEditor;
    private EditorTab currentTab;
    private GameObject previewTreePrefab;
    private GameObject previewSlotPrefab;
    private GameObject previewLinePrefab;
    private PreviewRenderUtility runtimePreviewUtility;
    private GameObject runtimePreviewRoot;
    private int runtimePreviewHash;
    private bool isConnecting;
    private bool previewUsePrefabSettings = true;
    private bool previewShowGrid = true;
    private bool previewShowContentRect = true;
    private bool previewShowViewportRect = true;
    private bool previewShowNodeIds;
    private LockType previewSlotState = LockType.UnLocked;
    private float previewZoom = 1f;
    private Vector2 previewViewportSize = new Vector2(1600f, 900f);
    private Vector2 previewGridCellSize = CellSize;
    private Vector2 previewContentPadding = new Vector2(520f, 360f);
    private Vector2 previewMinimumContentSize = new Vector2(2200f, 1400f);
    private Vector2 previewSlotSize = new Vector2(70f, 70f);
    private float previewLineThickness = 4f;

    private int viewMinX = -4;
    private int viewMaxX = 10;
    private int viewMinY = -4;
    private int viewMaxY = 4;
    private int createGridX;
    private int createGridY;

    [MenuItem("Tools/Upgrade Tree Editor")]
    public static void ShowWindow()
    {
        GetWindow<UpgradeTreeEditor>("Upgrade Tree Editor").minSize = new Vector2(980f, 640f);
    }

    private void OnEnable()
    {
        wantsMouseMove = true;
    }

    private void OnDisable()
    {
        ReleaseCachedNodeEditor();
        ReleaseRuntimePrefabPreview();
    }

    private void OnGUI()
    {
        DrawTopBar();

        if (selectedDatabase == null)
        {
            DrawDatabaseSelector();
            return;
        }

        NormalizeViewBounds();
        ExpandViewToIncludeNodes();

        if (currentTab == EditorTab.GraphEditor)
        {
            EditorGUILayout.BeginHorizontal();
            DrawGraphView();
            DrawSplitter();
            DrawSidePanel();
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            DrawRuntimePreviewTab();
        }

        ProcessEvents(Event.current);

        if (Event.current.isMouse || Event.current.isKey)
            Repaint();
    }

    private void DrawTopBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUI.BeginChangeCheck();
        UpgradeDatabase newDatabase = (UpgradeDatabase)EditorGUILayout.ObjectField(selectedDatabase, typeof(UpgradeDatabase), false, GUILayout.Width(280f));
        if (EditorGUI.EndChangeCheck())
            SetDatabase(newDatabase);

        if (GUILayout.Button("Find DB", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            SetDatabase(FindFirstDatabase());

        EditorGUI.BeginDisabledGroup(selectedDatabase == null);
        if (GUILayout.Button("Frame All", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            FrameAllNodes();
        EditorGUI.EndDisabledGroup();

        currentTab = (EditorTab)GUILayout.Toolbar(
            (int)currentTab,
            new[] { "Graph Editor", "Runtime Preview" },
            EditorStyles.toolbarButton,
            GUILayout.Width(230f));

        GUILayout.FlexibleSpace();
        GUILayout.Label(currentTab == EditorTab.GraphEditor
            ? "Edit with the same slot and line visuals used by the runtime preview."
            : "Preview mirrors the runtime UpgradeTreeUI content layout.",
            EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDatabaseSelector()
    {
        GUILayout.Space(24f);
        EditorGUILayout.HelpBox("Select an UpgradeDatabase to edit the grid-based upgrade graph.", MessageType.Info);
    }

    private UpgradeDatabase FindFirstDatabase()
    {
        string[] guids = AssetDatabase.FindAssets("t:UpgradeDatabase");
        if (guids == null || guids.Length == 0)
            return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<UpgradeDatabase>(path);
    }

    private void SetDatabase(UpgradeDatabase database)
    {
        if (selectedDatabase == database)
            return;

        selectedDatabase = database;
        selectedNode = null;
        isConnecting = false;
        ReleaseCachedNodeEditor();
        graphScrollPos = Vector2.zero;
    }

    private void DrawSplitter()
    {
        GUILayout.Box("", GUILayout.Width(1f), GUILayout.ExpandHeight(true));
    }

    private void DrawGraphView()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawVisualGraphToolbar();

        PreviewLayoutSettings settings = ResolvePreviewLayoutSettings();
        PreviewLayout layout = BuildPreviewLayout(settings);
        Rect graphRect = GUILayoutUtility.GetRect(
            GUIContent.none,
            GUIStyle.none,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true),
            GUILayout.MinHeight(420f));

        DrawRuntimePreview(graphRect, layout, settings, true);
        EditorGUILayout.EndVertical();
    }

    private void DrawVisualGraphToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Reset View", EditorStyles.toolbarButton, GUILayout.Width(76f)))
        {
            previewPan = Vector2.zero;
            previewZoom = 1f;
        }

        if (GUILayout.Button("Frame All", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            previewPan = Vector2.zero;

        GUILayout.Space(8f);
        GUILayout.Label("Zoom", GUILayout.Width(36f));
        previewZoom = GUILayout.HorizontalSlider(previewZoom, 0.35f, 2.5f, GUILayout.Width(120f));
        GUILayout.Label($"{previewZoom:0.00}x", GUILayout.Width(48f));

        GUILayout.Space(12f);
        GUILayout.Label("X", GUILayout.Width(14f));
        viewMinX = EditorGUILayout.IntField(viewMinX, GUILayout.Width(46f));
        GUILayout.Label("to", GUILayout.Width(18f));
        viewMaxX = EditorGUILayout.IntField(viewMaxX, GUILayout.Width(46f));

        GUILayout.Space(8f);
        GUILayout.Label("Y", GUILayout.Width(14f));
        viewMinY = EditorGUILayout.IntField(viewMinY, GUILayout.Width(46f));
        GUILayout.Label("to", GUILayout.Width(18f));
        viewMaxY = EditorGUILayout.IntField(viewMaxY, GUILayout.Width(46f));

        if (GUILayout.Button("Expand", EditorStyles.toolbarButton, GUILayout.Width(70f)))
        {
            viewMinX -= 2;
            viewMaxX += 2;
            viewMinY -= 2;
            viewMaxY += 2;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void HandleGraphPanInput(Rect canvasRect)
    {
        Event e = Event.current;
        if (!canvasRect.Contains(e.mousePosition) || e.button != 1)
            return;

        if (e.type == EventType.MouseDown)
        {
            e.Use();
            return;
        }

        if (e.type != EventType.MouseDrag)
            return;

        graphScrollPos -= e.delta;
        graphScrollPos.x = Mathf.Max(0f, graphScrollPos.x);
        graphScrollPos.y = Mathf.Max(0f, graphScrollPos.y);
        e.Use();
        Repaint();
    }

    private void DrawViewRangeToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("X", GUILayout.Width(14f));
        viewMinX = EditorGUILayout.IntField(viewMinX, GUILayout.Width(46f));
        GUILayout.Label("to", GUILayout.Width(18f));
        viewMaxX = EditorGUILayout.IntField(viewMaxX, GUILayout.Width(46f));

        GUILayout.Space(12f);
        GUILayout.Label("Y", GUILayout.Width(14f));
        viewMinY = EditorGUILayout.IntField(viewMinY, GUILayout.Width(46f));
        GUILayout.Label("to", GUILayout.Width(18f));
        viewMaxY = EditorGUILayout.IntField(viewMaxY, GUILayout.Width(46f));

        GUILayout.Space(12f);
        if (GUILayout.Button("Expand", EditorStyles.toolbarButton, GUILayout.Width(70f)))
        {
            viewMinX -= 2;
            viewMaxX += 2;
            viewMinY -= 2;
            viewMaxY += 2;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawBackgroundGrid(Rect canvasRect)
    {
        if (Event.current.type != EventType.Repaint)
            return;

        Handles.BeginGUI();

        for (int x = viewMinX; x <= viewMaxX; x++)
        {
            float canvasX = GridToCanvas(canvasRect, x, 0).x;
            Handles.color = x == 0 ? new Color(0.7f, 0.85f, 1f, 0.55f) : new Color(0.45f, 0.45f, 0.45f, 0.22f);
            Handles.DrawLine(new Vector3(canvasX, canvasRect.y), new Vector3(canvasX, canvasRect.yMax));
            GUI.Label(new Rect(canvasX + 4f, canvasRect.y + 4f, 46f, 18f), x.ToString(), EditorStyles.miniLabel);
        }

        for (int y = viewMinY; y <= viewMaxY; y++)
        {
            float canvasY = GridToCanvas(canvasRect, 0, y).y;
            Handles.color = y == 0 ? new Color(0.7f, 0.85f, 1f, 0.55f) : new Color(0.45f, 0.45f, 0.45f, 0.22f);
            Handles.DrawLine(new Vector3(canvasRect.x, canvasY), new Vector3(canvasRect.xMax, canvasY));
            GUI.Label(new Rect(canvasRect.x + 6f, canvasY + 4f, 46f, 18f), y.ToString(), EditorStyles.miniLabel);
        }

        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawConnections(Rect canvasRect)
    {
        if (Event.current.type != EventType.Repaint || selectedDatabase.allUpgrades == null)
            return;

        Handles.BeginGUI();
        HashSet<string> drawnEdges = new HashSet<string>();
        Color lineColor = EditorGUIUtility.isProSkin ? new Color(0.85f, 0.9f, 1f, 0.88f) : new Color(0.1f, 0.15f, 0.22f, 0.9f);

        foreach (UpgradeNodeSO node in selectedDatabase.allUpgrades)
        {
            if (node == null)
                continue;

            if (node.unlockedNodeIDs == null)
                continue;

            Vector3 start = GridToCanvas(canvasRect, node.gridX, node.gridY);
            foreach (int childId in node.unlockedNodeIDs)
            {
                UpgradeNodeSO childNode = FindNodeById(childId);
                if (childNode == null)
                    continue;

                string edgeKey = GetEdgeKey(node.nodeID, childId);
                if (!drawnEdges.Add(edgeKey))
                    continue;

                Vector3 end = GridToCanvas(canvasRect, childNode.gridX, childNode.gridY);
                Handles.color = lineColor;
                Handles.DrawAAPolyLine(3f, start, end);
                DrawDirectionMarker(start, end, lineColor);
            }
        }

        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawDirectionMarker(Vector3 start, Vector3 end, Color color)
    {
        Vector3 direction = (end - start).normalized;
        if (direction.sqrMagnitude < 0.01f)
            return;

        Handles.color = color;
        Vector3 center = Vector3.Lerp(start, end, 0.62f);
        Vector3 normal = new Vector3(-direction.y, direction.x, 0f);
        Vector3 tip = center + (direction * 7f);
        Vector3 left = center - (direction * 7f) + (normal * 5f);
        Vector3 right = center - (direction * 7f) - (normal * 5f);
        Handles.DrawAAConvexPolygon(tip, left, right);
    }

    private void DrawEmptySlots(Rect canvasRect)
    {
        if (isConnecting)
            return;

        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 1f, 1f, 0.26f);

        for (int x = viewMinX; x <= viewMaxX; x++)
        {
            for (int y = viewMinY; y <= viewMaxY; y++)
            {
                if (GetNodeAt(x, y) != null)
                    continue;

                Vector2 center = GridToCanvas(canvasRect, x, y);
                Rect buttonRect = new Rect(center.x - 13f, center.y - 13f, 26f, 26f);
                if (GUI.Button(buttonRect, "+", EditorStyles.miniButton))
                    CreateNode(x, y);
            }
        }

        GUI.backgroundColor = previousColor;
    }

    private void DrawNodes(Rect canvasRect)
    {
        if (selectedDatabase.allUpgrades == null)
            return;

        foreach (UpgradeNodeSO node in selectedDatabase.allUpgrades)
        {
            if (node == null)
                continue;

            Rect nodeRect = GetNodeRect(canvasRect, node.gridX, node.gridY);
            DrawNode(node, nodeRect);
        }
    }

    private void DrawNode(UpgradeNodeSO node, Rect rect)
    {
        Color previousColor = GUI.backgroundColor;
        if (node == selectedNode)
            GUI.backgroundColor = new Color(0.35f, 1f, 0.45f);
        else if (isConnecting)
            GUI.backgroundColor = new Color(1f, 0.86f, 0.35f);
        else
            GUI.backgroundColor = new Color(0.35f, 0.68f, 1f);

        string title = string.IsNullOrWhiteSpace(node.upgradeName) ? node.name : node.upgradeName;
        if (GUI.Button(rect, $"{title}\n[{node.gridX}, {node.gridY}]"))
        {
            if (isConnecting)
                CompleteConnection(node);
            else
                SelectNode(node);
        }

        GUI.backgroundColor = previousColor;
    }

    private void DrawConnectionPreview(Rect canvasRect)
    {
        if (!isConnecting || selectedNode == null || Event.current.type != EventType.Repaint)
            return;

        Handles.BeginGUI();
        Handles.color = Color.green;
        Handles.DrawAAPolyLine(3f, GridToCanvas(canvasRect, selectedNode.gridX, selectedNode.gridY), Event.current.mousePosition);
        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawSidePanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true));
        DrawCreateControls();
        GUILayout.Space(8f);

        if (selectedNode == null)
        {
            EditorGUILayout.HelpBox("Select a node to edit its data, move it on the grid, or create links.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        GUILayout.Label($"Selected: {GetDisplayName(selectedNode)}", EditorStyles.boldLabel);
        DrawPositionEditor();
        GUILayout.Space(8f);

        GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
        if (GUILayout.Button("Delete Selected Node"))
            DeleteNode(selectedNode);
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10f);
        DrawConnectionManager();
        GUILayout.Space(10f);
        DrawSelectedNodeInspector();

        EditorGUILayout.EndVertical();
    }

    private void DrawCreateControls()
    {
        GUILayout.Label("Create Node", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        createGridX = EditorGUILayout.IntField("X", createGridX);
        createGridY = EditorGUILayout.IntField("Y", createGridY);
        EditorGUILayout.EndHorizontal();

        bool occupied = GetNodeAt(createGridX, createGridY) != null;
        EditorGUI.BeginDisabledGroup(occupied);
        if (GUILayout.Button(occupied ? "Cell Occupied" : "Create At Coordinate"))
            CreateNode(createGridX, createGridY);
        EditorGUI.EndDisabledGroup();
    }

    private void DrawPositionEditor()
    {
        EditorGUILayout.LabelField("Grid Position", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int newX = EditorGUILayout.IntField("X", selectedNode.gridX);
        int newY = EditorGUILayout.IntField("Y", selectedNode.gridY);
        if (EditorGUI.EndChangeCheck())
            TryMoveNode(selectedNode, newX, newY);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Left")) TryMoveNode(selectedNode, selectedNode.gridX - 1, selectedNode.gridY);
        if (GUILayout.Button("Right")) TryMoveNode(selectedNode, selectedNode.gridX + 1, selectedNode.gridY);
        if (GUILayout.Button("Up")) TryMoveNode(selectedNode, selectedNode.gridX, selectedNode.gridY + 1);
        if (GUILayout.Button("Down")) TryMoveNode(selectedNode, selectedNode.gridX, selectedNode.gridY - 1);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawConnectionManager()
    {
        GUILayout.Label("Connections", EditorStyles.boldLabel);

        if (isConnecting)
        {
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Cancel Connection"))
                isConnecting = false;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("Click another node on the grid to connect it as an unlocked child.", MessageType.Info);
        }
        else
        {
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Start Connection From Selected"))
                isConnecting = true;
            GUI.backgroundColor = Color.white;
        }

        EnsureLinkLists(selectedNode);
        DrawOutgoingLinks();
        DrawRequiredParents();
    }

    private void DrawOutgoingLinks()
    {
        GUILayout.Space(6f);
        GUILayout.Label("Unlocks");

        if (selectedNode.nextNodes.Count == 0)
        {
            GUILayout.Label("None", EditorStyles.miniLabel);
            return;
        }

        foreach (UpgradeNodeSO child in new List<UpgradeNodeSO>(selectedNode.nextNodes))
        {
            if (child == null)
                continue;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"-> {GetDisplayName(child)}", EditorStyles.miniLabel);
            if (GUILayout.Button("X", GUILayout.Width(24f)))
            {
                DisconnectNodes(selectedNode, child);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawRequiredParents()
    {
        GUILayout.Space(6f);
        GUILayout.Label("Required Parents");

        if (selectedNode.requiredParents.Count == 0)
        {
            GUILayout.Label("None", EditorStyles.miniLabel);
            return;
        }

        foreach (UpgradeNodeSO parent in new List<UpgradeNodeSO>(selectedNode.requiredParents))
        {
            if (parent == null)
                continue;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"<- {GetDisplayName(parent)}", EditorStyles.miniLabel);
            if (GUILayout.Button("X", GUILayout.Width(24f)))
            {
                DisconnectNodes(parent, selectedNode);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawSelectedNodeInspector()
    {
        if (cachedNodeEditor == null || cachedNodeEditor.target != selectedNode)
        {
            ReleaseCachedNodeEditor();
            cachedNodeEditor = Editor.CreateEditor(selectedNode);
        }

        inspectorScrollPos = EditorGUILayout.BeginScrollView(inspectorScrollPos);
        cachedNodeEditor.OnInspectorGUI();
        EditorGUILayout.EndScrollView();
    }

    private void DrawRuntimePreviewTab()
    {
        PreviewLayoutSettings settings = ResolvePreviewLayoutSettings();
        PreviewLayout layout = BuildPreviewLayout(settings);

        EditorGUILayout.BeginHorizontal();
        DrawPreviewCanvasPanel(layout, settings);
        DrawSplitter();
        DrawPreviewSettingsPanel(layout, settings);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPreviewCanvasPanel(PreviewLayout layout, PreviewLayoutSettings settings)
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Reset View", EditorStyles.toolbarButton, GUILayout.Width(76f)))
        {
            previewPan = Vector2.zero;
            previewZoom = 1f;
        }

        if (GUILayout.Button("Frame All", EditorStyles.toolbarButton, GUILayout.Width(72f)))
            previewPan = Vector2.zero;

        GUILayout.Space(8f);
        GUILayout.Label("Zoom", GUILayout.Width(36f));
        previewZoom = GUILayout.HorizontalSlider(previewZoom, 0.35f, 2.5f, GUILayout.Width(120f));
        GUILayout.Label($"{previewZoom:0.00}x", GUILayout.Width(48f));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        Rect previewRect = GUILayoutUtility.GetRect(
            GUIContent.none,
            GUIStyle.none,
            GUILayout.ExpandWidth(true),
            GUILayout.ExpandHeight(true),
            GUILayout.MinHeight(420f));

        DrawRuntimePreview(previewRect, layout, settings, false);
        EditorGUILayout.EndVertical();
    }

    private void DrawPreviewSettingsPanel(PreviewLayout layout, PreviewLayoutSettings settings)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth), GUILayout.ExpandHeight(true));
        GUILayout.Label("Runtime Preview", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        previewUsePrefabSettings = EditorGUILayout.Toggle("Use Prefab Settings", previewUsePrefabSettings);
        if (EditorGUI.EndChangeCheck())
        {
            runtimePreviewHash = 0;
            Repaint();
        }

        using (new EditorGUI.DisabledScope(!previewUsePrefabSettings))
        {
            EnsurePreviewPrefabReference();
            EditorGUI.BeginChangeCheck();
            previewTreePrefab = (GameObject)EditorGUILayout.ObjectField(
                "UpgradeTree Prefab",
                previewTreePrefab,
                typeof(GameObject),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                runtimePreviewHash = 0;
                Repaint();
            }
        }

        if (previewUsePrefabSettings)
        {
            DrawPrefabBackedPreviewSettings(settings);
        }
        else
        {
            DrawOverridePreviewSettings();
        }

        GUILayout.Space(6f);
        previewSlotState = (LockType)EditorGUILayout.EnumPopup("Preview Slot State", previewSlotState);
        previewViewportSize = EditorGUILayout.Vector2Field("Viewport Size", previewViewportSize);
        previewShowGrid = EditorGUILayout.Toggle("Show Grid", previewShowGrid);
        previewShowContentRect = EditorGUILayout.Toggle("Show Content Rect", previewShowContentRect);
        previewShowViewportRect = EditorGUILayout.Toggle("Show Viewport Rect", previewShowViewportRect);
        previewShowNodeIds = EditorGUILayout.Toggle("Show Node IDs", previewShowNodeIds);

        if (GUILayout.Button("Rebuild Actual Preview"))
            runtimePreviewHash = 0;

        GUILayout.Space(8f);
        EditorGUILayout.LabelField("Resolved Layout", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Grid Cell", FormatVector(settings.gridCellSize));
        EditorGUILayout.LabelField("Content Size", FormatVector(layout.contentSize));
        EditorGUILayout.LabelField("Graph Min", layout.hasNode ? FormatVector(layout.graphMin) : "None");
        EditorGUILayout.LabelField("Graph Max", layout.hasNode ? FormatVector(layout.graphMax) : "None");
        EditorGUILayout.LabelField("Graph Center", layout.hasNode ? FormatVector(layout.graphCenter) : "None");

        GUILayout.Space(8f);
        EditorGUILayout.LabelField("Selected Node", EditorStyles.boldLabel);
        if (selectedNode == null)
        {
            EditorGUILayout.HelpBox("Click a node in the preview or graph tab to inspect its runtime position.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("Name", GetDisplayName(selectedNode));
            EditorGUILayout.LabelField("Grid", $"{selectedNode.gridX}, {selectedNode.gridY}");
            if (layout.nodePositions.TryGetValue(selectedNode, out Vector2 contentPosition))
                EditorGUILayout.LabelField("Content Pos", FormatVector(contentPosition));

            if (GUILayout.Button("Ping Asset"))
                EditorGUIUtility.PingObject(selectedNode);
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.HelpBox("Preview shows the runtime content-centered layout. Right-drag or middle-drag the preview to pan; mouse wheel changes zoom.", MessageType.None);
        EditorGUILayout.EndVertical();
    }

    private void DrawPrefabBackedPreviewSettings(PreviewLayoutSettings settings)
    {
        UpgradeTreeUI treeUI = ResolvePreviewTreeUI();
        if (treeUI == null)
        {
            EditorGUILayout.HelpBox("UpgradeTreeUI could not be found on the selected prefab.", MessageType.Warning);
            return;
        }

        SerializedObject serializedTree = new SerializedObject(treeUI);
        serializedTree.Update();

        EditorGUI.BeginChangeCheck();
        DrawObjectProperty(serializedTree, "slotPrefab", "Slot Prefab");
        DrawObjectProperty(serializedTree, "linePrefab", "Line Prefab");
        DrawVector2Property(serializedTree, "gridCellSize", "Grid Cell Size", Vector2.one);
        DrawVector2Property(serializedTree, "contentPadding", "Content Padding", Vector2.zero);
        DrawVector2Property(serializedTree, "minimumContentSize", "Minimum Content", Vector2.one);
        DrawFloatProperty(serializedTree, "lineThickness", "Line Thickness", 1f);

        if (EditorGUI.EndChangeCheck())
        {
            serializedTree.ApplyModifiedProperties();
            EditorUtility.SetDirty(treeUI);
            if (previewTreePrefab != null)
                EditorUtility.SetDirty(previewTreePrefab);

            runtimePreviewHash = 0;
            Repaint();
        }
        else
        {
            serializedTree.ApplyModifiedProperties();
        }

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.Vector2Field("Slot Size", settings.slotSize);
    }

    private void DrawOverridePreviewSettings()
    {
        EditorGUI.BeginChangeCheck();
        previewSlotPrefab = (GameObject)EditorGUILayout.ObjectField("Slot Prefab", previewSlotPrefab, typeof(GameObject), false);
        previewLinePrefab = (GameObject)EditorGUILayout.ObjectField("Line Prefab", previewLinePrefab, typeof(GameObject), false);
        previewGridCellSize = MaxVector(EditorGUILayout.Vector2Field("Grid Cell Size", previewGridCellSize), Vector2.one);
        previewContentPadding = MaxVector(EditorGUILayout.Vector2Field("Content Padding", previewContentPadding), Vector2.zero);
        previewMinimumContentSize = MaxVector(EditorGUILayout.Vector2Field("Minimum Content", previewMinimumContentSize), Vector2.one);
        previewSlotSize = MaxVector(EditorGUILayout.Vector2Field("Slot Size", previewSlotSize), Vector2.one);
        previewLineThickness = Mathf.Max(1f, EditorGUILayout.FloatField("Line Thickness", previewLineThickness));

        if (!EditorGUI.EndChangeCheck())
            return;

        runtimePreviewHash = 0;
        Repaint();
    }

    private void DrawObjectProperty(SerializedObject serializedObject, string propertyName, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private void DrawVector2Property(SerializedObject serializedObject, string propertyName, string label, Vector2 minimum)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.vector2Value = MaxVector(EditorGUILayout.Vector2Field(label, property.vector2Value), minimum);
    }

    private void DrawFloatProperty(SerializedObject serializedObject, string propertyName, string label, float minimum)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.floatValue = Mathf.Max(minimum, EditorGUILayout.FloatField(label, property.floatValue));
    }

    private PreviewLayoutSettings ResolvePreviewLayoutSettings()
    {
        PreviewLayoutSettings settings = new PreviewLayoutSettings
        {
            gridCellSize = previewGridCellSize,
            contentPadding = previewContentPadding,
            minimumContentSize = previewMinimumContentSize,
            viewportSize = previewViewportSize,
            slotSize = previewSlotSize,
            slotPrefab = previewSlotPrefab,
            linePrefab = previewLinePrefab,
            lineThickness = Mathf.Max(1f, previewLineThickness)
        };

        if (!previewUsePrefabSettings)
            return SanitizePreviewLayoutSettings(settings);

        EnsurePreviewPrefabReference();
        UpgradeTreeUI treeUI = ResolvePreviewTreeUI();
        if (treeUI == null)
            return settings;

        SerializedObject serializedTree = new SerializedObject(treeUI);
        settings.gridCellSize = ReadVector2(serializedTree, "gridCellSize", settings.gridCellSize);
        settings.contentPadding = ReadVector2(serializedTree, "contentPadding", settings.contentPadding);
        settings.minimumContentSize = ReadVector2(serializedTree, "minimumContentSize", settings.minimumContentSize);
        settings.lineThickness = Mathf.Max(1f, ReadFloat(serializedTree, "lineThickness", settings.lineThickness));

        SerializedProperty slotPrefabProperty = serializedTree.FindProperty("slotPrefab");
        settings.slotPrefab = slotPrefabProperty != null ? slotPrefabProperty.objectReferenceValue as GameObject : settings.slotPrefab;

        SerializedProperty linePrefabProperty = serializedTree.FindProperty("linePrefab");
        settings.linePrefab = linePrefabProperty != null ? linePrefabProperty.objectReferenceValue as GameObject : settings.linePrefab;

        if (previewSlotPrefab == null)
            previewSlotPrefab = settings.slotPrefab;

        if (previewLinePrefab == null)
            previewLinePrefab = settings.linePrefab;

        GameObject slotPrefab = settings.slotPrefab;
        RectTransform slotRect = slotPrefab != null ? slotPrefab.GetComponent<RectTransform>() : null;
        if (slotRect != null && slotRect.sizeDelta.x > 0f && slotRect.sizeDelta.y > 0f)
            settings.slotSize = slotRect.sizeDelta;

        return SanitizePreviewLayoutSettings(settings);
    }

    private PreviewLayoutSettings SanitizePreviewLayoutSettings(PreviewLayoutSettings settings)
    {
        settings.gridCellSize = MaxVector(settings.gridCellSize, Vector2.one);
        settings.contentPadding = MaxVector(settings.contentPadding, Vector2.zero);
        settings.minimumContentSize = MaxVector(settings.minimumContentSize, Vector2.one);
        settings.viewportSize = MaxVector(settings.viewportSize, Vector2.one);
        settings.slotSize = MaxVector(settings.slotSize, Vector2.one);
        settings.lineThickness = Mathf.Max(1f, settings.lineThickness);
        return settings;
    }

    private void EnsurePreviewPrefabReference()
    {
        if (previewTreePrefab != null)
            return;

        previewTreePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultUpgradeTreePrefabPath);
    }

    private UpgradeTreeUI ResolvePreviewTreeUI()
    {
        EnsurePreviewPrefabReference();
        return previewTreePrefab != null ? previewTreePrefab.GetComponentInChildren<UpgradeTreeUI>(true) : null;
    }

    private Vector2 ReadVector2(SerializedObject serializedObject, string propertyName, Vector2 fallback)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.vector2Value : fallback;
    }

    private float ReadFloat(SerializedObject serializedObject, string propertyName, float fallback)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null ? property.floatValue : fallback;
    }

    private PreviewLayout BuildPreviewLayout(PreviewLayoutSettings settings)
    {
        PreviewLayout layout = new PreviewLayout();
        List<UpgradeNodeSO> nodes = selectedDatabase != null ? selectedDatabase.allUpgrades : null;

        if (nodes == null)
        {
            layout.contentSize = Vector2.Max(settings.minimumContentSize, settings.viewportSize);
            return layout;
        }

        foreach (UpgradeNodeSO node in nodes)
        {
            if (node == null)
                continue;

            Vector2 rawPosition = node.GetUiPosition(settings.gridCellSize);
            if (!layout.hasNode)
            {
                layout.graphMin = rawPosition;
                layout.graphMax = rawPosition;
                layout.hasNode = true;
            }
            else
            {
                layout.graphMin = Vector2.Min(layout.graphMin, rawPosition);
                layout.graphMax = Vector2.Max(layout.graphMax, rawPosition);
            }
        }

        if (!layout.hasNode)
        {
            layout.contentSize = Vector2.Max(settings.minimumContentSize, settings.viewportSize);
            return layout;
        }

        Vector2 graphSize = layout.graphMax - layout.graphMin;
        Vector2 requiredSize = graphSize + (settings.contentPadding * 2f);
        layout.contentSize = Vector2.Max(Vector2.Max(requiredSize, settings.minimumContentSize), settings.viewportSize);
        layout.graphCenter = (layout.graphMin + layout.graphMax) * 0.5f;

        foreach (UpgradeNodeSO node in nodes)
        {
            if (node == null)
                continue;

            layout.nodePositions[node] = node.GetUiPosition(settings.gridCellSize) - layout.graphCenter;
        }

        return layout;
    }

    private void DrawRuntimePreview(Rect rect, PreviewLayout layout, PreviewLayoutSettings settings, bool enableEditing)
    {
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.14f, 1f));
        HandlePreviewInput(rect);

        float scale = CalculatePreviewScale(rect, layout.contentSize) * previewZoom;
        Vector2 origin = rect.center + previewPan;
        Rect contentRect = CenteredRect(origin, layout.contentSize * scale);

        if (!layout.hasNode)
        {
            DrawCenteredLabel(rect, "No upgrade nodes to preview.");
            if (previewShowGrid)
                DrawPreviewGrid(contentRect, layout, settings, origin, scale);

            if (previewShowContentRect)
                DrawPreviewBorder(contentRect, new Color(0.42f, 0.46f, 0.55f, 1f));

            if (previewShowViewportRect)
                DrawPreviewViewport(settings, origin, scale);

            if (enableEditing)
                DrawVisualEditorOverlay(rect, layout, settings, origin, scale);

            DrawPreviewOverlay(rect, layout, settings, scale);
            DrawPreviewBorder(rect, new Color(0.42f, 0.42f, 0.46f, 1f));
            return;
        }

        bool canRenderActualPreview = settings.slotPrefab != null && settings.linePrefab != null;
        if (canRenderActualPreview && Event.current.type == EventType.Repaint)
        {
            Texture renderedPreview = RenderRuntimePrefabPreview(rect, layout, settings, scale);
            if (renderedPreview != null)
                GUI.DrawTexture(rect, renderedPreview, ScaleMode.StretchToFill, false);
        }
        else if (!canRenderActualPreview)
        {
            DrawCenteredLabel(rect, "Slot or line prefab is missing.");
        }

        if (previewShowGrid)
            DrawPreviewGrid(contentRect, layout, settings, origin, scale);

        if (previewShowContentRect)
            DrawPreviewBorder(contentRect, new Color(0.42f, 0.46f, 0.55f, 1f));

        if (previewShowViewportRect)
            DrawPreviewViewport(settings, origin, scale);

        if (enableEditing)
            DrawVisualEditorOverlay(rect, layout, settings, origin, scale);

        DrawPreviewNodeHitAreas(layout, settings, origin, scale, enableEditing);
        if (previewShowNodeIds)
            DrawPreviewNodeLabels(layout, settings, origin, scale);

        if (enableEditing)
            DrawVisualConnectionPreview(layout, settings, origin, scale);

        DrawPreviewOverlay(rect, layout, settings, scale);
        DrawPreviewBorder(rect, new Color(0.42f, 0.42f, 0.46f, 1f));
    }

    private Texture RenderRuntimePrefabPreview(Rect rect, PreviewLayout layout, PreviewLayoutSettings settings, float scale)
    {
        if (!EnsureRuntimePrefabPreview(layout, settings))
            return null;

        runtimePreviewUtility.BeginPreview(rect, GUIStyle.none);

        Camera camera = runtimePreviewUtility.camera;
        camera.clearFlags = CameraClearFlags.Color;
        camera.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(1f, rect.height / Mathf.Max(0.001f, scale * 2f));
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 5000f;
        camera.cullingMask = ~0;
        camera.transform.position = new Vector3(-previewPan.x / scale, previewPan.y / scale, -1000f);
        camera.transform.rotation = Quaternion.identity;

        if (runtimePreviewUtility.lights != null)
        {
            for (int i = 0; i < runtimePreviewUtility.lights.Length; i++)
            {
                if (runtimePreviewUtility.lights[i] != null)
                    runtimePreviewUtility.lights[i].intensity = 0f;
            }
        }

        Canvas.ForceUpdateCanvases();
        camera.Render();
        return runtimePreviewUtility.EndPreview();
    }

    private bool EnsureRuntimePrefabPreview(PreviewLayout layout, PreviewLayoutSettings settings)
    {
        if (settings.slotPrefab == null || settings.linePrefab == null)
        {
            ReleaseRuntimePrefabPreview();
            return false;
        }

        int hash = CalculateRuntimePreviewHash(layout, settings);
        if (runtimePreviewUtility != null && runtimePreviewRoot != null && runtimePreviewHash == hash)
            return true;

        RebuildRuntimePrefabPreview(layout, settings, hash);
        return runtimePreviewUtility != null && runtimePreviewRoot != null;
    }

    private void RebuildRuntimePrefabPreview(PreviewLayout layout, PreviewLayoutSettings settings, int hash)
    {
        ReleaseRuntimePrefabPreview();

        runtimePreviewUtility = new PreviewRenderUtility();
        runtimePreviewRoot = CreatePreviewCanvasRoot(layout.contentSize);

        RectTransform contentRoot = CreatePreviewRect("Content", runtimePreviewRoot.transform as RectTransform, layout.contentSize);
        RectTransform lineParent = CreatePreviewRect("Lines", contentRoot, layout.contentSize);
        RectTransform slotParent = CreatePreviewRect("Slots", contentRoot, layout.contentSize);

        CreatePreviewLines(lineParent, layout, settings);
        CreatePreviewSlots(slotParent, layout, settings);

        SetPreviewHideFlags(runtimePreviewRoot);
        Canvas.ForceUpdateCanvases();
        runtimePreviewUtility.AddSingleGO(runtimePreviewRoot);
        runtimePreviewHash = hash;
    }

    private GameObject CreatePreviewCanvasRoot(Vector2 contentSize)
    {
        GameObject root = new GameObject("UpgradeTreeEditor_RuntimePreview", typeof(RectTransform), typeof(Canvas));
        RectTransform rootRect = root.transform as RectTransform;
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = contentSize;
        rootRect.position = Vector3.zero;
        rootRect.localScale = Vector3.one;

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 0;

        return root;
    }

    private RectTransform CreatePreviewRect(string objectName, RectTransform parent, Vector2 size)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = child.transform as RectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return rect;
    }

    private void CreatePreviewLines(RectTransform lineParent, PreviewLayout layout, PreviewLayoutSettings settings)
    {
        if (selectedDatabase == null || selectedDatabase.allUpgrades == null)
            return;

        HashSet<string> drawnEdges = new HashSet<string>();
        foreach (UpgradeNodeSO node in selectedDatabase.allUpgrades)
        {
            if (node == null || node.unlockedNodeIDs == null || !layout.nodePositions.TryGetValue(node, out Vector2 startPosition))
                continue;

            foreach (int childId in node.unlockedNodeIDs)
            {
                UpgradeNodeSO childNode = FindNodeById(childId);
                if (childNode == null || !layout.nodePositions.TryGetValue(childNode, out Vector2 endPosition))
                    continue;

                string edgeKey = GetEdgeKey(node.nodeID, childId);
                if (!drawnEdges.Add(edgeKey))
                    continue;

                Vector2 direction = endPosition - startPosition;
                float distance = direction.magnitude;
                if (distance < 0.1f)
                    continue;

                GameObject line = InstantiatePreviewPrefab(settings.linePrefab, lineParent);
                RectTransform lineRect = line != null ? line.GetComponent<RectTransform>() : null;
                if (lineRect == null)
                    continue;

                lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                lineRect.pivot = new Vector2(0f, 0.5f);
                lineRect.anchoredPosition = startPosition;
                lineRect.sizeDelta = new Vector2(distance, settings.lineThickness);
                lineRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                lineRect.localScale = Vector3.one;
            }
        }
    }

    private void CreatePreviewSlots(RectTransform slotParent, PreviewLayout layout, PreviewLayoutSettings settings)
    {
        if (selectedDatabase == null || selectedDatabase.allUpgrades == null)
            return;

        foreach (UpgradeNodeSO node in selectedDatabase.allUpgrades)
        {
            if (node == null || !layout.nodePositions.TryGetValue(node, out Vector2 contentPosition))
                continue;

            GameObject slotObject = InstantiatePreviewPrefab(settings.slotPrefab, slotParent);
            RectTransform slotRect = slotObject != null ? slotObject.GetComponent<RectTransform>() : null;
            UpgradeSlotUI slotUI = slotObject != null ? slotObject.GetComponent<UpgradeSlotUI>() : null;
            if (slotRect == null || slotUI == null)
                continue;

            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.anchoredPosition = contentPosition;
            slotRect.localRotation = Quaternion.identity;
            slotRect.localScale = Vector3.one;

            slotUI.assignedNode = node;
            slotUI.RefreshUI();
            ApplyPreviewSlotState(slotUI, node);

            foreach (TMPro.TMP_Text text in slotObject.GetComponentsInChildren<TMPro.TMP_Text>(true))
                text.ForceMeshUpdate();
        }
    }

    private GameObject InstantiatePreviewPrefab(GameObject prefab, Transform parent)
    {
        if (prefab == null)
            return null;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            instance = Instantiate(prefab);

        instance.transform.SetParent(parent, false);
        return instance;
    }

    private void ApplyPreviewSlotState(UpgradeSlotUI slotUI, UpgradeNodeSO node)
    {
        if (slotUI == null || node == null)
            return;

        if (slotUI.priceText != null)
            slotUI.priceText.text = node.price.ToString();

        if (slotUI.iconImage != null && node.icon != null)
            slotUI.iconImage.sprite = node.icon;

        switch (previewSlotState)
        {
            case LockType.Purchased:
                if (slotUI.buyButton != null) slotUI.buyButton.interactable = false;
                if (slotUI.lockIcon != null) slotUI.lockIcon.enabled = false;
                if (slotUI.purchasedCheckMark != null) slotUI.purchasedCheckMark.SetActive(true);
                if (slotUI.iconImage != null) slotUI.iconImage.color = Color.gray;
                break;

            case LockType.UnLocked:
                if (slotUI.buyButton != null) slotUI.buyButton.interactable = true;
                if (slotUI.lockIcon != null) slotUI.lockIcon.enabled = false;
                if (slotUI.purchasedCheckMark != null) slotUI.purchasedCheckMark.SetActive(false);
                if (slotUI.iconImage != null) slotUI.iconImage.color = Color.white;
                break;

            default:
                if (slotUI.buyButton != null) slotUI.buyButton.interactable = false;
                if (slotUI.lockIcon != null) slotUI.lockIcon.enabled = true;
                if (slotUI.purchasedCheckMark != null) slotUI.purchasedCheckMark.SetActive(false);
                if (slotUI.iconImage != null) slotUI.iconImage.color = new Color(0.3f, 0.3f, 0.3f);
                break;
        }
    }

    private void SetPreviewHideFlags(GameObject target)
    {
        if (target == null)
            return;

        target.hideFlags = HideFlags.HideAndDontSave;
        foreach (Transform child in target.transform)
            SetPreviewHideFlags(child.gameObject);
    }

    private void ReleaseRuntimePrefabPreview()
    {
        if (runtimePreviewRoot != null)
        {
            DestroyImmediate(runtimePreviewRoot);
            runtimePreviewRoot = null;
        }

        if (runtimePreviewUtility != null)
        {
            runtimePreviewUtility.Cleanup();
            runtimePreviewUtility = null;
        }

        runtimePreviewHash = 0;
    }

    private int CalculateRuntimePreviewHash(PreviewLayout layout, PreviewLayoutSettings settings)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + GetInstanceHash(selectedDatabase);
            hash = (hash * 31) + GetInstanceHash(settings.slotPrefab);
            hash = (hash * 31) + GetInstanceHash(settings.linePrefab);
            hash = (hash * 31) + HashVector(settings.gridCellSize);
            hash = (hash * 31) + HashVector(settings.contentPadding);
            hash = (hash * 31) + HashVector(settings.minimumContentSize);
            hash = (hash * 31) + HashVector(settings.viewportSize);
            hash = (hash * 31) + HashVector(settings.slotSize);
            hash = (hash * 31) + Mathf.RoundToInt(settings.lineThickness * 100f);
            hash = (hash * 31) + (int)previewSlotState;
            hash = (hash * 31) + HashVector(layout.contentSize);

            if (selectedDatabase?.allUpgrades != null)
            {
                foreach (UpgradeNodeSO node in selectedDatabase.allUpgrades)
                {
                    if (node == null)
                        continue;

                    hash = (hash * 31) + GetInstanceHash(node);
                    hash = (hash * 31) + node.nodeID;
                    hash = (hash * 31) + node.gridX;
                    hash = (hash * 31) + node.gridY;
                    hash = (hash * 31) + node.price;
                    hash = (hash * 31) + GetInstanceHash(node.icon);

                    if (node.unlockedNodeIDs == null)
                        continue;

                    for (int i = 0; i < node.unlockedNodeIDs.Count; i++)
                        hash = (hash * 31) + node.unlockedNodeIDs[i];
                }
            }

            return hash;
        }
    }

    private float CalculatePreviewScale(Rect rect, Vector2 contentSize)
    {
        float widthScale = (rect.width - 48f) / Mathf.Max(1f, contentSize.x);
        float heightScale = (rect.height - 48f) / Mathf.Max(1f, contentSize.y);
        return Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.05f, 4f);
    }

    private void DrawPreviewGrid(Rect contentRect, PreviewLayout layout, PreviewLayoutSettings settings, Vector2 origin, float scale)
    {
        Vector2 halfSize = layout.contentSize * 0.5f;
        int minX = Mathf.FloorToInt((-halfSize.x + layout.graphCenter.x) / settings.gridCellSize.x);
        int maxX = Mathf.CeilToInt((halfSize.x + layout.graphCenter.x) / settings.gridCellSize.x);
        int minY = Mathf.FloorToInt((-halfSize.y + layout.graphCenter.y) / settings.gridCellSize.y);
        int maxY = Mathf.CeilToInt((halfSize.y + layout.graphCenter.y) / settings.gridCellSize.y);

        Handles.BeginGUI();
        for (int x = minX; x <= maxX; x++)
        {
            float contentX = (x * settings.gridCellSize.x) - layout.graphCenter.x;
            Vector2 start = ContentToPreview(new Vector2(contentX, -halfSize.y), origin, scale);
            Vector2 end = ContentToPreview(new Vector2(contentX, halfSize.y), origin, scale);
            Handles.color = x == 0 ? new Color(0.4f, 0.65f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.08f);
            Handles.DrawLine(ToVector3(start), ToVector3(end));
        }

        for (int y = minY; y <= maxY; y++)
        {
            float contentY = (y * settings.gridCellSize.y) - layout.graphCenter.y;
            Vector2 start = ContentToPreview(new Vector2(-halfSize.x, contentY), origin, scale);
            Vector2 end = ContentToPreview(new Vector2(halfSize.x, contentY), origin, scale);
            Handles.color = y == 0 ? new Color(0.4f, 0.65f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.08f);
            Handles.DrawLine(ToVector3(start), ToVector3(end));
        }
        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawVisualEditorOverlay(Rect rect, PreviewLayout layout, PreviewLayoutSettings settings, Vector2 origin, float scale)
    {
        if (!isConnecting)
            DrawEmptyVisualEditorGrid(rect, layout, settings, origin, scale);

        string modeText = isConnecting
            ? "Connection Mode: click a target node. Esc cancels."
            : "Edit Mode: click a node to select, click + to create, right-drag to pan.";

        Rect modeRect = new Rect(rect.x + 10f, rect.yMax - 32f, 420f, 22f);
        EditorGUI.DrawRect(modeRect, new Color(0f, 0f, 0f, 0.48f));
        GUI.Label(new Rect(modeRect.x + 8f, modeRect.y + 2f, modeRect.width - 16f, 18f), modeText, EditorStyles.whiteMiniLabel);
    }

    private void DrawEmptyVisualEditorGrid(Rect rect, PreviewLayout layout, PreviewLayoutSettings settings, Vector2 origin, float scale)
    {
        if (selectedDatabase == null)
            return;

        Color previousColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 1f, 1f, 0.65f);

        for (int x = viewMinX; x <= viewMaxX; x++)
        {
            for (int y = viewMinY; y <= viewMaxY; y++)
            {
                if (GetNodeAt(x, y) != null)
                    continue;

                Vector2 center = ContentToPreview(GridToContentPosition(x, y, layout, settings), origin, scale);
                if (!rect.Contains(center))
                    continue;

                float size = Mathf.Clamp(22f * previewZoom, 16f, 28f);
                Rect buttonRect = CenteredRect(center, new Vector2(size, size));
                if (GUI.Button(buttonRect, "+", EditorStyles.miniButton))
                    CreateNode(x, y);
            }
        }

        GUI.backgroundColor = previousColor;
    }

    private void DrawVisualConnectionPreview(PreviewLayout layout, PreviewLayoutSettings settings, Vector2 origin, float scale)
    {
        if (!isConnecting || selectedNode == null)
            return;

        if (Event.current.type != EventType.Repaint)
            return;

        if (!layout.nodePositions.TryGetValue(selectedNode, out Vector2 startPosition))
            startPosition = GridToContentPosition(selectedNode.gridX, selectedNode.gridY, layout, settings);

        Handles.BeginGUI();
        Handles.color = Color.green;
        Handles.DrawAAPolyLine(3f, ToVector3(ContentToPreview(startPosition, origin, scale)), ToVector3(Event.current.mousePosition));
        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private void DrawPreviewNodeHitAreas(PreviewLayout layout, PreviewLayoutSettings settings, Vector2 origin, float scale, bool enableEditing)
    {
        if (selectedDatabase == null || selectedDatabase.allUpgrades == null)
            return;

        foreach (UpgradeNodeSO node in selectedDatabase.allUpgrades)
        {
            if (node == null || !layout.nodePositions.TryGetValue(node, out Vector2 contentPosition))
                continue;

            Vector2 nodeCenter = ContentToPreview(contentPosition, origin, scale);
            Rect nodeRect = CenteredRect(nodeCenter, settings.slotSize * scale);

            if (GUI.Button(nodeRect, GUIContent.none, GUIStyle.none))
            {
                if (enableEditing && isConnecting)
                    CompleteConnection(node);
                else
                    SelectNode(node);
            }

            if (node == selectedNode)
                DrawPreviewBorder(nodeRect, new Color(0.35f, 1f, 0.45f, 1f));
        }
    }

    private void DrawPreviewNodeLabels(PreviewLayout layout, PreviewLayoutSettings settings, Vector2 origin, float scale)
    {
        if (selectedDatabase == null || selectedDatabase.allUpgrades == null)
            return;

        foreach (UpgradeNodeSO node in selectedDatabase.allUpgrades)
        {
            if (node == null || !layout.nodePositions.TryGetValue(node, out Vector2 contentPosition))
                continue;

            Vector2 nodeCenter = ContentToPreview(contentPosition, origin, scale);
            Vector2 nodeSize = settings.slotSize * scale;
            Rect labelRect = new Rect(nodeCenter.x - 90f, nodeCenter.y + (nodeSize.y * 0.5f) + 4f, 180f, 32f);
            GUI.Label(labelRect, $"{node.nodeID}\n[{node.gridX}, {node.gridY}]", EditorStyles.whiteMiniLabel);
        }
    }

    private void DrawPreviewViewport(PreviewLayoutSettings settings, Vector2 origin, float scale)
    {
        Rect viewportRect = CenteredRect(origin, settings.viewportSize * scale);
        DrawPreviewBorder(viewportRect, new Color(1f, 0.82f, 0.28f, 1f));
        GUI.Label(new Rect(viewportRect.x + 6f, viewportRect.y + 4f, 160f, 18f), "Initial Viewport", EditorStyles.whiteMiniLabel);
    }

    private void DrawPreviewOverlay(Rect rect, PreviewLayout layout, PreviewLayoutSettings settings, float scale)
    {
        Rect overlayRect = new Rect(rect.x + 10f, rect.y + 10f, 300f, 88f);
        EditorGUI.DrawRect(overlayRect, new Color(0f, 0f, 0f, 0.55f));
        GUI.Label(new Rect(overlayRect.x + 8f, overlayRect.y + 6f, 284f, 18f), $"Content {FormatVector(layout.contentSize)}", EditorStyles.whiteMiniLabel);
        GUI.Label(new Rect(overlayRect.x + 8f, overlayRect.y + 24f, 284f, 18f), $"Viewport {FormatVector(settings.viewportSize)}", EditorStyles.whiteMiniLabel);
        GUI.Label(new Rect(overlayRect.x + 8f, overlayRect.y + 42f, 284f, 18f), $"Cell {FormatVector(settings.gridCellSize)}", EditorStyles.whiteMiniLabel);
        GUI.Label(new Rect(overlayRect.x + 8f, overlayRect.y + 60f, 284f, 18f), $"Scale {scale:0.000}", EditorStyles.whiteMiniLabel);
    }

    private void HandlePreviewInput(Rect rect)
    {
        Event e = Event.current;
        if (!rect.Contains(e.mousePosition))
            return;

        if (e.type == EventType.ScrollWheel)
        {
            float zoomDelta = -e.delta.y * 0.05f;
            previewZoom = Mathf.Clamp(previewZoom + zoomDelta, 0.35f, 2.5f);
            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
        {
            previewPan += e.delta;
            e.Use();
            Repaint();
        }
    }

    private Vector2 ContentToPreview(Vector2 contentPosition, Vector2 origin, float scale)
    {
        return new Vector2(
            origin.x + (contentPosition.x * scale),
            origin.y - (contentPosition.y * scale));
    }

    private Vector2 GridToContentPosition(int gridX, int gridY, PreviewLayout layout, PreviewLayoutSettings settings)
    {
        Vector2 rawPosition = new Vector2(gridX * settings.gridCellSize.x, gridY * settings.gridCellSize.y);
        return rawPosition - layout.graphCenter;
    }

    private Rect CenteredRect(Vector2 center, Vector2 size)
    {
        return new Rect(center.x - (size.x * 0.5f), center.y - (size.y * 0.5f), size.x, size.y);
    }

    private Vector2 MaxVector(Vector2 value, Vector2 minimum)
    {
        return new Vector2(Mathf.Max(value.x, minimum.x), Mathf.Max(value.y, minimum.y));
    }

    private int HashVector(Vector2 value)
    {
        unchecked
        {
            int hash = 17;
            hash = (hash * 31) + Mathf.RoundToInt(value.x * 100f);
            hash = (hash * 31) + Mathf.RoundToInt(value.y * 100f);
            return hash;
        }
    }

    private int GetInstanceHash(Object target)
    {
        return target != null ? target.GetInstanceID() : 0;
    }

    private Vector3 ToVector3(Vector2 value)
    {
        return new Vector3(value.x, value.y, 0f);
    }

    private void DrawCenteredLabel(Rect rect, string text)
    {
        GUI.Label(rect, text, new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        });
    }

    private void DrawPreviewBorder(Rect rect, Color color)
    {
        Handles.BeginGUI();
        Handles.color = color;
        Handles.DrawAAPolyLine(2f,
            new Vector3(rect.xMin, rect.yMin),
            new Vector3(rect.xMax, rect.yMin),
            new Vector3(rect.xMax, rect.yMax),
            new Vector3(rect.xMin, rect.yMax),
            new Vector3(rect.xMin, rect.yMin));
        Handles.color = Color.white;
        Handles.EndGUI();
    }

    private string FormatVector(Vector2 value)
    {
        return $"{value.x:0.#}, {value.y:0.#}";
    }

    private Rect GetNodeRect(Rect canvasRect, int gridX, int gridY)
    {
        Vector2 center = GridToCanvas(canvasRect, gridX, gridY);
        return new Rect(center.x - (NodeWidth * 0.5f), center.y - (NodeHeight * 0.5f), NodeWidth, NodeHeight);
    }

    private Vector2 GridToCanvas(Rect canvasRect, int gridX, int gridY)
    {
        float x = canvasRect.x + CanvasPadding + ((gridX - viewMinX) * CellSize.x);
        float y = canvasRect.y + CanvasPadding + ((viewMaxY - gridY) * CellSize.y);
        return new Vector2(x, y);
    }

    private UpgradeNodeSO GetNodeAt(int gridX, int gridY)
    {
        if (selectedDatabase == null || selectedDatabase.allUpgrades == null)
            return null;

        return selectedDatabase.allUpgrades.Find(node => node != null && node.gridX == gridX && node.gridY == gridY);
    }

    private UpgradeNodeSO FindNodeById(int nodeId)
    {
        if (selectedDatabase == null || selectedDatabase.allUpgrades == null)
            return null;

        return selectedDatabase.allUpgrades.Find(node => node != null && node.nodeID == nodeId);
    }

    private void CreateNode(int gridX, int gridY)
    {
        if (selectedDatabase == null || GetNodeAt(gridX, gridY) != null)
            return;

        EnsureNodeFolder();

        UpgradeNodeSO newNode = CreateInstance<UpgradeNodeSO>();
        string fileName = $"Node_{System.DateTime.Now.Ticks}";
        newNode.name = fileName;
        newNode.nodeID = Animator.StringToHash(fileName);
        newNode.gridX = gridX;
        newNode.gridY = gridY;
        newNode.upgradeName = $"Upgrade {gridX},{gridY}";
        newNode.price = 100;

        string path = AssetDatabase.GenerateUniqueAssetPath($"{NodeFolder}/{fileName}.asset");
        AssetDatabase.CreateAsset(newNode, path);
        Undo.RegisterCreatedObjectUndo(newNode, "Create Upgrade Node");

        if (selectedDatabase.allUpgrades == null)
            selectedDatabase.allUpgrades = new List<UpgradeNodeSO>();

        Undo.RecordObject(selectedDatabase, "Add Upgrade Node");
        selectedDatabase.allUpgrades.Add(newNode);
        EditorUtility.SetDirty(selectedDatabase);
        AssetDatabase.SaveAssets();
        SelectNode(newNode);
    }

    private void DeleteNode(UpgradeNodeSO target)
    {
        if (target == null || selectedDatabase == null || selectedDatabase.allUpgrades == null)
            return;

        if (!EditorUtility.DisplayDialog("Delete Upgrade Node", $"Delete {GetDisplayName(target)}?", "Delete", "Cancel"))
            return;

        foreach (UpgradeNodeSO node in selectedDatabase.allUpgrades)
        {
            if (node == null)
                continue;

            Undo.RecordObject(node, "Remove Upgrade Links");
            EnsureLinkLists(node);
            node.nextNodes.Remove(target);
            node.requiredParents.Remove(target);
            node.unlockedNodeIDs.RemoveAll(id => id == target.nodeID);
            node.requiredParentIDs.RemoveAll(id => id == target.nodeID);
            EditorUtility.SetDirty(node);
        }

        Undo.RecordObject(selectedDatabase, "Delete Upgrade Node");
        selectedDatabase.allUpgrades.Remove(target);
        EditorUtility.SetDirty(selectedDatabase);

        string path = AssetDatabase.GetAssetPath(target);
        selectedNode = null;
        isConnecting = false;
        ReleaseCachedNodeEditor();

        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
    }

    private void SelectNode(UpgradeNodeSO node)
    {
        selectedNode = node;
        isConnecting = false;
        Selection.activeObject = node;
        GUI.FocusControl(null);
        ReleaseCachedNodeEditor();
    }

    private void CompleteConnection(UpgradeNodeSO target)
    {
        if (selectedNode == null || target == null || selectedNode == target)
            return;

        EnsureLinkLists(selectedNode);
        EnsureLinkLists(target);
        Undo.RecordObjects(new UnityEngine.Object[] { selectedNode, target }, "Connect Upgrade Nodes");

        if (!selectedNode.nextNodes.Contains(target))
            selectedNode.nextNodes.Add(target);

        if (!selectedNode.unlockedNodeIDs.Contains(target.nodeID))
            selectedNode.unlockedNodeIDs.Add(target.nodeID);

        if (!target.requiredParents.Contains(selectedNode))
            target.requiredParents.Add(selectedNode);

        if (!target.requiredParentIDs.Contains(selectedNode.nodeID))
            target.requiredParentIDs.Add(selectedNode.nodeID);

        EditorUtility.SetDirty(selectedNode);
        EditorUtility.SetDirty(target);
        isConnecting = false;
        AssetDatabase.SaveAssets();
    }

    private void DisconnectNodes(UpgradeNodeSO parent, UpgradeNodeSO child)
    {
        if (parent == null || child == null)
            return;

        EnsureLinkLists(parent);
        EnsureLinkLists(child);
        Undo.RecordObjects(new UnityEngine.Object[] { parent, child }, "Disconnect Upgrade Nodes");

        parent.nextNodes.Remove(child);
        parent.unlockedNodeIDs.RemoveAll(id => id == child.nodeID);
        child.requiredParents.Remove(parent);
        child.requiredParentIDs.RemoveAll(id => id == parent.nodeID);

        EditorUtility.SetDirty(parent);
        EditorUtility.SetDirty(child);
        AssetDatabase.SaveAssets();
    }

    private bool TryMoveNode(UpgradeNodeSO node, int newX, int newY)
    {
        if (node == null)
            return false;

        UpgradeNodeSO occupiedNode = GetNodeAt(newX, newY);
        if (occupiedNode != null && occupiedNode != node)
        {
            ShowNotification(new GUIContent("Target cell is occupied."));
            return false;
        }

        Undo.RecordObject(node, "Move Upgrade Node");
        node.gridX = newX;
        node.gridY = newY;
        EditorUtility.SetDirty(node);
        ExpandViewToIncludeNodes();
        return true;
    }

    private void EnsureNodeFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        if (!AssetDatabase.IsValidFolder("Assets/Resources/Upgrades"))
            AssetDatabase.CreateFolder("Assets/Resources", "Upgrades");

        if (!AssetDatabase.IsValidFolder(NodeFolder))
            AssetDatabase.CreateFolder("Assets/Resources/Upgrades", "Nodes");
    }

    private void EnsureLinkLists(UpgradeNodeSO node)
    {
        if (node == null)
            return;

        node.nextNodes ??= new List<UpgradeNodeSO>();
        node.requiredParents ??= new List<UpgradeNodeSO>();
        node.unlockedNodeIDs ??= new List<int>();
        node.requiredParentIDs ??= new List<int>();
    }

    private void NormalizeViewBounds()
    {
        if (viewMinX > viewMaxX)
        {
            int temp = viewMinX;
            viewMinX = viewMaxX;
            viewMaxX = temp;
        }

        if (viewMinY > viewMaxY)
        {
            int temp = viewMinY;
            viewMinY = viewMaxY;
            viewMaxY = temp;
        }

        if (viewMinX == viewMaxX)
            viewMaxX++;

        if (viewMinY == viewMaxY)
            viewMaxY++;
    }

    private void ExpandViewToIncludeNodes()
    {
        if (selectedDatabase == null || selectedDatabase.allUpgrades == null)
            return;

        foreach (UpgradeNodeSO node in selectedDatabase.allUpgrades)
        {
            if (node == null)
                continue;

            viewMinX = Mathf.Min(viewMinX, node.gridX - 2);
            viewMaxX = Mathf.Max(viewMaxX, node.gridX + 2);
            viewMinY = Mathf.Min(viewMinY, node.gridY - 2);
            viewMaxY = Mathf.Max(viewMaxY, node.gridY + 2);
        }
    }

    private void FrameAllNodes()
    {
        if (selectedDatabase == null || selectedDatabase.allUpgrades == null || selectedDatabase.allUpgrades.Count == 0)
            return;

        bool hasNode = false;
        int minX = 0;
        int maxX = 0;
        int minY = 0;
        int maxY = 0;

        foreach (UpgradeNodeSO node in selectedDatabase.allUpgrades)
        {
            if (node == null)
                continue;

            if (!hasNode)
            {
                minX = maxX = node.gridX;
                minY = maxY = node.gridY;
                hasNode = true;
            }
            else
            {
                minX = Mathf.Min(minX, node.gridX);
                maxX = Mathf.Max(maxX, node.gridX);
                minY = Mathf.Min(minY, node.gridY);
                maxY = Mathf.Max(maxY, node.gridY);
            }
        }

        if (!hasNode)
            return;

        viewMinX = minX - 4;
        viewMaxX = maxX + 4;
        viewMinY = minY - 4;
        viewMaxY = maxY + 4;
        graphScrollPos = Vector2.zero;
    }

    private string GetDisplayName(UpgradeNodeSO node)
    {
        if (node == null)
            return "None";

        return string.IsNullOrWhiteSpace(node.upgradeName) ? node.name : node.upgradeName;
    }

    private string GetEdgeKey(int fromId, int toId)
    {
        return fromId < toId ? $"{fromId}:{toId}" : $"{toId}:{fromId}";
    }

    private void ProcessEvents(Event e)
    {
        if (e.type != EventType.KeyDown)
            return;

        if (e.keyCode == KeyCode.Escape)
        {
            isConnecting = false;
            e.Use();
            Repaint();
            return;
        }

        if (selectedNode == null || EditorGUIUtility.editingTextField)
            return;

        int step = e.shift ? 5 : 1;
        switch (e.keyCode)
        {
            case KeyCode.LeftArrow:
                TryMoveNode(selectedNode, selectedNode.gridX - step, selectedNode.gridY);
                e.Use();
                break;
            case KeyCode.RightArrow:
                TryMoveNode(selectedNode, selectedNode.gridX + step, selectedNode.gridY);
                e.Use();
                break;
            case KeyCode.UpArrow:
                TryMoveNode(selectedNode, selectedNode.gridX, selectedNode.gridY + step);
                e.Use();
                break;
            case KeyCode.DownArrow:
                TryMoveNode(selectedNode, selectedNode.gridX, selectedNode.gridY - step);
                e.Use();
                break;
        }
    }

    private void ReleaseCachedNodeEditor()
    {
        if (cachedNodeEditor == null)
            return;

        DestroyImmediate(cachedNodeEditor);
        cachedNodeEditor = null;
    }
}
#endif

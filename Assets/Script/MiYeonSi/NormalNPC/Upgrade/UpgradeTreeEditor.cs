#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class UpgradeTreeEditor : EditorWindow
{
    // --- 설정 변수 ---
    private const float SIDEBAR_WIDTH = 340f;
    private const float NODE_WIDTH = 120f;
    private const float NODE_HEIGHT = 80f;
    private const float COL_WIDTH = 160f;
    private const float ROW_HEIGHT = 140f;
    private const float BOTTOM_MARGIN = 100f;
    private const int EXTRA_ROWS = 2;

    // --- 상태 변수 ---
    private UpgradeDatabase selectedDatabase;
    private Vector2 gridScrollPos;
    private Vector2 inspectorScrollPos;

    private bool isConnecting = false;
    private UpgradeNodeSO selectedNode = null;
    private Editor cachedNodeEditor;

    [MenuItem("Tools/Upgrade Tree Editor")]
    public static void ShowWindow()
    {
        UpgradeTreeEditor window = GetWindow<UpgradeTreeEditor>("Upgrade Tree Editor");
        window.minSize = new Vector2(900, 600);
    }

    private void OnEnable()
    {
        wantsMouseMove = true;
    }

    private void OnGUI()
    {
        // 마우스 움직임 감지하여 갱신 (선 그리기용)
        if (Event.current.type == EventType.ScrollWheel ||
            Event.current.type == EventType.MouseMove ||
            Event.current.type == EventType.MouseDrag)
        {
            Repaint();
        }

        if (selectedDatabase == null)
        {
            DrawDatabaseSelector();
            return;
        }

        GUILayout.BeginHorizontal();
        {
            DrawGridView();
            DrawSplitter();
            DrawSidePanel();
        }
        GUILayout.EndHorizontal();

        ProcessEvents(Event.current);
    }

    private void DrawDatabaseSelector()
    {
        GUILayout.Space(20);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();
        GUILayout.Label("Upgrade Tree Editor", EditorStyles.boldLabel);
        GUILayout.Space(10);
        selectedDatabase = (UpgradeDatabase)EditorGUILayout.ObjectField("Database를 연결하세요:", selectedDatabase, typeof(UpgradeDatabase), false, GUILayout.Width(300));
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void DrawSplitter()
    {
        GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));
    }

    // =================================================================================
    // [왼쪽] 격자 뷰
    // =================================================================================
    private void DrawGridView()
    {
        int maxGridY = 0;
        foreach (var node in selectedDatabase.allUpgrades)
            if (node != null && node.gridY > maxGridY) maxGridY = node.gridY;

        int totalRows = maxGridY + EXTRA_ROWS + 1;
        float canvasHeight = totalRows * ROW_HEIGHT + BOTTOM_MARGIN;
        float canvasWidth = COL_WIDTH * 3 + 100f;

        gridScrollPos = GUILayout.BeginScrollView(gridScrollPos, true, true, GUILayout.ExpandHeight(true));

        Rect canvasRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight);

        DrawBackgroundGrid(canvasRect, totalRows);
        DrawConnections(canvasRect); // 선 먼저 그리기

        // 노드 그리기
        for (int y = 0; y < totalRows; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                Rect slotRect = GetSlotRect(canvasRect, x, y);
                UpgradeNodeSO node = GetNodeAt(x, y);

                if (node != null) DrawNode(node, slotRect);
                else DrawEmptySlot(slotRect, x, y);
            }
        }

        // 연결 중일 때 마우스 따라다니는 선
        if (isConnecting && selectedNode != null)
        {
            Rect startRect = GetSlotRect(canvasRect, selectedNode.gridX, selectedNode.gridY);
            Vector2 mousePos = Event.current.mousePosition;
            DrawBezierLine(startRect.center, mousePos, Color.green, 3f);
        }

        GUILayout.EndScrollView();
    }

    private void DrawBackgroundGrid(Rect canvasRect, int rows)
    {
        Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
        float centerX = canvasRect.width / 2f;

        for (float i = -1.5f; i <= 1.5f; i++)
        {
            float xPos = centerX + (i * COL_WIDTH);
            Handles.DrawLine(new Vector3(xPos, 0), new Vector3(xPos, canvasRect.height));
        }

        for (int y = 0; y <= rows; y++)
        {
            float yPos = canvasRect.height - BOTTOM_MARGIN - (y * ROW_HEIGHT) + (ROW_HEIGHT / 2f);
            Handles.DrawLine(new Vector3(0, yPos), new Vector3(canvasRect.width, yPos));
            GUI.Label(new Rect(10, yPos - 20, 100, 20), $"Floor {y}", EditorStyles.miniLabel);
        }
        Handles.color = Color.white;
    }

    private void DrawConnections(Rect canvasRect)
    {
        if (Event.current.type != EventType.Repaint) return;
        Color lineColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;

        foreach (var node in selectedDatabase.allUpgrades)
        {
            if (node == null) continue;

            Rect startRect = GetSlotRect(canvasRect, node.gridX, node.gridY);

            foreach (var childID in node.unlockedNodeIDs)
            {
                var childNode = selectedDatabase.allUpgrades.Find(n => n.nodeID == childID);
                if (childNode != null)
                {
                    Rect endRect = GetSlotRect(canvasRect, childNode.gridX, childNode.gridY);
                    DrawBezierLine(startRect.center, endRect.center, lineColor, 3f);
                }
            }
        }
    }

    private void DrawBezierLine(Vector3 start, Vector3 end, Color color, float width)
    {
        Vector3 startTan = start + Vector3.up * -50f;
        Vector3 endTan = end + Vector3.up * 50f;
        Handles.DrawBezier(start, end, startTan, endTan, color, null, width);
    }

    private void DrawNode(UpgradeNodeSO node, Rect rect)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 11;

        if (node == selectedNode) GUI.backgroundColor = Color.green;
        else if (isConnecting) GUI.backgroundColor = new Color(1f, 0.9f, 0.4f);
        else GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);

        string label = $"{node.upgradeName}\nID: {node.nodeID}";

        if (GUI.Button(rect, label, style))
        {
            if (isConnecting) CompleteConnection(node);
            else SelectNode(node);
        }
        GUI.backgroundColor = Color.white;
    }

    private void DrawEmptySlot(Rect rect, int gridX, int gridY)
    {
        if (isConnecting) return;

        GUI.backgroundColor = new Color(1, 1, 1, 0.1f);
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.fontSize = 24;
        style.normal.textColor = Color.gray;

        if (GUI.Button(rect, "+", style))
        {
            CreateNode(gridX, gridY);
        }
        GUI.backgroundColor = Color.white;
    }

    // =================================================================================
    // [오른쪽] 사이드 패널
    // =================================================================================
    private void DrawSidePanel()
    {
        GUILayout.BeginVertical(GUILayout.Width(SIDEBAR_WIDTH));

        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Inspector", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("DB Change", EditorStyles.toolbarButton)) selectedDatabase = null;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (selectedNode != null)
        {
            GUILayout.Label($"Editing: {selectedNode.upgradeName}", EditorStyles.largeLabel);
            GUILayout.Space(5);

            // 삭제 버튼
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("Delete Node", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Delete Node",
                    $"Delete '{selectedNode.upgradeName}'?", "Yes", "Cancel"))
                {
                    DeleteNode(selectedNode);
                    return;
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);
            DrawConnectionManager();

            GUILayout.Space(10);
            DrawUILine(Color.gray);
            GUILayout.Space(10);

            // [핵심] 인스펙터 그리기 (여기서 Effects 리스트를 수정하게 됨)
            if (cachedNodeEditor == null || cachedNodeEditor.target != selectedNode)
            {
                cachedNodeEditor = Editor.CreateEditor(selectedNode);
            }

            inspectorScrollPos = GUILayout.BeginScrollView(inspectorScrollPos);
            cachedNodeEditor.OnInspectorGUI();
            GUILayout.EndScrollView();
        }
        else
        {
            GUILayout.Label("Select a node to edit.", EditorStyles.centeredGreyMiniLabel);
        }

        GUILayout.EndVertical();
    }

    private void DrawConnectionManager()
    {
        if (isConnecting)
        {
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Cancel Connection Mode", GUILayout.Height(30))) isConnecting = false;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("Click a child node to connect.", MessageType.Warning);
        }
        else
        {
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Make New Connection", GUILayout.Height(30))) isConnecting = true;
            GUI.backgroundColor = Color.white;
        }

        GUILayout.Space(10);
        GUILayout.Label("Connections", EditorStyles.boldLabel);

        if (selectedNode.nextNodes != null && selectedNode.nextNodes.Count > 0)
        {
            for (int i = 0; i < selectedNode.nextNodes.Count; i++)
            {
                var child = selectedNode.nextNodes[i];
                if (child == null) continue;

                GUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label($"To: {child.upgradeName}", GUILayout.Width(180));
                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    DisconnectNodes(selectedNode, child);
                    break;
                }
                GUILayout.EndHorizontal();
            }
        }
        else
        {
            GUILayout.Label("(No child nodes connected)", EditorStyles.miniLabel);
        }
    }

    private void DrawUILine(Color color, int thickness = 2, int padding = 10)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding / 2;
        r.x -= 2;
        r.width += 6;
        EditorGUI.DrawRect(r, color);
    }

    // =================================================================================
    // [로직] 노드 생성 및 삭제
    // =================================================================================

    // [수정됨] 더미 클래스가 아닌 UpgradeNodeSO 자체를 생성
    private void CreateNode(int gridX, int gridY)
    {
        string path = "Assets/Resources/Upgrades/Nodes/";
        if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);

        UpgradeNodeSO newNode = CreateInstance<UpgradeNodeSO>();

        // 1. 파일 이름 미리 결정
        string fileName = $"Node_{System.DateTime.Now.Ticks}";

        // 2. 속성 설정
        newNode.gridX = gridX;
        newNode.gridY = gridY;
        newNode.upgradeName = $"New Upgrade {gridX}_{gridY}";
        newNode.price = 100;

        // [핵심 수정] OnValidate를 기다리지 말고, 여기서 바로 ID를 계산해서 넣습니다.
        // 이때 파일 이름(fileName)을 기준으로 해시를 만듭니다.
        newNode.name = fileName; // 메모리 상의 이름도 맞춰줍니다.
        newNode.nodeID = Animator.StringToHash(fileName);

        // 3. 파일 생성
        AssetDatabase.CreateAsset(newNode, path + fileName + ".asset");

        selectedDatabase.allUpgrades.Add(newNode);

        // 4. 확실하게 더티 체크 및 저장
        EditorUtility.SetDirty(newNode);
        EditorUtility.SetDirty(selectedDatabase);
        AssetDatabase.SaveAssets();

        SelectNode(newNode);
    }

    private void DeleteNode(UpgradeNodeSO nodeToDelete)
    {
        // 연결 관계 끊기
        foreach (var node in selectedDatabase.allUpgrades)
        {
            if (node == null || node == nodeToDelete) continue;

            if (node.nextNodes.Contains(nodeToDelete))
            {
                node.nextNodes.Remove(nodeToDelete);
                node.unlockedNodeIDs.Remove(nodeToDelete.nodeID);
                EditorUtility.SetDirty(node);
            }

            if (node.requiredParents.Contains(nodeToDelete))
            {
                node.requiredParents.Remove(nodeToDelete);
                node.requiredParentIDs.Remove(nodeToDelete.nodeID);
                EditorUtility.SetDirty(node);
            }
        }

        selectedDatabase.allUpgrades.Remove(nodeToDelete);
        EditorUtility.SetDirty(selectedDatabase);

        string path = AssetDatabase.GetAssetPath(nodeToDelete);
        AssetDatabase.DeleteAsset(path);

        selectedNode = null;
        isConnecting = false;
        AssetDatabase.SaveAssets();
    }

    // =================================================================================
    // [로직] 연결 및 선택
    // =================================================================================

    private void SelectNode(UpgradeNodeSO node)
    {
        selectedNode = node;
        isConnecting = false;
        Selection.activeObject = node;
        GUI.FocusControl(null);
    }

    private Rect GetSlotRect(Rect canvasRect, int gridX, int gridY)
    {
        float centerX = canvasRect.width / 2f;
        float x = centerX + (gridX * COL_WIDTH) - (NODE_WIDTH / 2f);
        float y = canvasRect.height - BOTTOM_MARGIN - (gridY * ROW_HEIGHT) - (NODE_HEIGHT / 2f);
        return new Rect(x, y, NODE_WIDTH, NODE_HEIGHT);
    }

    private UpgradeNodeSO GetNodeAt(int x, int y)
    {
        return selectedDatabase.allUpgrades.Find(node => node.gridX == x && node.gridY == y);
    }

    private void CompleteConnection(UpgradeNodeSO targetNode)
    {
        if (selectedNode == null || selectedNode == targetNode) return;

        if (!selectedNode.nextNodes.Contains(targetNode))
        {
            selectedNode.nextNodes.Add(targetNode);
            if (!selectedNode.unlockedNodeIDs.Contains(targetNode.nodeID))
                selectedNode.unlockedNodeIDs.Add(targetNode.nodeID);
            EditorUtility.SetDirty(selectedNode);
        }

        if (!targetNode.requiredParents.Contains(selectedNode))
        {
            targetNode.requiredParents.Add(selectedNode);
            if (!targetNode.requiredParentIDs.Contains(selectedNode.nodeID))
                targetNode.requiredParentIDs.Add(selectedNode.nodeID);
            EditorUtility.SetDirty(targetNode);
        }

        isConnecting = false;
        AssetDatabase.SaveAssets();
    }

    private void DisconnectNodes(UpgradeNodeSO parent, UpgradeNodeSO child)
    {
        if (parent.nextNodes.Contains(child)) parent.nextNodes.Remove(child);
        if (parent.unlockedNodeIDs.Contains(child.nodeID)) parent.unlockedNodeIDs.Remove(child.nodeID);

        if (child.requiredParents.Contains(parent)) child.requiredParents.Remove(parent);
        if (child.requiredParentIDs.Contains(parent.nodeID)) child.requiredParentIDs.Remove(parent.nodeID);

        EditorUtility.SetDirty(parent);
        EditorUtility.SetDirty(child);
        AssetDatabase.SaveAssets();
    }

    private void ProcessEvents(Event e)
    {
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            isConnecting = false;
            Repaint();
        }
    }
}
#endif
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class UpgradeTreeEditor : EditorWindow
{
    // ... (설정 변수 동일) ...
    private const float SIDEBAR_WIDTH = 340f;
    private const float NODE_WIDTH = 120f;
    private const float NODE_HEIGHT = 80f;
    private const float COL_SPACING = 200f;
    private const float ROW_SPACING = 150f;
    private const float START_X = 100f;

    // ... (상태 변수 동일) ...
    private UpgradeDatabase selectedDatabase;
    private Vector2 gridScrollPos;
    private Vector2 inspectorScrollPos;
    private bool isConnecting = false;
    private UpgradeNodeSO selectedNode = null;
    private Editor cachedNodeEditor;

    [MenuItem("Tools/Upgrade Tree Editor")]
    public static void ShowWindow()
    {
        GetWindow<UpgradeTreeEditor>("Upgrade Tree Editor").minSize = new Vector2(900, 600);
    }

    private void OnEnable() => wantsMouseMove = true;

    private void OnGUI()
    {
        if (selectedDatabase == null) { DrawDatabaseSelector(); return; }

        GUILayout.BeginHorizontal();
        DrawGridView();
        DrawSplitter();
        DrawSidePanel();
        GUILayout.EndHorizontal();

        if (Event.current.isMouse || Event.current.isKey) Repaint();
        ProcessEvents(Event.current);
    }

    // ... (Selector, Splitter 동일) ...
    private void DrawDatabaseSelector()
    {
        GUILayout.Space(20);
        selectedDatabase = (UpgradeDatabase)EditorGUILayout.ObjectField("Select DB:", selectedDatabase, typeof(UpgradeDatabase), false, GUILayout.Width(300));
    }
    private void DrawSplitter() => GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));

    private void DrawGridView()
    {
        int maxGridX = 0;
        if (selectedDatabase.allUpgrades != null)
        {
            foreach (var node in selectedDatabase.allUpgrades)
                if (node != null && node.gridX > maxGridX) maxGridX = node.gridX;
        }

        // 캔버스 크기 계산
        float canvasWidth = START_X + ((maxGridX + 2) * COL_SPACING);
        float canvasHeight = (3 + 2) * ROW_SPACING; // -1, 0, 1 + 여백

        gridScrollPos = GUILayout.BeginScrollView(gridScrollPos, true, true, GUILayout.ExpandHeight(true));
        Rect canvasRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight);

        DrawBackgroundGrid(canvasRect, maxGridX + 2);
        DrawConnections(canvasRect);

        // [수정] 반복문 범위 확인: 0부터 maxGridX까지 모두 순회
        for (int x = 0; x <= maxGridX + 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Rect slotRect = GetSlotRect(canvasRect, x, y);
                UpgradeNodeSO node = GetNodeAt(x, y);

                if (node != null) DrawNode(node, slotRect);
                else DrawEmptySlot(slotRect, x, y);
            }
        }

        if (isConnecting && selectedNode != null)
        {
            Rect startRect = GetSlotRect(canvasRect, selectedNode.gridX, selectedNode.gridY);
            DrawBezierLine(startRect.center, Event.current.mousePosition, Color.green, 3f);
        }

        GUILayout.EndScrollView();
    }

    // ... (DrawBackgroundGrid, DrawConnections, DrawBezierLine 동일) ...
    private void DrawBackgroundGrid(Rect canvasRect, int cols)
    {
        Handles.color = new Color(0.3f, 0.3f, 0.3f, 0.3f);
        float centerY = canvasRect.height / 2f;
        for (int x = 0; x <= cols; x++)
        {
            float gridX = START_X + (x * COL_SPACING); // 노드 중앙 기준
            Handles.DrawLine(new Vector3(gridX, 0), new Vector3(gridX, canvasRect.height));
            GUI.Label(new Rect(gridX + 5, 10, 50, 20), $"{x}", EditorStyles.miniLabel);
        }
        for (int y = -1; y <= 1; y++)
        {
            float yPos = centerY + (-y * ROW_SPACING);
            Handles.DrawLine(new Vector3(0, yPos), new Vector3(canvasRect.width, yPos));
        }
        Handles.color = Color.white;
    }

    private void DrawConnections(Rect canvasRect)
    {
        if (Event.current.type != EventType.Repaint || selectedDatabase.allUpgrades == null) return;
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
        Vector3 startTan = start + Vector3.right * 80f;
        Vector3 endTan = end + Vector3.left * 80f;
        Handles.DrawBezier(start, end, startTan, endTan, color, null, width);
    }

    private void DrawNode(UpgradeNodeSO node, Rect rect)
    {
        if (node == selectedNode) GUI.backgroundColor = Color.green;
        else if (isConnecting) GUI.backgroundColor = new Color(1f, 0.9f, 0.4f);
        else GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);

        if (GUI.Button(rect, $"{node.upgradeName}\n[{node.gridX}, {node.gridY}]"))
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
        if (GUI.Button(rect, "+")) CreateNode(gridX, gridY);
        GUI.backgroundColor = Color.white;
    }

    // ... (SidePanel, DrawConnectionManager 등 동일) ...
    private void DrawSidePanel()
    {
        GUILayout.BeginVertical(GUILayout.Width(SIDEBAR_WIDTH));
        if (GUILayout.Button("Close DB")) selectedDatabase = null;
        GUILayout.Space(10);
        if (selectedNode != null)
        {
            GUILayout.Label($"Editing: {selectedNode.upgradeName}", EditorStyles.boldLabel);
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("Delete Node")) DeleteNode(selectedNode);
            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);
            DrawConnectionManager();
            GUILayout.Space(10);
            if (cachedNodeEditor == null || cachedNodeEditor.target != selectedNode)
                cachedNodeEditor = Editor.CreateEditor(selectedNode);
            inspectorScrollPos = GUILayout.BeginScrollView(inspectorScrollPos);
            cachedNodeEditor.OnInspectorGUI();
            GUILayout.EndScrollView();
        }
        GUILayout.EndVertical();
    }

    private void DrawConnectionManager()
    {
        if (isConnecting)
        {
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Cancel Connection")) isConnecting = false;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.HelpBox("Select child node.", MessageType.Info);
        }
        else
        {
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Make Connection")) isConnecting = true;
            GUI.backgroundColor = Color.white;
        }
        if (selectedNode.nextNodes.Count > 0)
        {
            GUILayout.Label("Next Nodes:");
            foreach (var child in selectedNode.nextNodes)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"-> {child.upgradeName}");
                if (GUILayout.Button("X", GUILayout.Width(25))) { DisconnectNodes(selectedNode, child); break; }
                GUILayout.EndHorizontal();
            }
        }
    }

    // [중요] 좌표 계산 로직
    private Rect GetSlotRect(Rect canvasRect, int gridX, int gridY)
    {
        float centerY = canvasRect.height / 2f;
        float x = START_X + (gridX * COL_SPACING);
        float y = centerY + (-gridY * ROW_SPACING); // Y축 반전
        return new Rect(x - (NODE_WIDTH / 2f), y - (NODE_HEIGHT / 2f), NODE_WIDTH, NODE_HEIGHT);
    }

    private UpgradeNodeSO GetNodeAt(int x, int y)
    {
        if (selectedDatabase.allUpgrades == null) return null;
        return selectedDatabase.allUpgrades.Find(node => node.gridX == x && node.gridY == y);
    }

    // ... (CreateNode, DeleteNode, SelectNode, CompleteConnection, DisconnectNodes, ProcessEvents 동일) ...
    private void CreateNode(int gridX, int gridY)
    {
        string path = "Assets/Resources/Upgrades/Nodes/";
        if (!System.IO.Directory.Exists(path)) System.IO.Directory.CreateDirectory(path);
        UpgradeNodeSO newNode = CreateInstance<UpgradeNodeSO>();
        string fileName = $"Node_{System.DateTime.Now.Ticks}";
        newNode.gridX = gridX; newNode.gridY = gridY; newNode.upgradeName = $"Up_{gridX}_{gridY}";
        newNode.name = fileName; newNode.nodeID = Animator.StringToHash(fileName); newNode.price = 100;
        AssetDatabase.CreateAsset(newNode, path + fileName + ".asset");
        if (selectedDatabase.allUpgrades == null) selectedDatabase.allUpgrades = new List<UpgradeNodeSO>();
        selectedDatabase.allUpgrades.Add(newNode);
        EditorUtility.SetDirty(selectedDatabase); AssetDatabase.SaveAssets(); SelectNode(newNode);
    }
    private void DeleteNode(UpgradeNodeSO target)
    {
        foreach (var node in selectedDatabase.allUpgrades)
        {
            if (node.nextNodes.Contains(target)) { node.nextNodes.Remove(target); node.unlockedNodeIDs.Remove(target.nodeID); EditorUtility.SetDirty(node); }
            if (node.requiredParents.Contains(target)) { node.requiredParents.Remove(target); node.requiredParentIDs.Remove(target.nodeID); EditorUtility.SetDirty(node); }
        }
        selectedDatabase.allUpgrades.Remove(target); AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(target));
        selectedNode = null; AssetDatabase.SaveAssets();
    }
    private void SelectNode(UpgradeNodeSO node) { selectedNode = node; isConnecting = false; Selection.activeObject = node; GUI.FocusControl(null); }
    private void CompleteConnection(UpgradeNodeSO target)
    {
        if (selectedNode == null || selectedNode == target) return;
        if (!selectedNode.nextNodes.Contains(target)) { selectedNode.nextNodes.Add(target); selectedNode.unlockedNodeIDs.Add(target.nodeID); EditorUtility.SetDirty(selectedNode); }
        if (!target.requiredParents.Contains(selectedNode)) { target.requiredParents.Add(selectedNode); target.requiredParentIDs.Add(selectedNode.nodeID); EditorUtility.SetDirty(target); }
        isConnecting = false; AssetDatabase.SaveAssets();
    }
    private void DisconnectNodes(UpgradeNodeSO p, UpgradeNodeSO c)
    {
        p.nextNodes.Remove(c); p.unlockedNodeIDs.Remove(c.nodeID); c.requiredParents.Remove(p); c.requiredParentIDs.Remove(p.nodeID);
        EditorUtility.SetDirty(p); EditorUtility.SetDirty(c); AssetDatabase.SaveAssets();
    }
    private void ProcessEvents(Event e) { if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { isConnecting = false; Repaint(); } }
}
#endif
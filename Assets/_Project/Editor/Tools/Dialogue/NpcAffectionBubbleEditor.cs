using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NpcAffectionBubble))]
public sealed class NpcAffectionBubbleEditor : Editor
{
    private bool preview = true;

    private void OnEnable() => EditorApplication.update += RepaintPreview;
    private void OnDisable() => EditorApplication.update -= RepaintPreview;
    private void RepaintPreview() { if (preview) { Repaint(); SceneView.RepaintAll(); } }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        preview = EditorGUILayout.Toggle("Animated Scene Preview", preview);
        EditorGUILayout.HelpBox("Drag the position handle in Scene view. Save the prefab to share the position with future encounters.", MessageType.Info);
    }

    private void OnSceneGUI()
    {
        var bubble = (NpcAffectionBubble)target;
        if (bubble.Anchor == null) return;
        EditorGUI.BeginChangeCheck();
        Vector3 position = Handles.PositionHandle(bubble.Anchor.position, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(bubble.Anchor, "Move NPC affection bubble");
            bubble.Anchor.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(bubble.Anchor);
        }
        if (!preview || Application.isPlaying || bubble.Frames == null || bubble.Frames.Length == 0) return;
        Sprite frame = bubble.Frames[(int)(EditorApplication.timeSinceStartup * bubble.FramesPerSecond) % bubble.Frames.Length];
        if (frame == null || SceneView.currentDrawingSceneView == null) return;
        Camera camera = SceneView.currentDrawingSceneView.camera;
        Vector3 right = camera.transform.right * bubble.WorldSize * 0.5f;
        Vector3 up = camera.transform.up * bubble.WorldSize * 0.5f;
        Vector2 topLeft = HandleUtility.WorldToGUIPoint(bubble.Anchor.position - right + up);
        Vector2 bottomRight = HandleUtility.WorldToGUIPoint(bubble.Anchor.position + right - up);
        Rect uv = frame.rect;
        uv.x /= frame.texture.width; uv.width /= frame.texture.width;
        uv.y /= frame.texture.height; uv.height /= frame.texture.height;
        Handles.BeginGUI();
        GUI.DrawTextureWithTexCoords(Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y), frame.texture, uv);
        Handles.EndGUI();
    }
}

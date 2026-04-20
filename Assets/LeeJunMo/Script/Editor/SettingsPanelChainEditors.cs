using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SettingsPanelFakeChainPresentation))]
public sealed class SettingsPanelFakeChainPresentationEditor : Editor
{
    private SerializedProperty chainContainerProperty;
    private SerializedProperty topAnchorProperty;
    private SerializedProperty bottomEndpointModeProperty;
    private SerializedProperty bottomAnchorProperty;
    private SerializedProperty bottomAnchorLocalOffsetProperty;
    private SerializedProperty freeEndLocalOffsetProperty;
    private SerializedProperty lastLinkLocalOffsetProperty;
    private SerializedProperty chainLinksProperty;

    private void OnEnable()
    {
        chainContainerProperty = serializedObject.FindProperty("chainContainer");
        topAnchorProperty = serializedObject.FindProperty("topAnchor");
        bottomEndpointModeProperty = serializedObject.FindProperty("bottomEndpointMode");
        bottomAnchorProperty = serializedObject.FindProperty("bottomAnchor");
        bottomAnchorLocalOffsetProperty = serializedObject.FindProperty("bottomAnchorLocalOffset");
        freeEndLocalOffsetProperty = serializedObject.FindProperty("freeEndLocalOffset");
        lastLinkLocalOffsetProperty = serializedObject.FindProperty("lastLinkLocalOffset");
        chainLinksProperty = serializedObject.FindProperty("chainLinks");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Setup Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Assign"))
                AutoAssignReferences();

            if (GUILayout.Button("Snap Preview"))
                SnapPreview();
        }

        EditorGUILayout.HelpBox(
            "Anchored mode keeps both ends fixed. Free Hanging mode ignores Bottom Anchor and lets you place a decorative free end directly in SceneView.",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        serializedObject.Update();

        RectTransform topAnchor = topAnchorProperty.objectReferenceValue as RectTransform;
        RectTransform bottomAnchor = bottomAnchorProperty.objectReferenceValue as RectTransform;
        SettingsPanelFakeChainPresentation presentation = (SettingsPanelFakeChainPresentation)target;
        bool anchoredBottomEndpoint = UsesAnchoredBottomEndpoint();
        if (topAnchor == null)
            return;

        DrawAnchorHandle(topAnchor, new Color(0.25f, 0.95f, 0.45f, 1f), "Top Anchor", presentation);
        if (anchoredBottomEndpoint && bottomAnchor != null)
            DrawAnchorHandle(bottomAnchor, new Color(0.95f, 0.55f, 0.25f, 1f), "Bottom Anchor", presentation);
        DrawBottomEndpointHandle(presentation);
        DrawLastLinkHandle(presentation);

        Handles.color = new Color(0.4f, 0.8f, 1f, 0.85f);
        if (presentation.TryGetBottomEndpointWorldPosition(out Vector2 bottomEndpointWorldPosition))
            Handles.DrawAAPolyLine(4f, topAnchor.position, bottomEndpointWorldPosition);
        Handles.DrawWireDisc(topAnchor.position, Vector3.forward, presentation.TotalChainLength);
        Handles.Label(topAnchor.position + Vector3.left * 24f, $"Reach {presentation.TotalChainLength:0}");
    }

    private void DrawAnchorHandle(RectTransform anchor, Color color, string label, SettingsPanelFakeChainPresentation presentation)
    {
        Handles.color = color;
        float size = HandleUtility.GetHandleSize(anchor.position) * 0.12f;
        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.FreeMoveHandle(anchor.position, size, Vector3.zero, Handles.SphereHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(anchor, $"Move {label}");
            anchor.position = newPosition;
            EditorUtility.SetDirty(anchor);
            presentation.SnapToCurrentPose();
        }

        Handles.Label(anchor.position + Vector3.right * 12f, label);
    }

    private void DrawBottomEndpointHandle(SettingsPanelFakeChainPresentation presentation)
    {
        RectTransform chainContainer = chainContainerProperty.objectReferenceValue as RectTransform;
        RectTransform bottomAnchor = bottomAnchorProperty.objectReferenceValue as RectTransform;
        RectTransform topAnchor = topAnchorProperty.objectReferenceValue as RectTransform;
        if (chainContainer == null)
            return;

        if (!presentation.TryGetBottomEndpointWorldPosition(out Vector2 bottomEndpointWorldPosition))
            return;

        bool anchoredBottomEndpoint = UsesAnchoredBottomEndpoint();
        if (anchoredBottomEndpoint && (bottomAnchor == null || bottomAnchorLocalOffsetProperty == null))
            return;

        if (!anchoredBottomEndpoint && (topAnchor == null || freeEndLocalOffsetProperty == null))
            return;

        Handles.color = new Color(1f, 0.9f, 0.2f, 0.95f);
        float size = HandleUtility.GetHandleSize(bottomEndpointWorldPosition) * 0.1f;
        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.FreeMoveHandle(bottomEndpointWorldPosition, size, Vector3.zero, Handles.RectangleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Vector2 newLocal = chainContainer.InverseTransformPoint(newPosition);
            if (anchoredBottomEndpoint)
            {
                Vector2 baseLocal = chainContainer.InverseTransformPoint(bottomAnchor.position);
                bottomAnchorLocalOffsetProperty.vector2Value = newLocal - baseLocal;
            }
            else
            {
                Vector2 topLocal = chainContainer.InverseTransformPoint(topAnchor.position);
                freeEndLocalOffsetProperty.vector2Value = newLocal - topLocal;
            }

            serializedObject.ApplyModifiedProperties();
            presentation.SnapToCurrentPose();
            EditorUtility.SetDirty(presentation);
        }

        if (anchoredBottomEndpoint)
            Handles.DrawDottedLine(bottomAnchor.position, bottomEndpointWorldPosition, 4f);
        else if (topAnchor != null)
            Handles.DrawDottedLine(topAnchor.position, bottomEndpointWorldPosition, 4f);

        Handles.Label(
            (Vector3)bottomEndpointWorldPosition + Vector3.right * 12f,
            anchoredBottomEndpoint ? "Bottom Endpoint" : "Free End");
    }

    private void DrawLastLinkHandle(SettingsPanelFakeChainPresentation presentation)
    {
        RectTransform chainContainer = chainContainerProperty.objectReferenceValue as RectTransform;
        if (chainContainer == null || lastLinkLocalOffsetProperty == null)
            return;

        if (!presentation.TryGetLastLinkBaseLocalPosition(out Vector2 baseLocalPosition))
            return;

        if (!presentation.TryGetLastLinkHandleWorldPosition(out Vector2 currentWorldPosition))
            return;

        Handles.color = new Color(0.45f, 0.95f, 1f, 0.95f);
        float size = HandleUtility.GetHandleSize(currentWorldPosition) * 0.11f;
        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.FreeMoveHandle(currentWorldPosition, size, Vector3.zero, Handles.CircleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Vector2 newLocal = chainContainer.InverseTransformPoint(newPosition);
            lastLinkLocalOffsetProperty.vector2Value = newLocal - baseLocalPosition;
            serializedObject.ApplyModifiedProperties();
            presentation.SnapToCurrentPose();
            EditorUtility.SetDirty(presentation);
        }

        Handles.DrawDottedLine(chainContainer.TransformPoint(baseLocalPosition), currentWorldPosition, 4f);
        Handles.Label((Vector3)currentWorldPosition + Vector3.right * 12f, "Last Link");
    }

    private void AutoAssignReferences()
    {
        serializedObject.Update();

        SettingsPanelFakeChainPresentation presentation = (SettingsPanelFakeChainPresentation)target;
        RectTransform container = chainContainerProperty.objectReferenceValue as RectTransform;
        if (container == null)
            container = presentation.transform as RectTransform;

        chainContainerProperty.objectReferenceValue = container;
        if (container == null)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (topAnchorProperty.objectReferenceValue == null)
            topAnchorProperty.objectReferenceValue = FindByName(container, "TopAnchor");

        if (UsesAnchoredBottomEndpoint() && bottomAnchorProperty.objectReferenceValue == null)
            bottomAnchorProperty.objectReferenceValue = FindByName(container.root, "PanelTopAnchor");

        List<RectTransform> links = new List<RectTransform>();
        CollectLinkRects(container, links);
        chainLinksProperty.arraySize = links.Count;
        for (int i = 0; i < links.Count; i++)
            chainLinksProperty.GetArrayElementAtIndex(i).objectReferenceValue = links[i];

        serializedObject.ApplyModifiedProperties();
        presentation.SnapToCurrentPose();
        EditorUtility.SetDirty(presentation);
    }

    private void SnapPreview()
    {
        serializedObject.ApplyModifiedProperties();
        foreach (Object currentTarget in targets)
        {
            SettingsPanelFakeChainPresentation presentation = currentTarget as SettingsPanelFakeChainPresentation;
            if (presentation == null)
                continue;

            presentation.SnapToCurrentPose();
            EditorUtility.SetDirty(presentation);
        }

        SceneView.RepaintAll();
    }

    private bool UsesAnchoredBottomEndpoint()
    {
        return bottomEndpointModeProperty == null
            || bottomEndpointModeProperty.enumValueIndex == (int)SettingsPanelChainBottomEndpointMode.Anchored;
    }

    private static RectTransform FindByName(Transform root, string name)
    {
        if (root == null)
            return null;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect != null && rect.name == name)
                return rect;
        }

        return null;
    }

    private static void CollectLinkRects(RectTransform root, List<RectTransform> results)
    {
        if (root == null || results == null)
            return;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || rect == root)
                continue;

            if (rect.name.StartsWith("Link"))
                results.Add(rect);
        }

        results.Sort((left, right) => CompareHierarchyOrder(left, right));
    }

    private static int CompareHierarchyOrder(Transform left, Transform right)
    {
        if (left == right)
            return 0;

        if (left == null)
            return -1;

        if (right == null)
            return 1;

        if (left.parent == right.parent)
            return left.GetSiblingIndex().CompareTo(right.GetSiblingIndex());

        return string.CompareOrdinal(left.GetHierarchyPath(), right.GetHierarchyPath());
    }
}

[CustomEditor(typeof(UIChainDropPresentation))]
public sealed class UIChainDropPresentationEditor : Editor
{
    private SerializedProperty panelRootProperty;
    private SerializedProperty interactionCanvasGroupProperty;
    private SerializedProperty chainAttachPointProperty;
    private SerializedProperty fakeChainPresentationProperty;
    private SerializedProperty chainConstraintsProperty;

    private void OnEnable()
    {
        panelRootProperty = serializedObject.FindProperty("panelRoot");
        interactionCanvasGroupProperty = serializedObject.FindProperty("interactionCanvasGroup");
        chainAttachPointProperty = serializedObject.FindProperty("chainAttachPoint");
        fakeChainPresentationProperty = serializedObject.FindProperty("fakeChainPresentation");
        chainConstraintsProperty = serializedObject.FindProperty("chainConstraints");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Setup Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Assign"))
                AutoAssignReferences();

            if (GUILayout.Button("Play Open"))
                ExecutePreview(presentation => presentation.PlayOpen());

            if (GUILayout.Button("Play Close"))
                ExecutePreview(presentation => presentation.PlayClose());
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Toggle Preview"))
                ExecutePreview(presentation => presentation.TogglePreview());

            if (GUILayout.Button("Snap Open"))
                ExecutePreview(presentation => presentation.SnapOpen());

            if (GUILayout.Button("Snap Closed"))
                ExecutePreview(presentation => presentation.SnapClosed());
        }

        EditorGUILayout.HelpBox(
            "Auto Assign wires the legacy single-chain fields. For left/right dual chains, add entries to Chain Constraints and assign each attach point and chain presentation separately.",
            MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        serializedObject.Update();

        bool drewAnyHandle = false;

        if (chainConstraintsProperty != null && chainConstraintsProperty.isArray && chainConstraintsProperty.arraySize > 0)
        {
            for (int i = 0; i < chainConstraintsProperty.arraySize; i++)
            {
                SerializedProperty element = chainConstraintsProperty.GetArrayElementAtIndex(i);
                SerializedProperty attachProperty = element.FindPropertyRelative("chainAttachPoint");
                SerializedProperty presentationProperty = element.FindPropertyRelative("fakeChainPresentation");
                drewAnyHandle |= DrawAttachHandle(attachProperty, presentationProperty, $"Chain Attach {i + 1}");
            }
        }

        if (!drewAnyHandle)
            DrawAttachHandle(chainAttachPointProperty, fakeChainPresentationProperty, "Chain Attach");
    }

    private void AutoAssignReferences()
    {
        serializedObject.Update();

        UIChainDropPresentation presentation = (UIChainDropPresentation)target;
        RectTransform panelRoot = panelRootProperty.objectReferenceValue as RectTransform;
        if (panelRoot == null)
            panelRoot = presentation.transform as RectTransform;

        panelRootProperty.objectReferenceValue = panelRoot;
        if (interactionCanvasGroupProperty.objectReferenceValue == null && panelRoot != null)
            interactionCanvasGroupProperty.objectReferenceValue = panelRoot.GetComponent<CanvasGroup>();

        if (chainAttachPointProperty.objectReferenceValue == null && panelRoot != null)
            chainAttachPointProperty.objectReferenceValue = FindByName(panelRoot, "PanelTopAnchor") ?? panelRoot;

        if (fakeChainPresentationProperty.objectReferenceValue == null)
        {
            SettingsPanelFakeChainPresentation fakeChain = presentation.GetComponentInParent<Canvas>(true) != null
                ? presentation.GetComponentInParent<Canvas>(true).GetComponentInChildren<SettingsPanelFakeChainPresentation>(true)
                : presentation.GetComponentInChildren<SettingsPanelFakeChainPresentation>(true);
            fakeChainPresentationProperty.objectReferenceValue = fakeChain;
        }

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(presentation);
    }

    private bool DrawAttachHandle(
        SerializedProperty attachPointProperty,
        SerializedProperty presentationProperty,
        string label)
    {
        RectTransform attachPoint = attachPointProperty != null ? attachPointProperty.objectReferenceValue as RectTransform : null;
        SettingsPanelFakeChainPresentation fakeChain = presentationProperty != null
            ? presentationProperty.objectReferenceValue as SettingsPanelFakeChainPresentation
            : null;
        if (attachPoint == null)
            return false;

        Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
        float size = HandleUtility.GetHandleSize(attachPoint.position) * 0.11f;
        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.FreeMoveHandle(attachPoint.position, size, Vector3.zero, Handles.RectangleHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(attachPoint, $"Move {label}");
            attachPoint.position = newPosition;
            EditorUtility.SetDirty(attachPoint);
            fakeChain?.SnapToCurrentPose();
        }

        Handles.Label(attachPoint.position + Vector3.right * 12f, label);

        if (fakeChain != null && fakeChain.TryGetTopAnchorWorldPosition(out Vector2 topWorld))
        {
            Handles.color = new Color(1f, 0.92f, 0.35f, 0.85f);
            Handles.DrawDottedLine(topWorld, attachPoint.position, 4f);
        }

        return true;
    }

    private void ExecutePreview(System.Action<UIChainDropPresentation> action)
    {
        serializedObject.ApplyModifiedProperties();
        foreach (Object currentTarget in targets)
        {
            UIChainDropPresentation presentation = currentTarget as UIChainDropPresentation;
            if (presentation == null)
                continue;

            action(presentation);
            EditorUtility.SetDirty(presentation);
        }

        SceneView.RepaintAll();
    }

    private static RectTransform FindByName(Transform root, string name)
    {
        if (root == null)
            return null;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect != null && rect.name == name)
                return rect;
        }

        return null;
    }
}

internal static class SettingsPanelEditorTransformExtensions
{
    public static string GetHierarchyPath(this Transform transform)
    {
        if (transform == null)
            return string.Empty;

        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}

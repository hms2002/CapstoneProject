using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIParticleEmitter))]
[CanEditMultipleObjects]
[InitializeOnLoad]
public sealed class UIParticleEmitterEditor : Editor
{
    private static readonly Dictionary<UIParticleEmitter, double> previewTimes = new();

    private static readonly Color RectColor = new(0.35f, 0.85f, 1f, 0.95f);
    private static readonly Color ShapeColor = new(1f, 0.76f, 0.2f, 1f);
    private static readonly Color DiameterColor = new(0.35f, 1f, 0.55f, 1f);
    private static readonly Color DirectionColor = new(1f, 0.35f, 0.08f, 1f);
    private static readonly Color SpreadColor = new(1f, 0.55f, 0.16f, 0.95f);
    private static readonly Color OriginColor = new(1f, 0.92f, 0.2f, 1f);

    private const string ParticleRootPropertyName = "particleRoot";
    private const string ShowGizmosPropertyName = "showGizmos";
    private const string ShowGizmosOnlyWhenSelectedPropertyName = "showGizmosOnlyWhenSelected";
    private const string ShapePropertyName = "shape";
    private const string ShapeRadiusPropertyName = "shapeRadius";
    private const string ShapeSizePropertyName = "shapeSize";
    private const string EmitterOffsetPropertyName = "emitterOffset";
    private const string DirectionAnglePropertyName = "directionAngle";
    private const string SpreadAnglePropertyName = "spreadAngle";
    private const string StartLifetimePropertyName = "startLifetime";
    private const string StartSpeedPropertyName = "startSpeed";

    private SerializedProperty emitterOffsetProperty;

    static UIParticleEmitterEditor()
    {
        SceneView.duringSceneGui -= DrawSelectedEmittersInSceneView;
        SceneView.duringSceneGui += DrawSelectedEmittersInSceneView;
    }

    private void OnEnable()
    {
        emitterOffsetProperty = serializedObject.FindProperty(EmitterOffsetPropertyName);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Test Play"))
                ForEachTargetObject(TestPlay);

            if (GUILayout.Button("Stop"))
                ForEachTargetObject(StopPreview);

            if (GUILayout.Button("Clear"))
                ForEachTargetObject(ClearPreview);
        }
    }

    public void OnSceneGUI()
    {
        serializedObject.Update();

        UIParticleEmitter emitter = (UIParticleEmitter)target;
        if (emitter == null)
            return;

        if (!TryBuildGizmoData(serializedObject, emitter.transform, out GizmoData gizmoData, out _))
            return;

        UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        try
        {
            DrawOriginHandle(gizmoData.Root, gizmoData.OriginWorld);
        }
        finally
        {
            Handles.zTest = previousZTest;
        }
    }

    private void DrawOriginHandle(RectTransform root, Vector3 originWorld)
    {
        Handles.color = OriginColor;
        float size = HandleUtility.GetHandleSize(originWorld) * 0.14f;

        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.Slider2D(
            originWorld,
            ResolvePlaneNormal(root),
            root.right,
            root.up,
            size,
            Handles.RectangleHandleCap,
            Vector2.zero);
        if (EditorGUI.EndChangeCheck() && emitterOffsetProperty != null)
        {
            Undo.RecordObject(target, "Move UI Particle Emitter Origin");
            emitterOffsetProperty.vector2Value = root.InverseTransformPoint(newPosition);
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.InSelectionHierarchy | GizmoType.Pickable)]
    private static void DrawEmitterGizmosForScene(UIParticleEmitter emitter, GizmoType gizmoType)
    {
        if (!TryBuildGizmoData(emitter, out GizmoData gizmoData))
            return;

        bool selected = (gizmoType & GizmoType.Selected) != 0
            || (gizmoType & GizmoType.InSelectionHierarchy) != 0;
        if (selected || gizmoData.ShowOnlyWhenSelected)
            return;

        DrawEmitterGizmos(gizmoData);
    }

    private static void DrawEmitterGizmos(GizmoData gizmoData)
    {
        UnityEngine.Rendering.CompareFunction previousZTest = Handles.zTest;
        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        try
        {
            DrawRootFrame(gizmoData.Root);

            Vector3 originWorld = gizmoData.OriginWorld;
            DrawOriginMarker(originWorld);
            DrawShape(
                gizmoData.Root,
                gizmoData.OriginLocal,
                originWorld,
                gizmoData.Shape,
                gizmoData.ShapeRadius,
                gizmoData.ShapeSize,
                gizmoData.DirectionAngle,
                gizmoData.SpreadAngle);
            DrawDirection(
                gizmoData.Root,
                gizmoData.OriginLocal,
                originWorld,
                gizmoData.DirectionAngle,
                gizmoData.SpreadAngle,
                gizmoData.PreviewDistance);
        }
        finally
        {
            Handles.zTest = previousZTest;
        }
    }

    private static bool TryBuildGizmoData(UIParticleEmitter emitter, out GizmoData gizmoData)
    {
        return TryBuildGizmoData(emitter, out gizmoData, out _);
    }

    private static bool TryBuildGizmoData(UIParticleEmitter emitter, out GizmoData gizmoData, out string failureReason)
    {
        gizmoData = default;
        failureReason = null;
        if (emitter == null)
        {
            failureReason = "emitter is null";
            return false;
        }

        SerializedObject serializedEmitter = new(emitter);
        return TryBuildGizmoData(serializedEmitter, emitter.transform, out gizmoData, out failureReason);
    }

    private static bool TryBuildGizmoData(
        SerializedObject serializedEmitter,
        Transform fallbackTransform,
        out GizmoData gizmoData,
        out string failureReason)
    {
        gizmoData = default;
        failureReason = null;
        if (serializedEmitter == null)
        {
            failureReason = "serializedObject is null";
            return false;
        }

        SerializedProperty showGizmos = serializedEmitter.FindProperty(ShowGizmosPropertyName);
        if (showGizmos != null && !showGizmos.boolValue)
        {
            failureReason = "showGizmos is false";
            return false;
        }

        RectTransform root = ResolveRoot(serializedEmitter, fallbackTransform);
        if (root == null)
        {
            failureReason = $"root is null. fallback={fallbackTransform?.name ?? "null"}";
            return false;
        }

        Vector2 startSpeed = serializedEmitter.FindProperty(StartSpeedPropertyName)?.vector2Value ?? Vector2.zero;
        Vector2 startLifetime = serializedEmitter.FindProperty(StartLifetimePropertyName)?.vector2Value ?? Vector2.zero;
        Vector2 shapeSize = serializedEmitter.FindProperty(ShapeSizePropertyName)?.vector2Value ?? Vector2.zero;

        gizmoData = new GizmoData(
            root,
            serializedEmitter.FindProperty(EmitterOffsetPropertyName)?.vector2Value ?? Vector2.zero,
            ResolveShape(serializedEmitter.FindProperty(ShapePropertyName)),
            Mathf.Max(0f, serializedEmitter.FindProperty(ShapeRadiusPropertyName)?.floatValue ?? 0f),
            new Vector2(Mathf.Max(0f, shapeSize.x), Mathf.Max(0f, shapeSize.y)),
            serializedEmitter.FindProperty(DirectionAnglePropertyName)?.floatValue ?? 0f,
            serializedEmitter.FindProperty(SpreadAnglePropertyName)?.floatValue ?? 0f,
            startSpeed,
            startLifetime,
            serializedEmitter.FindProperty(ShowGizmosOnlyWhenSelectedPropertyName)?.boolValue ?? false);
        return true;
    }

    private static void DrawSelectedEmittersInSceneView(SceneView sceneView)
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
            return;

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            GameObject selectedObject = selectedObjects[i];
            if (selectedObject == null)
                continue;

            UIParticleEmitter emitter = selectedObject.GetComponent<UIParticleEmitter>();
            if (emitter == null)
                continue;

            if (TryBuildGizmoData(emitter, out GizmoData gizmoData, out _))
                DrawEmitterGizmos(gizmoData);
        }
    }

    private static RectTransform ResolveRoot(SerializedObject serializedEmitter, Transform fallbackTransform)
    {
        RectTransform root = serializedEmitter.FindProperty(ParticleRootPropertyName)?.objectReferenceValue as RectTransform;
        if (root != null)
            return root;

        if (fallbackTransform is RectTransform fallbackRect)
            return fallbackRect;

        return fallbackTransform != null
            ? fallbackTransform.GetComponentInParent<RectTransform>()
            : null;
    }

    private static UIParticleShape ResolveShape(SerializedProperty shapeProperty)
    {
        if (shapeProperty == null)
            return UIParticleShape.Point;

        return (UIParticleShape)shapeProperty.enumValueIndex;
    }

    private readonly struct GizmoData
    {
        public readonly RectTransform Root;
        public readonly Vector2 OriginLocal;
        public readonly UIParticleShape Shape;
        public readonly float ShapeRadius;
        public readonly Vector2 ShapeSize;
        public readonly float DirectionAngle;
        public readonly float SpreadAngle;
        public readonly Vector2 StartSpeed;
        public readonly Vector2 StartLifetime;
        public readonly bool ShowOnlyWhenSelected;

        public GizmoData(
            RectTransform root,
            Vector2 originLocal,
            UIParticleShape shape,
            float shapeRadius,
            Vector2 shapeSize,
            float directionAngle,
            float spreadAngle,
            Vector2 startSpeed,
            Vector2 startLifetime,
            bool showOnlyWhenSelected)
        {
            Root = root;
            OriginLocal = originLocal;
            Shape = shape;
            ShapeRadius = shapeRadius;
            ShapeSize = shapeSize;
            DirectionAngle = directionAngle;
            SpreadAngle = spreadAngle;
            StartSpeed = startSpeed;
            StartLifetime = startLifetime;
            ShowOnlyWhenSelected = showOnlyWhenSelected;
        }

        public Vector3 OriginWorld => Root.TransformPoint(OriginLocal);
        public float PreviewDistance => Mathf.Max(24f, ResolvePreviewTravelDistance(StartSpeed, StartLifetime));
    }

    private static void DrawOriginMarker(Vector3 originWorld)
    {
        Handles.color = OriginColor;
        float size = HandleUtility.GetHandleSize(originWorld) * 0.06f;
        Handles.DotHandleCap(0, originWorld, Quaternion.identity, size, EventType.Repaint);
    }

    private static void DrawRootFrame(RectTransform root)
    {
        Vector3[] corners = new Vector3[4];
        root.GetWorldCorners(corners);

        Handles.color = RectColor;
        for (int i = 0; i < 4; i++)
            Handles.DrawDottedLine(corners[i], corners[(i + 1) % 4], 4f);
    }

    private static void DrawShape(
        RectTransform root,
        Vector2 originLocal,
        Vector3 originWorld,
        UIParticleShape shape,
        float radius,
        Vector2 size,
        float directionAngle,
        float spreadAngle)
    {
        Handles.color = ShapeColor;

        switch (shape)
        {
            case UIParticleShape.Circle:
            case UIParticleShape.Ring:
                DrawLocalCircle(root, originLocal, radius, ShapeColor, 72, 4f);
                DrawDiameter(root, originWorld, root.TransformPoint(originLocal - Vector2.right * radius), root.TransformPoint(originLocal + Vector2.right * radius));
                break;

            case UIParticleShape.Line:
                DrawLocalLine(root, originWorld, originLocal - Vector2.right * (size.x * 0.5f), originLocal + Vector2.right * (size.x * 0.5f));
                break;

            case UIParticleShape.Rectangle:
                DrawLocalRect(root, originLocal, size, drawCenterLines: true);
                break;

            case UIParticleShape.RectangleEdge:
                DrawLocalRect(root, originLocal, size, drawCenterLines: false);
                break;

            case UIParticleShape.Ellipse:
                DrawLocalEllipse(root, originLocal, size, drawCenterLines: true);
                break;

            case UIParticleShape.EllipseEdge:
                DrawLocalEllipse(root, originLocal, size, drawCenterLines: false);
                break;

            case UIParticleShape.Arc:
                DrawArcShape(root, originLocal, originWorld, radius, directionAngle, spreadAngle, drawSector: false);
                break;

            case UIParticleShape.ArcFilled:
                DrawArcShape(root, originLocal, originWorld, radius, directionAngle, spreadAngle, drawSector: true);
                break;

            case UIParticleShape.Point:
            default:
                DrawCross(root, originWorld);
                break;
        }
    }

    private static void DrawDiameter(RectTransform root, Vector3 originWorld, Vector3 leftWorld, Vector3 rightWorld)
    {
        float tickSize = HandleUtility.GetHandleSize(originWorld) * 0.06f;

        Handles.color = DiameterColor;
        Handles.DrawDottedLine(leftWorld, rightWorld, 4f);
        Handles.DrawLine(leftWorld - root.up * tickSize, leftWorld + root.up * tickSize);
        Handles.DrawLine(rightWorld - root.up * tickSize, rightWorld + root.up * tickSize);
    }

    private static void DrawLocalLine(RectTransform root, Vector3 originWorld, Vector2 localStart, Vector2 localEnd)
    {
        if ((localEnd - localStart).sqrMagnitude <= 0.001f)
        {
            DrawCross(root, originWorld);
            return;
        }

        Vector3 startWorld = root.TransformPoint(localStart);
        Vector3 endWorld = root.TransformPoint(localEnd);
        Vector2 localDirection = (localEnd - localStart).normalized;
        Vector3 tickDirection = LocalVectorToWorld(root, new Vector2(-localDirection.y, localDirection.x));
        float tickSize = HandleUtility.GetHandleSize(originWorld) * 0.06f;

        Handles.color = ShapeColor;
        Handles.DrawDottedLine(startWorld, endWorld, 4f);
        Handles.DrawLine(startWorld - tickDirection * tickSize, startWorld + tickDirection * tickSize);
        Handles.DrawLine(endWorld - tickDirection * tickSize, endWorld + tickDirection * tickSize);
    }

    private static void DrawLocalRect(RectTransform root, Vector2 originLocal, Vector2 size, bool drawCenterLines)
    {
        Vector3 originWorld = root.TransformPoint(originLocal);
        if (size.x <= 0f && size.y <= 0f)
        {
            DrawCross(root, originWorld);
            return;
        }

        if (size.y <= 0f)
        {
            DrawLocalLine(root, originWorld, originLocal - Vector2.right * (size.x * 0.5f), originLocal + Vector2.right * (size.x * 0.5f));
            return;
        }

        if (size.x <= 0f)
        {
            DrawLocalLine(root, originWorld, originLocal - Vector2.up * (size.y * 0.5f), originLocal + Vector2.up * (size.y * 0.5f));
            return;
        }

        Vector2 halfSize = size * 0.5f;
        Vector2 topLeft = originLocal + new Vector2(-halfSize.x, halfSize.y);
        Vector2 topRight = originLocal + halfSize;
        Vector2 bottomRight = originLocal + new Vector2(halfSize.x, -halfSize.y);
        Vector2 bottomLeft = originLocal - halfSize;

        Handles.color = ShapeColor;
        DrawLocalPolyline(root, 4f, topLeft, topRight, bottomRight, bottomLeft, topLeft);

        if (!drawCenterLines)
            return;

        Color previousColor = Handles.color;
        Handles.color = new Color(ShapeColor.r, ShapeColor.g, ShapeColor.b, 0.45f);
        Handles.DrawDottedLine(root.TransformPoint(originLocal - Vector2.right * halfSize.x), root.TransformPoint(originLocal + Vector2.right * halfSize.x), 5f);
        Handles.DrawDottedLine(root.TransformPoint(originLocal - Vector2.up * halfSize.y), root.TransformPoint(originLocal + Vector2.up * halfSize.y), 5f);
        Handles.color = previousColor;
    }

    private static void DrawLocalEllipse(RectTransform root, Vector2 originLocal, Vector2 size, bool drawCenterLines)
    {
        Vector3 originWorld = root.TransformPoint(originLocal);
        if (size.x <= 0f && size.y <= 0f)
        {
            DrawCross(root, originWorld);
            return;
        }

        if (size.y <= 0f)
        {
            DrawLocalLine(root, originWorld, originLocal - Vector2.right * (size.x * 0.5f), originLocal + Vector2.right * (size.x * 0.5f));
            return;
        }

        if (size.x <= 0f)
        {
            DrawLocalLine(root, originWorld, originLocal - Vector2.up * (size.y * 0.5f), originLocal + Vector2.up * (size.y * 0.5f));
            return;
        }

        DrawLocalEllipseOutline(root, originLocal, size, ShapeColor, 72, 4f);

        if (!drawCenterLines)
            return;

        Vector2 halfSize = size * 0.5f;
        Color previousColor = Handles.color;
        Handles.color = new Color(ShapeColor.r, ShapeColor.g, ShapeColor.b, 0.45f);
        Handles.DrawDottedLine(root.TransformPoint(originLocal - Vector2.right * halfSize.x), root.TransformPoint(originLocal + Vector2.right * halfSize.x), 5f);
        Handles.DrawDottedLine(root.TransformPoint(originLocal - Vector2.up * halfSize.y), root.TransformPoint(originLocal + Vector2.up * halfSize.y), 5f);
        Handles.color = previousColor;
    }

    private static void DrawArcShape(
        RectTransform root,
        Vector2 originLocal,
        Vector3 originWorld,
        float radius,
        float directionAngle,
        float spreadAngle,
        bool drawSector)
    {
        if (radius <= 0f)
        {
            DrawCross(root, originWorld);
            return;
        }

        float clampedSpread = Mathf.Clamp(spreadAngle, 0f, 360f);
        if (clampedSpread >= 359.9f)
        {
            DrawLocalCircle(root, originLocal, radius, ShapeColor, 72, 4f);
            DrawDiameter(root, originWorld, root.TransformPoint(originLocal - Vector2.right * radius), root.TransformPoint(originLocal + Vector2.right * radius));
            return;
        }

        if (clampedSpread <= 0.1f)
        {
            DrawLocalLine(root, originWorld, originLocal, originLocal + AngleToVector(directionAngle) * radius);
            return;
        }

        float halfSpread = clampedSpread * 0.5f;
        float startAngle = directionAngle - halfSpread;
        Vector2 startLocal = originLocal + AngleToVector(startAngle) * radius;
        Vector2 endLocal = originLocal + AngleToVector(directionAngle + halfSpread) * radius;

        DrawLocalArc(root, originLocal, radius, startAngle, clampedSpread, 64, ShapeColor);

        if (!drawSector)
            return;

        Handles.color = ShapeColor;
        Handles.DrawDottedLine(originWorld, root.TransformPoint(startLocal), 4f);
        Handles.DrawDottedLine(originWorld, root.TransformPoint(endLocal), 4f);
    }

    private static void DrawDirection(
        RectTransform root,
        Vector2 originLocal,
        Vector3 originWorld,
        float directionAngle,
        float spreadAngle,
        float previewDistance)
    {
        Vector2 centerDirection = AngleToVector(directionAngle);
        Vector3 directionEnd = root.TransformPoint(originLocal + centerDirection * previewDistance);

        Handles.color = DirectionColor;
        Handles.DrawDottedLine(originWorld, directionEnd, 4f);
        DrawArrowHead(root, originWorld, directionEnd, DirectionColor);

        float halfSpread = spreadAngle * 0.5f;
        if (spreadAngle > 0.1f && spreadAngle < 359.9f)
        {
            Vector3 leftEnd = root.TransformPoint(originLocal + AngleToVector(directionAngle - halfSpread) * previewDistance);
            Vector3 rightEnd = root.TransformPoint(originLocal + AngleToVector(directionAngle + halfSpread) * previewDistance);

            Handles.color = SpreadColor;
            Handles.DrawDottedLine(originWorld, leftEnd, 4f);
            Handles.DrawDottedLine(originWorld, rightEnd, 4f);
            DrawLocalArc(root, originLocal, previewDistance, directionAngle - halfSpread, spreadAngle, 64, SpreadColor);
        }
        else if (spreadAngle >= 359.9f)
        {
            DrawLocalCircle(root, originLocal, previewDistance, SpreadColor, 96, 3f);
        }
    }

    private static void DrawCross(RectTransform root, Vector3 originWorld)
    {
        float size = HandleUtility.GetHandleSize(originWorld) * 0.16f;
        Handles.DrawLine(originWorld - root.right * size, originWorld + root.right * size);
        Handles.DrawLine(originWorld - root.up * size, originWorld + root.up * size);
    }

    private static void DrawLocalPolyline(RectTransform root, float dashSize, params Vector2[] points)
    {
        if (points == null || points.Length < 2)
            return;

        Vector3 previous = root.TransformPoint(points[0]);
        for (int i = 1; i < points.Length; i++)
        {
            Vector3 current = root.TransformPoint(points[i]);
            Handles.DrawDottedLine(previous, current, dashSize);
            previous = current;
        }
    }

    private static Vector3 LocalVectorToWorld(RectTransform root, Vector2 localVector)
    {
        Vector3 worldVector = root.right * localVector.x + root.up * localVector.y;
        return worldVector.sqrMagnitude > 0.0001f ? worldVector.normalized : root.up;
    }

    private static void DrawLocalCircle(
        RectTransform root,
        Vector2 originLocal,
        float radius,
        Color color,
        int segments,
        float dashSize)
    {
        if (radius <= 0f)
            return;

        segments = Mathf.Max(8, segments);
        Handles.color = color;

        Vector3 previous = root.TransformPoint(originLocal + Vector2.right * radius);
        for (int i = 1; i <= segments; i++)
        {
            float radians = (i / (float)segments) * Mathf.PI * 2f;
            Vector2 localPoint = originLocal + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
            Vector3 current = root.TransformPoint(localPoint);
            Handles.DrawDottedLine(previous, current, dashSize);
            previous = current;
        }
    }

    private static void DrawLocalEllipseOutline(
        RectTransform root,
        Vector2 originLocal,
        Vector2 size,
        Color color,
        int segments,
        float dashSize)
    {
        if (size.x <= 0f || size.y <= 0f)
            return;

        segments = Mathf.Max(8, segments);
        Handles.color = color;

        Vector2 halfSize = size * 0.5f;
        Vector3 previous = root.TransformPoint(originLocal + Vector2.right * halfSize.x);
        for (int i = 1; i <= segments; i++)
        {
            float radians = (i / (float)segments) * Mathf.PI * 2f;
            Vector2 localPoint = originLocal + new Vector2(
                Mathf.Cos(radians) * halfSize.x,
                Mathf.Sin(radians) * halfSize.y);
            Vector3 current = root.TransformPoint(localPoint);
            Handles.DrawDottedLine(previous, current, dashSize);
            previous = current;
        }
    }

    private static void DrawLocalArc(
        RectTransform root,
        Vector2 originLocal,
        float radius,
        float startAngle,
        float angleLength,
        int segments,
        Color color)
    {
        if (radius <= 0f)
            return;

        segments = Mathf.Max(2, segments);
        Handles.color = color;

        Vector3 previous = root.TransformPoint(originLocal + AngleToVector(startAngle) * radius);
        for (int i = 1; i <= segments; i++)
        {
            float angle = startAngle + angleLength * (i / (float)segments);
            Vector3 current = root.TransformPoint(originLocal + AngleToVector(angle) * radius);
            Handles.DrawDottedLine(previous, current, 4f);
            previous = current;
        }
    }

    private static void DrawArrowHead(RectTransform root, Vector3 origin, Vector3 end, Color color)
    {
        Vector3 direction = end - origin;
        if (direction.sqrMagnitude <= 0.001f)
            return;

        direction.Normalize();
        float size = HandleUtility.GetHandleSize(end) * 0.18f;
        Vector3 normal = ResolvePlaneNormal(root);
        Vector3 left = Quaternion.AngleAxis(150f, normal) * direction;
        Vector3 right = Quaternion.AngleAxis(-150f, normal) * direction;

        Handles.color = color;
        Handles.DrawLine(end, end + left * size);
        Handles.DrawLine(end, end + right * size);
    }

    private static Vector3 ResolvePlaneNormal(RectTransform root)
    {
        Vector3 normal = root != null ? root.forward : Vector3.forward;
        return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
    }

    private void ForEachTargetObject(System.Action<UIParticleEmitter> action)
    {
        Object[] currentTargets = targets;
        if (currentTargets == null || currentTargets.Length == 0)
            return;

        for (int i = 0; i < currentTargets.Length; i++)
        {
            if (currentTargets[i] is UIParticleEmitter emitter)
                action(emitter);
        }
    }

    private static Vector2 AngleToVector(float angleDegrees)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
    }

    private static float ResolvePreviewTravelDistance(Vector2 startSpeed, Vector2 startLifetime)
    {
        float speed = Mathf.Max(0f, Mathf.Max(startSpeed.x, startSpeed.y));
        float lifetime = Mathf.Max(0f, Mathf.Max(startLifetime.x, startLifetime.y));
        return speed * lifetime;
    }

    private static void TestPlay(UIParticleEmitter emitter)
    {
        if (emitter == null)
            return;

        if (Application.isPlaying)
            emitter.Stop(clear: true);
        else
            emitter.PrepareEditorPreview();

        emitter.Play();

        if (!Application.isPlaying)
            RegisterEditorPreview(emitter);

        EditorUtility.SetDirty(emitter);
        RepaintViews();
    }

    private static void StopPreview(UIParticleEmitter emitter)
    {
        if (emitter == null)
            return;

        emitter.Stop(clear: false);
        previewTimes.Remove(emitter);
        DisableEditorUpdateIfIdle();
        RepaintViews();
    }

    private static void ClearPreview(UIParticleEmitter emitter)
    {
        if (emitter == null)
            return;

        emitter.Stop(clear: true);
        previewTimes.Remove(emitter);

        if (!Application.isPlaying)
            emitter.DestroyEditorPreviewObjects();
        emitter.EndEditorPreview();

        DisableEditorUpdateIfIdle();
        RepaintViews();
    }

    private static void RegisterEditorPreview(UIParticleEmitter emitter)
    {
        previewTimes[emitter] = EditorApplication.timeSinceStartup;
        EditorApplication.update -= TickEditorPreviews;
        EditorApplication.update += TickEditorPreviews;
    }

    private static void TickEditorPreviews()
    {
        if (Application.isPlaying)
        {
            previewTimes.Clear();
            DisableEditorUpdateIfIdle();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        List<UIParticleEmitter> finished = null;
        List<UIParticleEmitter> emitters = new(previewTimes.Keys);

        for (int i = 0; i < emitters.Count; i++)
        {
            UIParticleEmitter emitter = emitters[i];
            if (emitter == null)
            {
                finished ??= new List<UIParticleEmitter>();
                finished.Add(emitter);
                continue;
            }

            double lastTime = previewTimes.TryGetValue(emitter, out double storedTime)
                ? storedTime
                : now;
            float deltaTime = Mathf.Clamp((float)(now - lastTime), 0f, 0.05f);
            previewTimes[emitter] = now;

            emitter.Simulate(deltaTime);
            if (!emitter.IsPlaying)
            {
                emitter.Stop(clear: true);
                emitter.DestroyEditorPreviewObjects();
                emitter.EndEditorPreview();
                finished ??= new List<UIParticleEmitter>();
                finished.Add(emitter);
            }
        }

        if (finished != null)
        {
            for (int i = 0; i < finished.Count; i++)
                previewTimes.Remove(finished[i]);
        }

        DisableEditorUpdateIfIdle();
        RepaintViews();
    }

    private static void DisableEditorUpdateIfIdle()
    {
        if (previewTimes.Count > 0)
            return;

        EditorApplication.update -= TickEditorPreviews;
    }

    private static void RepaintViews()
    {
        Canvas.ForceUpdateCanvases();
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }
}

using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIParticleEmitter))]
[CanEditMultipleObjects]
public sealed class UIParticleEmitterEditor : Editor
{
    private static readonly Dictionary<UIParticleEmitter, double> previewTimes = new();
    private static readonly Color ShapeColor = new(1f, 0.76f, 0.2f, 0.95f);
    private static readonly Color ShapeFillColor = new(1f, 0.76f, 0.2f, 0.08f);
    private static readonly Color DirectionColor = new(1f, 0.35f, 0.08f, 0.95f);
    private static readonly Color SpeedColor = new(0.35f, 0.85f, 1f, 0.9f);
    private static readonly Color RectPlaneColor = new(0.35f, 0.85f, 1f, 0.18f);

    private static readonly FieldInfo ParticleRootField = typeof(UIParticleEmitter).GetField("particleRoot", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ShapeField = typeof(UIParticleEmitter).GetField("shape", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ShapeRadiusField = typeof(UIParticleEmitter).GetField("shapeRadius", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo EmitterOffsetField = typeof(UIParticleEmitter).GetField("emitterOffset", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo DirectionAngleField = typeof(UIParticleEmitter).GetField("directionAngle", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo SpreadAngleField = typeof(UIParticleEmitter).GetField("spreadAngle", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo StartSpeedField = typeof(UIParticleEmitter).GetField("startSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ShowGizmosField = typeof(UIParticleEmitter).GetField("showGizmos", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo ShowGizmosOnlyWhenSelectedField = typeof(UIParticleEmitter).GetField("showGizmosOnlyWhenSelected", BindingFlags.Instance | BindingFlags.NonPublic);

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

    private static void TestPlay(UIParticleEmitter emitter)
    {
        if (emitter == null)
            return;

        emitter.Stop(clear: true);
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
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
    private static void DrawEmitterGizmosForScene(UIParticleEmitter emitter, GizmoType gizmoType)
    {
        if (emitter == null || !GetFieldValue<bool>(ShowGizmosField, emitter))
            return;

        bool selectedOnly = GetFieldValue<bool>(ShowGizmosOnlyWhenSelectedField, emitter);
        bool selected = (gizmoType & GizmoType.Selected) != 0
            || (gizmoType & GizmoType.InSelectionHierarchy) != 0;
        if (selectedOnly && !selected)
            return;

        DrawEmitterGizmos(emitter);
    }

    private static void DrawEmitterGizmos(UIParticleEmitter emitter)
    {
        RectTransform root = ResolveParticleRoot(emitter);
        if (root == null)
            return;

        Vector2 offset = GetFieldValue<Vector2>(EmitterOffsetField, emitter);
        Vector3 origin = root.TransformPoint(offset);
        float rootScale = ResolveRootScale(root);
        float shapeRadius = GetFieldValue<float>(ShapeRadiusField, emitter) * rootScale;
        UIParticleShape shape = GetFieldValue<UIParticleShape>(ShapeField, emitter);
        float directionAngle = GetFieldValue<float>(DirectionAngleField, emitter);
        float spreadAngle = GetFieldValue<float>(SpreadAngleField, emitter);
        Vector2 startSpeed = GetFieldValue<Vector2>(StartSpeedField, emitter);
        float previewSpeed = Mathf.Max(0f, Mathf.Max(startSpeed.x, startSpeed.y)) * 0.16f * rootScale;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        DrawRectPlane(root);
        DrawShape(origin, shape, shapeRadius, root);
        DrawDirection(origin, directionAngle, spreadAngle, previewSpeed, root);

        Handles.color = Color.white;
        Handles.Label(origin, "UI Particle");
    }

    private static void DrawRectPlane(RectTransform root)
    {
        Vector3[] corners = new Vector3[4];
        root.GetWorldCorners(corners);

        Handles.color = RectPlaneColor;
        Handles.DrawAAPolyLine(2f, corners[0], corners[1], corners[2], corners[3], corners[0]);
    }

    private static void DrawShape(Vector3 origin, UIParticleShape shape, float radius, RectTransform root)
    {
        Handles.color = ShapeColor;
        Vector3 normal = ResolveCanvasNormal(root);

        switch (shape)
        {
            case UIParticleShape.Circle:
                Handles.color = ShapeFillColor;
                Handles.DrawSolidDisc(origin, normal, radius);
                Handles.color = ShapeColor;
                Handles.DrawWireDisc(origin, normal, radius);
                break;

            case UIParticleShape.Ring:
                Handles.DrawWireDisc(origin, normal, radius);
                break;

            case UIParticleShape.Point:
            default:
                float size = HandleUtility.GetHandleSize(origin) * 0.12f;
                Vector3 right = root.right;
                Vector3 up = root.up;
                Handles.DrawLine(origin - right * size, origin + right * size);
                Handles.DrawLine(origin - up * size, origin + up * size);
                break;
        }
    }

    private static void DrawDirection(Vector3 origin, float directionAngle, float spreadAngle, float length, RectTransform root)
    {
        Handles.color = DirectionColor;
        Vector3 centerDirection = AngleToWorldVector(directionAngle, root);
        DrawArrow(origin, centerDirection, Mathf.Max(length, 24f));

        float halfSpread = spreadAngle * 0.5f;
        if (spreadAngle > 0.1f && spreadAngle < 359.9f)
        {
            Vector3 leftDirection = AngleToWorldVector(directionAngle - halfSpread, root);
            Vector3 rightDirection = AngleToWorldVector(directionAngle + halfSpread, root);
            float spreadLength = Mathf.Max(length * 0.78f, 18f);
            Handles.DrawLine(origin, origin + leftDirection * spreadLength);
            Handles.DrawLine(origin, origin + rightDirection * spreadLength);
            Handles.DrawWireArc(origin, ResolveCanvasNormal(root), leftDirection, spreadAngle, spreadLength);
        }
        else if (spreadAngle >= 359.9f)
        {
            Handles.color = SpeedColor;
            Handles.DrawWireDisc(origin, ResolveCanvasNormal(root), Mathf.Max(length * 0.55f, 16f));
        }
    }

    private static void DrawArrow(Vector3 origin, Vector3 direction, float length)
    {
        Vector3 end = origin + direction * length;
        Handles.DrawLine(origin, end);

        float headSize = Mathf.Max(length * 0.16f, 8f);
        Vector3 left = Quaternion.Euler(0f, 0f, 150f) * direction;
        Vector3 right = Quaternion.Euler(0f, 0f, -150f) * direction;
        Handles.DrawLine(end, end + left * headSize);
        Handles.DrawLine(end, end + right * headSize);
    }

    private static RectTransform ResolveParticleRoot(UIParticleEmitter emitter)
    {
        RectTransform root = GetFieldValue<RectTransform>(ParticleRootField, emitter);
        if (root != null)
            return root;

        return emitter.transform as RectTransform;
    }

    private static float ResolveRootScale(RectTransform root)
    {
        Vector3 lossyScale = root.lossyScale;
        return Mathf.Max(0.0001f, (Mathf.Abs(lossyScale.x) + Mathf.Abs(lossyScale.y)) * 0.5f);
    }

    private static Vector3 ResolveCanvasNormal(RectTransform root)
    {
        Vector3 normal = root.forward;
        return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.forward;
    }

    private static Vector3 AngleToWorldVector(float angleDegrees, RectTransform root)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector3 localVector = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        if (root == null)
            return localVector;

        Vector3 worldVector = (root.right * localVector.x) + (root.up * localVector.y);
        return worldVector.sqrMagnitude > 0.0001f ? worldVector.normalized : root.right;
    }

    private static T GetFieldValue<T>(FieldInfo field, UIParticleEmitter emitter)
    {
        if (field == null || emitter == null)
            return default;

        object value = field.GetValue(emitter);
        return value is T typedValue ? typedValue : default;
    }
}

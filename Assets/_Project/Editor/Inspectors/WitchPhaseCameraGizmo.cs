using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only preview of Chloe's phase camera after moving to the arena center.</summary>
public static class WitchPhaseCameraGizmo
{
    [DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy)]
    private static void DrawPhaseCamera(Witch witch, GizmoType gizmoType)
    {
        CameraPresentationDirector director = witch.GetComponent<CameraPresentationDirector>();
        if (director == null)
            return;

        using var settings = new SerializedObject(director);
        CinemachineCamera camera = settings.FindProperty("bossCam").objectReferenceValue as CinemachineCamera;
        if (camera == null)
            camera = CameraBootstrap.FindSceneBossCamera(witch.gameObject.scene);
        if (camera == null || camera.Lens.ModeOverride == LensSettings.OverrideModes.Perspective)
            return;

        Transform target = camera.Follow;
        CinemachineFollow follow = camera.GetComponent<CinemachineFollow>();
        if (target == null || follow == null || !follow.enabled)
            return;

        // These are the authored top-down binding modes. Do not guess other tracking behaviours.
        BindingMode binding = follow.TrackerSettings.BindingMode;
        if (binding != BindingMode.LockToTarget && binding != BindingMode.WorldSpace)
            return;

        Vector3 focus = target.position;
        if (target == witch.transform || target.IsChildOf(witch.transform))
            focus += witch.GetPhaseTransitionCenter() - witch.transform.position;

        Quaternion orientation = camera.transform.rotation;
        Quaternion offsetRotation = binding == BindingMode.WorldSpace ? Quaternion.identity : target.rotation;
        Vector2 phaseOffset = settings.FindProperty("phaseCameraOffset").vector2Value;
        Vector3 cameraPosition = focus + offsetRotation * (follow.FollowOffset + (Vector3)phaseOffset);
        float scale = settings.FindProperty("useBossLensPresentation").boolValue
            ? Mathf.Max(0.01f, settings.FindProperty("phaseLensScale").floatValue)
            : 1f;
        float size = camera.Lens.OrthographicSize * scale;

        // In Play Mode show the live BossCam instead of multiplying an already zoomed lens again.
        if (Application.isPlaying)
        {
            cameraPosition = camera.State.GetFinalPosition();
            orientation = camera.State.GetFinalOrientation();
            size = camera.State.Lens.OrthographicSize;
        }

        Vector2 gameViewSize = Handles.GetMainGameViewSize();
        if (gameViewSize.x <= 0f || gameViewSize.y <= 0f)
            return;
        float aspect = gameViewSize.x / gameViewSize.y;

        // Project the center onto the gameplay plane so the frame is readable in 2D Scene view.
        Vector3 forward = orientation * Vector3.forward;
        if (Mathf.Abs(forward.z) < 0.001f)
            return;
        Vector3 center = cameraPosition + forward * ((focus.z - cameraPosition.z) / forward.z);
        Vector3 right = orientation * Vector3.right * (size * aspect);
        Vector3 up = orientation * Vector3.up * size;
        Vector3[] corners =
        {
            center - right - up, center - right + up,
            center + right + up, center + right - up, center - right - up
        };

        using (new Handles.DrawingScope(new Color(0.2f, 0.9f, 1f, 1f)))
        {
            Handles.DrawAAPolyLine(2f, corners);
            float markerSize = HandleUtility.GetHandleSize(center) * 0.06f;
            Handles.DrawLine(center - Vector3.right * markerSize, center + Vector3.right * markerSize);
            Handles.DrawLine(center - Vector3.up * markerSize, center + Vector3.up * markerSize);
            Handles.DrawDottedLine(focus, center, 4f);
            string title = Application.isPlaying ? "Chloe BossCam (live)" : "Chloe Phase Camera (settled preview)";
            Handles.Label(center + up,
                $"{title}\nSize {size:0.##} | Aspect {aspect:0.###} | Center ({center.x:0.##}, {center.y:0.##})");
        }
    }
}

[CustomEditor(typeof(CameraPresentationDirector)), CanEditMultipleObjects]
public sealed class CameraPresentationDirectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var director = (CameraPresentationDirector)target;
        if (director.TryGetComponent<Witch>(out _) && director.GetComponent<CameraPresentationDirector>() != director)
        {
            EditorGUILayout.HelpBox(
                "Chloe's phase and gizmo use the FIRST Camera Presentation Director on this object. " +
                "Edit Phase Lens Scale / Phase Camera Offset on that component.", MessageType.Warning);
        }

        if (DrawDefaultInspector())
            SceneView.RepaintAll();

        if (((CameraPresentationDirector)target).TryGetComponent<Witch>(out _))
        {
            EditorGUILayout.HelpBox(
                "Phase Lens Scale changes the preview frame size. Phase Camera Offset adds an XY offset " +
                "to BossCam's Follow Offset for the phase only (positive Y moves up in this rig). " +
                "Scene Gizmos shows the settled phase preview in Edit Mode and the live BossCam in Play Mode.",
                MessageType.Info);
        }
    }
}

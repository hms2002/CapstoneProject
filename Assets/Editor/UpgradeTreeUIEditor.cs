#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UpgradeTreeUILakePreviewLoop
{
    private const double RepaintInterval = 1.0 / 30.0;
    private static double lastRepaintTime;

    static UpgradeTreeUILakePreviewLoop()
    {
        EditorApplication.update += UpdateLakePreviews;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += RestoreAllPreviewMaterials;
    }

    private static void UpdateLakePreviews()
    {
        if (Application.isPlaying)
            return;

        bool updatedAny = false;
        UpgradeTreeUI[] previews = Resources.FindObjectsOfTypeAll<UpgradeTreeUI>();
        foreach (UpgradeTreeUI preview in previews)
        {
            if (preview == null || !preview.ShouldAnimateLakePreviewInEditor)
                continue;

            preview.TickLakePreviewInEditor();
            updatedAny = true;
        }

        if (!updatedAny)
            return;

        double now = EditorApplication.timeSinceStartup;
        if (now - lastRepaintTime < RepaintInterval)
            return;

        lastRepaintTime = now;
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            RestoreAllPreviewMaterials();
        }
    }

    private static void RestoreAllPreviewMaterials()
    {
        UpgradeTreeUI[] previews = Resources.FindObjectsOfTypeAll<UpgradeTreeUI>();
        foreach (UpgradeTreeUI preview in previews)
        {
            if (preview != null)
                preview.RestoreLakePreviewMaterialInEditor(disableAnimation: false);
        }
    }
}

[CustomEditor(typeof(UpgradeTreeUI))]
[CanEditMultipleObjects]
public sealed class UpgradeTreeUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        bool changed = DrawDefaultInspector();

        GUILayout.Space(8f);
        EditorGUILayout.LabelField("Lake Material Workflow", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use the lake surface material as the visual tuning source. Animated Edit Mode preview uses a temporary material copy, so only runtime time values change.",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(Application.isPlaying);
        if (targets.Length == 1 && target is UpgradeTreeUI selectedUi)
            EditorGUILayout.LabelField("Test Preview", selectedUi.IsLakePreviewTestActiveInEditor ? "Running" : "Stopped");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Start Test Preview"))
            ForEachTarget(ui => ui.StartLakePreviewTestInEditor());

        if (GUILayout.Button("Stop Test Preview"))
            ForEachTarget(ui => ui.StopLakePreviewTestInEditor());

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Lake Preview"))
            ForEachTarget(ui => ui.RefreshLakePreviewInEditor());

        if (GUILayout.Button("Settings -> Material"))
            ForEachTarget(ui => ui.ApplyLakeSettingsToMaterial());

        if (GUILayout.Button("Material -> Settings"))
            ForEachTarget(ui => ui.ReadLakeSettingsFromMaterial());

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Test Ripple"))
            ForEachTarget(ui => ui.TestLakeRippleInEditor());

        if (GUILayout.Button("Test Wake"))
            ForEachTarget(ui => ui.TestLakeWakeInEditor());

        if (GUILayout.Button("Clear Interaction"))
            ForEachTarget(ui => ui.ClearLakeInteractionPreviewInEditor());

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Restore Material Slot"))
            ForEachTarget(ui => ui.RestoreLakePreviewMaterialInEditor());

        EditorGUI.EndDisabledGroup();

        if (changed && !Application.isPlaying)
            ForEachTarget(ui => ui.RefreshLakePreviewInEditor());
    }

    private void ForEachTarget(System.Action<UpgradeTreeUI> action)
    {
        foreach (UnityEngine.Object targetObject in targets)
        {
            if (targetObject is UpgradeTreeUI ui)
                action(ui);
        }
    }
}
#endif

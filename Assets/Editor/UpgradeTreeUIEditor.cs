#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class UpgradeTreeUILakePreviewLoop
{
    private static readonly bool AutomaticLakePreviewLoopEnabled = false;
    private const double RepaintInterval = 1.0 / 30.0;
    private static double lastRepaintTime;

    static UpgradeTreeUILakePreviewLoop()
    {
        // Keep edit-mode lake preview manual. Automatic editor callbacks previously ran during
        // Inspector/layout recovery and could destabilize Unity's internal PropertyEditor.
        if (!AutomaticLakePreviewLoopEnabled)
            return;

        EditorApplication.update += UpdateLakePreviews;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        AssemblyReloadEvents.beforeAssemblyReload += RestoreAllPreviewMaterials;
    }

    private static void UpdateLakePreviews()
    {
        if (Application.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        bool updatedAny = false;
        UpgradeTreeUI[] previews = Resources.FindObjectsOfTypeAll<UpgradeTreeUI>();
        foreach (UpgradeTreeUI preview in previews)
        {
            if (!CanUseEditorLakePreview(preview) || !preview.ShouldAnimateLakePreviewInEditor)
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
            if (CanUseEditorLakePreview(preview))
                preview.RestoreLakePreviewMaterialInEditor(disableAnimation: false);
        }
    }

    private static bool CanUseEditorLakePreview(UpgradeTreeUI preview)
    {
        return preview != null &&
               preview.gameObject != null &&
               preview.gameObject.scene.IsValid() &&
               preview.gameObject.scene.isLoaded &&
               !EditorUtility.IsPersistent(preview) &&
               UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(preview.gameObject) == null;
    }
}

[CustomEditor(typeof(UpgradeTreeUI))]
[CanEditMultipleObjects]
public sealed class UpgradeTreeUIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);
        EditorGUILayout.LabelField("Lake Material Workflow", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Lake preview is manual only. Use the refresh and test buttons when needed; automatic edit-mode animation is disabled for Inspector stability.",
            MessageType.Info);

        EditorGUI.BeginDisabledGroup(Application.isPlaying);
        if (targets.Length == 1 && target is UpgradeTreeUI selectedUi)
            EditorGUILayout.LabelField("Manual Test Preview", selectedUi.IsLakePreviewTestActiveInEditor ? "Prepared" : "Stopped");

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

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AffectionGainScreenEffect))]
[CanEditMultipleObjects]
public sealed class AffectionGainScreenEffectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Play Preview runs the same animation used by dialogue affection gain."
                : "Show Static Preview displays the border and pooled hearts without entering the dialogue flow.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            string previewLabel = Application.isPlaying ? "Play Preview" : "Show Static Preview";
            if (GUILayout.Button(previewLabel))
                ForEachTarget(effect => effect.Preview(), "Preview Affection Gain Effect");

            if (GUILayout.Button("Clear Preview"))
                ForEachTarget(effect => effect.ClearPreview(), "Clear Affection Gain Preview");
        }
    }

    private void ForEachTarget(System.Action<AffectionGainScreenEffect> action, string undoName)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            AffectionGainScreenEffect effect = targets[i] as AffectionGainScreenEffect;
            if (effect == null)
                continue;

            if (!Application.isPlaying)
                Undo.RegisterFullObjectHierarchyUndo(effect.gameObject, undoName);

            action(effect);

            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(effect);
                EditorUtility.SetDirty(effect.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
            }
        }

        SceneView.RepaintAll();
    }
}

[CustomEditor(typeof(ChoiceFailureScreenEffect))]
[CanEditMultipleObjects]
public sealed class ChoiceFailureScreenEffectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            string previewLabel = Application.isPlaying ? "Play Preview" : "Show Static Preview";
            if (GUILayout.Button(previewLabel))
                ForEachTarget(effect => effect.Preview(), "Preview Choice Failure Effect");

            if (GUILayout.Button("Clear Preview"))
                ForEachTarget(effect => effect.ClearPreview(), "Clear Choice Failure Preview");
        }
    }

    private void ForEachTarget(System.Action<ChoiceFailureScreenEffect> action, string undoName)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            ChoiceFailureScreenEffect effect = targets[i] as ChoiceFailureScreenEffect;
            if (effect == null)
                continue;

            if (!Application.isPlaying)
                Undo.RegisterFullObjectHierarchyUndo(effect.gameObject, undoName);

            action(effect);

            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(effect);
                EditorUtility.SetDirty(effect.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
            }
        }

        SceneView.RepaintAll();
    }
}

[CustomEditor(typeof(AffectionGradientBorderGraphic))]
[CanEditMultipleObjects]
public sealed class AffectionGradientBorderGraphicEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Affection Preview", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            string previewLabel = Application.isPlaying ? "Play Preview" : "Show Static Preview";
            if (GUILayout.Button(previewLabel))
                ForEachTarget(effect => effect.Preview(), "Preview Affection Gain Effect");

            if (GUILayout.Button("Clear Preview"))
                ForEachTarget(effect => effect.ClearPreview(), "Clear Affection Gain Preview");
        }
    }

    private void ForEachTarget(System.Action<AffectionGainScreenEffect> action, string undoName)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            AffectionGradientBorderGraphic graphic = targets[i] as AffectionGradientBorderGraphic;
            if (graphic == null)
                continue;

            AffectionGainScreenEffect effect = ResolveEffect(graphic);
            if (effect == null)
                continue;

            if (!Application.isPlaying)
                Undo.RegisterFullObjectHierarchyUndo(effect.gameObject, undoName);

            action(effect);

            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(graphic);
                EditorUtility.SetDirty(effect);
                EditorUtility.SetDirty(effect.gameObject);
                PrefabUtility.RecordPrefabInstancePropertyModifications(graphic);
                PrefabUtility.RecordPrefabInstancePropertyModifications(effect);
            }
        }

        SceneView.RepaintAll();
    }

    private static AffectionGainScreenEffect ResolveEffect(AffectionGradientBorderGraphic graphic)
    {
        Transform current = graphic != null ? graphic.transform : null;
        while (current != null)
        {
            AffectionGainScreenEffect effect = current.GetComponent<AffectionGainScreenEffect>();
            if (effect != null)
                return effect;

            current = current.parent;
        }

        return null;
    }
}

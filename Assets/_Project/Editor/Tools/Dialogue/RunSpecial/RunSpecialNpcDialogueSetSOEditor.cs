using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RunSpecialNpcDialogueSetSO))]
public sealed class RunSpecialNpcDialogueSetSOEditor : Editor
{
    private const float DefaultLineDuration = 2.5f;

    private static readonly Color DefaultBorderColor = Color.black;
    private static readonly Color DefaultFillColor = new(1f, 1f, 1f, 0.52f);
    private static readonly Color DefaultFontColor = Color.black;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        NormalizeLineDefaults();

        SerializedProperty featureKind = serializedObject.FindProperty("featureKind");
        EditorGUILayout.PropertyField(featureKind);
        EditorGUILayout.Space();

        RunSpecialNpcFeatureKind kind = (RunSpecialNpcFeatureKind)featureKind.enumValueIndex;
        if (kind == RunSpecialNpcFeatureKind.Construction)
        {
            DrawBranch("constructionNotStarted", "Not Started");
            DrawBranch("constructionPending", "Pending");
            DrawBranch("constructionCompleted", "Completed");
        }
        else if (kind == RunSpecialNpcFeatureKind.SameSceneTeleport)
        {
            DrawBranch("teleportAvailable", "Available");
            DrawBranch("teleportLocked", "Locked");
            DrawBranch("teleportUnavailable", "Unavailable");
        }

        NormalizeLineDefaults();
        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBranch(string propertyName, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren: true);
    }

    private void NormalizeLineDefaults()
    {
        NormalizeBranch("constructionNotStarted");
        NormalizeBranch("constructionInsufficientFunds");
        NormalizeBranch("constructionPending");
        NormalizeBranch("constructionCompleted");
        NormalizeBranch("teleportAvailable");
        NormalizeBranch("teleportLocked");
        NormalizeBranch("teleportUnavailable");
    }

    private void NormalizeBranch(string propertyName)
    {
        SerializedProperty branch = serializedObject.FindProperty(propertyName);
        if (branch == null)
            return;

        NormalizeLineArray(branch.FindPropertyRelative("lines"));

        SerializedProperty choices = branch.FindPropertyRelative("choices");
        if (choices == null || !choices.isArray)
            return;

        for (int i = 0; i < choices.arraySize; i++)
        {
            SerializedProperty choice = choices.GetArrayElementAtIndex(i);
            NormalizeLineArray(choice.FindPropertyRelative("responseLines"));
            NormalizeLineArray(choice.FindPropertyRelative("unavailableResponseLines"));
        }
    }

    private static void NormalizeLineArray(SerializedProperty lines)
    {
        if (lines == null || !lines.isArray)
            return;

        for (int i = 0; i < lines.arraySize; i++)
            NormalizeLine(lines.GetArrayElementAtIndex(i));
    }

    private static void NormalizeLine(SerializedProperty line)
    {
        if (line == null)
            return;

        SerializedProperty duration = line.FindPropertyRelative("duration");
        if (duration != null && duration.floatValue <= 0f)
            duration.floatValue = DefaultLineDuration;

        SerializedProperty theme = line.FindPropertyRelative("theme");
        if (theme == null)
            return;

        SerializedProperty borderColor = theme.FindPropertyRelative("borderColor");
        SerializedProperty fillColor = theme.FindPropertyRelative("fillColor");
        SerializedProperty fontColor = theme.FindPropertyRelative("fontColor");
        if (!IsColorProperty(borderColor) ||
            !IsColorProperty(fillColor) ||
            !IsColorProperty(fontColor))
        {
            return;
        }

        if (!IsZeroColor(borderColor.colorValue) ||
            !IsZeroColor(fillColor.colorValue) ||
            !IsZeroColor(fontColor.colorValue))
        {
            return;
        }

        borderColor.colorValue = DefaultBorderColor;
        fillColor.colorValue = DefaultFillColor;
        fontColor.colorValue = DefaultFontColor;
    }

    private static bool IsColorProperty(SerializedProperty property)
    {
        return property != null && property.propertyType == SerializedPropertyType.Color;
    }

    private static bool IsZeroColor(Color color)
    {
        return Mathf.Approximately(color.r, 0f) &&
               Mathf.Approximately(color.g, 0f) &&
               Mathf.Approximately(color.b, 0f) &&
               Mathf.Approximately(color.a, 0f);
    }
}

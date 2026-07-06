using UnityEditor;
using UnityEngine;

// 책임: CameraShakeHook 직렬화 필드를 인스펙터에서 조작하기 쉬운 형태로 그린다.
[CustomPropertyDrawer(typeof(CameraShakeHook))]
public sealed class CameraShakeHookDrawer : PropertyDrawer
{
    private const float TestButtonWidth = 88f;
    private const float BottomPadding = 6f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty amplitudeProperty = property.FindPropertyRelative("amplitude");
        SerializedProperty amplitudeMultiplierProperty = property.FindPropertyRelative("amplitudeMultiplier");
        SerializedProperty maxAmplitudeProperty = property.FindPropertyRelative("maxAmplitude");
        SerializedProperty minIntervalProperty = property.FindPropertyRelative("minIntervalSeconds");
        SerializedProperty directionModeProperty = property.FindPropertyRelative("directionMode");
        SerializedProperty customDirectionProperty = property.FindPropertyRelative("customDirection");
        SerializedProperty ignoreSettingProperty = property.FindPropertyRelative("ignoreScreenShakeSetting");

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        Rect lineRect = new Rect(position.x, position.y, position.width, lineHeight);

        EditorGUI.BeginProperty(position, label, property);

        Rect headerRect = new Rect(
            lineRect.x,
            lineRect.y,
            lineRect.width,
            lineHeight);
        Rect buttonRect = new Rect(lineRect.xMax - TestButtonWidth, lineRect.y, TestButtonWidth, lineHeight);

        property.isExpanded = EditorGUI.Foldout(
            headerRect,
            property.isExpanded,
            BuildHeader(label.text, amplitudeProperty, amplitudeMultiplierProperty, maxAmplitudeProperty),
            true);

        lineRect.y += lineHeight + spacing;
        buttonRect = new Rect(lineRect.x, lineRect.y, lineRect.width, lineHeight);

        using (new EditorGUI.DisabledScope(!CanPreviewButton(amplitudeProperty, amplitudeMultiplierProperty, maxAmplitudeProperty)))
        {
            if (GUI.Button(buttonRect, "Test Shake"))
                PreviewShake(property, label, amplitudeProperty, amplitudeMultiplierProperty, maxAmplitudeProperty, minIntervalProperty, directionModeProperty, customDirectionProperty, ignoreSettingProperty);
        }

        if (property.isExpanded)
        {
            lineRect.y += lineHeight + spacing;
            DrawPropertyLine(ref lineRect, amplitudeProperty, spacing);
            DrawPropertyLine(ref lineRect, amplitudeMultiplierProperty, spacing);
            DrawPropertyLine(ref lineRect, maxAmplitudeProperty, spacing);
            DrawPropertyLine(ref lineRect, minIntervalProperty, spacing);
            DrawPropertyLine(ref lineRect, directionModeProperty, spacing);

            if ((CameraShakeDirectionMode)directionModeProperty.enumValueIndex == CameraShakeDirectionMode.UseCustom)
                DrawPropertyLine(ref lineRect, customDirectionProperty, spacing);

            DrawPropertyLine(ref lineRect, ignoreSettingProperty, spacing);
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float height = (lineHeight * 2f) + spacing;

        if (!property.isExpanded)
            return height;

        height += GetExpandedPropertyHeight(property.FindPropertyRelative("amplitude"), spacing);
        height += GetExpandedPropertyHeight(property.FindPropertyRelative("amplitudeMultiplier"), spacing);
        height += GetExpandedPropertyHeight(property.FindPropertyRelative("maxAmplitude"), spacing);
        height += GetExpandedPropertyHeight(property.FindPropertyRelative("minIntervalSeconds"), spacing);
        height += GetExpandedPropertyHeight(property.FindPropertyRelative("directionMode"), spacing);

        SerializedProperty directionModeProperty = property.FindPropertyRelative("directionMode");
        if ((CameraShakeDirectionMode)directionModeProperty.enumValueIndex == CameraShakeDirectionMode.UseCustom)
            height += GetExpandedPropertyHeight(property.FindPropertyRelative("customDirection"), spacing);

        height += GetExpandedPropertyHeight(property.FindPropertyRelative("ignoreScreenShakeSetting"), spacing);

        return height + BottomPadding;
    }

    private static void DrawPropertyLine(ref Rect lineRect, SerializedProperty property, float spacing)
    {
        float propertyHeight = EditorGUI.GetPropertyHeight(property, true);
        lineRect.height = propertyHeight;
        EditorGUI.PropertyField(lineRect, property, true);
        lineRect.y += propertyHeight + spacing;
        lineRect.height = EditorGUIUtility.singleLineHeight;
    }

    private static float GetExpandedPropertyHeight(SerializedProperty property, float spacing)
    {
        return EditorGUI.GetPropertyHeight(property, true) + spacing;
    }

    private static string BuildHeader(
        string label,
        SerializedProperty amplitudeProperty,
        SerializedProperty amplitudeMultiplierProperty,
        SerializedProperty maxAmplitudeProperty)
    {
        float previewAmplitude = CalculatePreviewAmplitude(
            amplitudeProperty.floatValue,
            amplitudeMultiplierProperty.floatValue,
            maxAmplitudeProperty.floatValue);

        return previewAmplitude > 0f
            ? $"{label} ({previewAmplitude:0.###})"
            : label;
    }

    private static bool CanPreview(
        SerializedProperty amplitudeProperty,
        SerializedProperty amplitudeMultiplierProperty,
        SerializedProperty maxAmplitudeProperty)
    {
        return CalculatePreviewAmplitude(
                   amplitudeProperty.floatValue,
                   amplitudeMultiplierProperty.floatValue,
                   maxAmplitudeProperty.floatValue) > 0f;
    }

    private static bool CanPreviewButton(
        SerializedProperty amplitudeProperty,
        SerializedProperty amplitudeMultiplierProperty,
        SerializedProperty maxAmplitudeProperty)
    {
        if (!CanPreview(amplitudeProperty, amplitudeMultiplierProperty, maxAmplitudeProperty))
            return false;

        return Application.isPlaying || CameraShakeHookPreview.CanPreview();
    }

    private static float CalculatePreviewAmplitude(float amplitude, float multiplier, float maxAmplitude)
    {
        float safeAmplitude = Mathf.Max(0f, amplitude);
        float safeMultiplier = Mathf.Approximately(multiplier, 0f) ? 1f : Mathf.Max(0f, multiplier);
        float result = safeAmplitude * safeMultiplier;

        if (maxAmplitude > 0f)
            result = Mathf.Min(maxAmplitude, result);

        return result;
    }

    private static void PreviewShake(
        SerializedProperty property,
        GUIContent label,
        SerializedProperty amplitudeProperty,
        SerializedProperty amplitudeMultiplierProperty,
        SerializedProperty maxAmplitudeProperty,
        SerializedProperty minIntervalProperty,
        SerializedProperty directionModeProperty,
        SerializedProperty customDirectionProperty,
        SerializedProperty ignoreSettingProperty)
    {
        float amplitude = CalculatePreviewAmplitude(
            amplitudeProperty.floatValue,
            amplitudeMultiplierProperty.floatValue,
            maxAmplitudeProperty.floatValue);
        if (amplitude <= 0f)
            return;

        Vector3 direction = ResolvePreviewDirection(directionModeProperty, customDirectionProperty);

        if (Application.isPlaying)
        {
            Object owner = property.serializedObject.targetObject;
            GameObject source = (owner as Component)?.gameObject;
            string debugReason = owner != null ? $"{owner.name}.{label.text}.Preview" : $"{label.text}.Preview";

            CameraShakePlayback.Play(new CameraShakeRequest(
                amplitude,
                direction,
                source,
                minIntervalProperty.floatValue,
                debugReason,
                ignoreSettingProperty.boolValue));
            return;
        }

        CameraShakeHookPreview.Preview(amplitude, direction);
    }

    private static Vector3 ResolvePreviewDirection(
        SerializedProperty directionModeProperty,
        SerializedProperty customDirectionProperty)
    {
        if ((CameraShakeDirectionMode)directionModeProperty.enumValueIndex == CameraShakeDirectionMode.UseCustom)
        {
            Vector3 customDirection = customDirectionProperty.vector3Value;
            customDirection.z = 0f;
            if (customDirection.sqrMagnitude > 0.0001f)
                return customDirection.normalized;
        }

        return Vector3.up;
    }
}

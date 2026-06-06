using UnityEditor;
using UnityEngine;

/// <summary>
/// 책임:
/// - SortingLayerNameAttribute가 붙은 문자열 필드를 Unity Sorting Layer 드롭다운으로 그린다.
/// - 존재하지 않는 기존 문자열은 선택지 끝에 표시해 데이터 손실 없이 사용자가 교정할 수 있게 한다.
/// </summary>
[CustomPropertyDrawer(typeof(SortingLayerNameAttribute))]
public sealed class SortingLayerNameDrawer : PropertyDrawer
{
    private const string NoneOptionLabel = "None / 변경 안 함";
    private const string MissingPrefix = "Missing: ";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        SortingLayer[] layers = SortingLayer.layers;
        string currentValue = property.stringValue;
        int layerCount = layers?.Length ?? 0;
        bool hasCurrentLayer = string.IsNullOrEmpty(currentValue);

        for (int i = 0; i < layerCount; i++)
        {
            if (layers[i].name == currentValue)
            {
                hasCurrentLayer = true;
                break;
            }
        }

        int optionCount = 1 + layerCount + (hasCurrentLayer ? 0 : 1);
        string[] options = new string[optionCount];
        options[0] = NoneOptionLabel;

        int selectedIndex = 0;
        for (int i = 0; i < layerCount; i++)
        {
            string layerName = layers[i].name;
            int optionIndex = i + 1;
            options[optionIndex] = layerName;

            if (layerName == currentValue)
                selectedIndex = optionIndex;
        }

        if (!hasCurrentLayer)
        {
            selectedIndex = optionCount - 1;
            options[selectedIndex] = $"{MissingPrefix}{currentValue}";
        }

        EditorGUI.BeginProperty(position, label, property);
        Rect popupPosition = EditorGUI.PrefixLabel(position, label);
        int nextIndex = EditorGUI.Popup(popupPosition, selectedIndex, options);
        EditorGUI.EndProperty();

        if (nextIndex == selectedIndex)
            return;

        if (nextIndex <= 0)
        {
            property.stringValue = string.Empty;
            return;
        }

        property.stringValue = options[nextIndex].StartsWith(MissingPrefix)
            ? currentValue
            : options[nextIndex];
    }
}

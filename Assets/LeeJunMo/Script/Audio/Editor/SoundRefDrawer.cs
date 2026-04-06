#if UNITY_EDITOR
using System.Collections.Generic;
using CapstoneAudio;
using UnityEditor;
using UnityEngine;

namespace CapstoneAudio.EditorTools
{
    [CustomPropertyDrawer(typeof(SoundRef))]
    public sealed class SoundRefDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 56f;
        private const float ToolWidth = 52f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty keyProperty = property.FindPropertyRelative("key");
            SerializedProperty volumeMultiplierProperty = property.FindPropertyRelative("volumeMultiplier");
            SerializedProperty anchorPolicyProperty = property.FindPropertyRelative("anchorPolicy");
            SerializedProperty localOffsetProperty = property.FindPropertyRelative("localOffset");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect lineRect = new Rect(position.x, position.y, position.width, lineHeight);

            EditorGUI.BeginProperty(position, label, property);

            Rect foldoutRect = new Rect(lineRect.x, lineRect.y, 14f, lineHeight);
            Rect labelRect = new Rect(foldoutRect.xMax, lineRect.y, Mathf.Max(60f, lineRect.width - (ButtonWidth + ToolWidth + 6f)), lineHeight);
            Rect pickRect = new Rect(lineRect.xMax - (ButtonWidth + ToolWidth + 4f), lineRect.y, ButtonWidth, lineHeight);
            Rect toolRect = new Rect(lineRect.xMax - ToolWidth, lineRect.y, ToolWidth, lineHeight);

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);

            string headerText = string.IsNullOrWhiteSpace(keyProperty.stringValue)
                ? label.text
                : $"{label.text} ({keyProperty.stringValue})";
            EditorGUI.LabelField(labelRect, headerText);

            if (GUI.Button(pickRect, "Pick"))
                ShowKeyPicker(property.serializedObject, keyProperty.propertyPath, volumeMultiplierProperty.propertyPath);

            if (GUI.Button(toolRect, "Tool"))
                AudioCatalogWindow.OpenWindow(AudioCatalogEditorUtility.FindOwningCatalog(keyProperty.stringValue));

            lineRect.y += lineHeight + spacing;
            string editedKey = EditorGUI.TextField(lineRect, "Key", keyProperty.stringValue);
            keyProperty.stringValue = string.IsNullOrWhiteSpace(editedKey)
                ? string.Empty
                : editedKey.Trim().ToLowerInvariant();

            if (property.isExpanded)
            {
                lineRect.y += lineHeight + spacing;

                Rect leftRect = new Rect(lineRect.x, lineRect.y, lineRect.width * 0.45f, lineHeight);
                Rect rightRect = new Rect(leftRect.xMax + 6f, lineRect.y, lineRect.width - leftRect.width - 6f, lineHeight);
                EditorGUI.PropertyField(leftRect, volumeMultiplierProperty);
                EditorGUI.PropertyField(rightRect, anchorPolicyProperty);

                lineRect.y += lineHeight + spacing;
                EditorGUI.PropertyField(lineRect, localOffsetProperty);
            }

            if (!string.IsNullOrWhiteSpace(keyProperty.stringValue)
                && !AudioCatalogEditorUtility.KeyExists(keyProperty.stringValue))
            {
                lineRect.y += lineHeight + spacing;
                EditorGUI.HelpBox(lineRect, $"Catalog key '{keyProperty.stringValue}' was not found.", MessageType.Warning);
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float height = (lineHeight * 2f) + spacing;

            if (property.isExpanded)
                height += (lineHeight * 2f) + (spacing * 2f);

            SerializedProperty keyProperty = property.FindPropertyRelative("key");
            if (!string.IsNullOrWhiteSpace(keyProperty.stringValue)
                && !AudioCatalogEditorUtility.KeyExists(keyProperty.stringValue))
            {
                height += lineHeight + spacing;
            }

            return height;
        }

        private static void ShowKeyPicker(SerializedObject serializedObject, string keyPropertyPath, string volumeMultiplierPath)
        {
            GenericMenu menu = new GenericMenu();
            IReadOnlyList<string> keys = AudioCatalogEditorUtility.GetAllKeys();

            if (keys.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Audio Keys Found"));
                menu.AddSeparator(string.Empty);
                menu.AddItem(new GUIContent("Open Catalog Tool"), false, AudioCatalogWindow.OpenWindow);
                menu.ShowAsContext();
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                string selectedKey = keys[i];
                string menuPath = selectedKey.Replace('.', '/');
                menu.AddItem(
                    new GUIContent(menuPath),
                    false,
                    () => AssignKey(serializedObject, keyPropertyPath, volumeMultiplierPath, selectedKey));
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Open Catalog Tool"), false, AudioCatalogWindow.OpenWindow);
            menu.ShowAsContext();
        }

        private static void AssignKey(
            SerializedObject serializedObject,
            string keyPropertyPath,
            string volumeMultiplierPath,
            string selectedKey)
        {
            serializedObject.Update();

            SerializedProperty keyProperty = serializedObject.FindProperty(keyPropertyPath);
            SerializedProperty volumeMultiplierProperty = serializedObject.FindProperty(volumeMultiplierPath);
            if (keyProperty == null || volumeMultiplierProperty == null)
                return;

            keyProperty.stringValue = selectedKey;
            if (Mathf.Approximately(volumeMultiplierProperty.floatValue, 0f))
                volumeMultiplierProperty.floatValue = 1f;

            serializedObject.ApplyModifiedProperties();
        }

    }
}
#endif

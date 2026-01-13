using UnityEditor;
using UnityEngine;

namespace UnityGAS
{
    [CustomEditor(typeof(GameplayEffect), true)]
    public class GameplayEffectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Info", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("effectName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("duration"));
            if (serializedObject.FindProperty("duration").floatValue > 0)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("canStack"));
                if (serializedObject.FindProperty("canStack").boolValue)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("maxStacks"));
                }
            }

            // Draw properties specific to the derived classes (e.g., InstantModifierEffect)
            DrawPropertiesExcluding(serializedObject, "m_Script", "effectName", "icon", "description", "duration", "canStack", "maxStacks", "grantedTags");

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("grantedTags"), true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
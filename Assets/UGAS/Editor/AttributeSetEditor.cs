using UnityEditor;
using UnityEngine;
using System.Collections;
using System.Reflection;

namespace UnityGAS
{
    [CustomEditor(typeof(AttributeSet))]
    public class AttributeSetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (!Application.isPlaying) return;

            var attributeSet = (AttributeSet)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Attributes", EditorStyles.boldLabel);

            var attributes = (IDictionary)GetInstanceField(attributeSet, "attributes");

            if (attributes == null || attributes.Count == 0)
            {
                EditorGUILayout.LabelField("No attributes initialized.");
                return;
            }

            foreach (DictionaryEntry entry in attributes)
            {
                var definition = (AttributeDefinition)entry.Key;
                var value = (AttributeValue)entry.Value;

                float current = value.CurrentValue;
                float max = definition.maxValue;
                float percentage = (max > 0) ? current / max : 0;

                EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), percentage, $"{definition.name}: {current:F1} / {max:F1}");
            }

            Repaint();
        }

        private object GetInstanceField(object instance, string fieldName)
        {
            var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(instance);
        }
    }
}
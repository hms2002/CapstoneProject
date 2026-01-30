using UnityEditor;

namespace UnityGAS
{
    [CustomEditor(typeof(AttributeDefinition))]
    public class AttributeDefinitionEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // Draw the default inspector which is sufficient for this ScriptableObject.
            base.OnInspectorGUI();
        }
    }
}
using UnityEditor;
using UnityEngine;
using UnityGAS;

[CustomEditor(typeof(GameplayCue_HitSparkParticles))]
public sealed class GameplayCue_HitSparkParticlesEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Preview Burst"))
            {
                GameplayCue_HitSparkParticles spark = (GameplayCue_HitSparkParticles)target;
                spark.EditorPreviewBurst();
                EditorUtility.SetDirty(spark);
                SceneView.RepaintAll();
            }
        }
    }
}

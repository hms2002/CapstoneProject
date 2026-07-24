using UnityEditor;
using UnityEngine;
using UnityGAS;

[CustomEditor(typeof(GameplayCueDefinition))]
[CanEditMultipleObjects]
public class GameplayCueDefinitionEditor : Editor
{
    private SerializedProperty cueTag;
    private SerializedProperty mode;
    private SerializedProperty cueNotifyHostPrefab;
    private SerializedProperty cuePrefab;
    private SerializedProperty vfxPrefab;
    private SerializedProperty audioOnExecute;
    private SerializedProperty audioWhileActive;
    private SerializedProperty audioOnRemove;
    private SerializedProperty cameraShakeOnExecute;
    private SerializedProperty cameraShakeWhileActive;
    private SerializedProperty cameraShakeOnRemove;
    private SerializedProperty presentationOnExecute;
    private SerializedProperty presentationWhileActive;
    private SerializedProperty presentationOnRemove;
    private SerializedProperty attachToTarget;
    private SerializedProperty useExplicitHitPoint;
    private SerializedProperty spawnAnchorPolicy;
    private SerializedProperty applyOffsetInTargetLocalSpace;
    private SerializedProperty localOffset;
    private SerializedProperty autoDestroySeconds;
    private SerializedProperty isPersistent;
    private SerializedProperty uniquePerTarget;
    private SerializedProperty addLocalPosition;
    private SerializedProperty addLocalEuler;
    private SerializedProperty mulLocalScale;
    private SerializedProperty transformExecuteDuration;

    private void OnEnable()
    {
        cueTag = serializedObject.FindProperty("cueTag");
        mode = serializedObject.FindProperty("mode");
        cueNotifyHostPrefab = serializedObject.FindProperty("cueNotifyHostPrefab");
        cuePrefab = serializedObject.FindProperty("cuePrefab");
        vfxPrefab = serializedObject.FindProperty("vfxPrefab");
        audioOnExecute = serializedObject.FindProperty("audioOnExecute");
        audioWhileActive = serializedObject.FindProperty("audioWhileActive");
        audioOnRemove = serializedObject.FindProperty("audioOnRemove");
        cameraShakeOnExecute = serializedObject.FindProperty("cameraShakeOnExecute");
        cameraShakeWhileActive = serializedObject.FindProperty("cameraShakeWhileActive");
        cameraShakeOnRemove = serializedObject.FindProperty("cameraShakeOnRemove");
        presentationOnExecute = serializedObject.FindProperty("presentationOnExecute");
        presentationWhileActive = serializedObject.FindProperty("presentationWhileActive");
        presentationOnRemove = serializedObject.FindProperty("presentationOnRemove");
        attachToTarget = serializedObject.FindProperty("attachToTarget");
        useExplicitHitPoint = serializedObject.FindProperty("useExplicitHitPoint");
        spawnAnchorPolicy = serializedObject.FindProperty("spawnAnchorPolicy");
        applyOffsetInTargetLocalSpace = serializedObject.FindProperty("applyOffsetInTargetLocalSpace");
        localOffset = serializedObject.FindProperty("localOffset");
        autoDestroySeconds = serializedObject.FindProperty("autoDestroySeconds");
        isPersistent = serializedObject.FindProperty("isPersistent");
        uniquePerTarget = serializedObject.FindProperty("uniquePerTarget");
        addLocalPosition = serializedObject.FindProperty("addLocalPosition");
        addLocalEuler = serializedObject.FindProperty("addLocalEuler");
        mulLocalScale = serializedObject.FindProperty("mulLocalScale");
        transformExecuteDuration = serializedObject.FindProperty("transformExecuteDuration");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginDisabledGroup(true);
        MonoScript script = MonoScript.FromScriptableObject((GameplayCueDefinition)target);
        EditorGUILayout.ObjectField("Script", script, typeof(MonoScript), false);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Key", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cueTag);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Execution", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(mode);

        GameplayCueDefinition.ExecutionMode selectedMode =
            (GameplayCueDefinition.ExecutionMode)mode.enumValueIndex;

        switch (selectedMode)
        {
            case GameplayCueDefinition.ExecutionMode.TargetNotify:
                EditorGUILayout.PropertyField(cueNotifyHostPrefab);
                break;

            case GameplayCueDefinition.ExecutionMode.SpawnPrefab:
                EditorGUILayout.PropertyField(cuePrefab);
                EditorGUILayout.PropertyField(vfxPrefab);
                break;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Audio", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(audioOnExecute);
        EditorGUILayout.PropertyField(audioWhileActive);
        EditorGUILayout.PropertyField(audioOnRemove);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Camera Shake", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cameraShakeOnExecute);
        EditorGUILayout.PropertyField(cameraShakeWhileActive);
        EditorGUILayout.PropertyField(cameraShakeOnRemove);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spawned Presentation", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(presentationOnExecute, includeChildren: true);
        EditorGUILayout.PropertyField(presentationWhileActive, includeChildren: true);
        EditorGUILayout.PropertyField(presentationOnRemove, includeChildren: true);

        if (selectedMode == GameplayCueDefinition.ExecutionMode.SpawnPrefab)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Spawn Options", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(attachToTarget);
                EditorGUILayout.PropertyField(useExplicitHitPoint, new GUIContent("Use Hit Point"));

                if (!useExplicitHitPoint.boolValue)
                    EditorGUILayout.PropertyField(spawnAnchorPolicy);

                EditorGUILayout.PropertyField(applyOffsetInTargetLocalSpace);
                EditorGUILayout.PropertyField(localOffset);
                EditorGUILayout.PropertyField(autoDestroySeconds);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Persistence", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(isPersistent);
        EditorGUILayout.PropertyField(uniquePerTarget);

        if (selectedMode == GameplayCueDefinition.ExecutionMode.TransformOnly)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Transform Only", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(addLocalPosition);
            EditorGUILayout.PropertyField(addLocalEuler);
            EditorGUILayout.PropertyField(mulLocalScale);
            EditorGUILayout.PropertyField(transformExecuteDuration);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PixelLightTestScaleWaveReplacementTool
{
    private const string ScenePath = "Assets/Scenes/PixelLightTest.unity";
    private const string TargetObjectName = "BeatingSpotLight 2D";
    private const string MenuPath = "Tools/Rendering/Pixel Lighting/Replace BeatingSpotLight Scale Wave Graph";

    private const float GraphSpeed = 5f;
    private const float GraphAmplitude = 0.02f;

    [MenuItem(MenuPath)]
    public static void ReplaceBeatingSpotLightScaleWaveGraph()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Cannot replace BeatingSpotLight Scale Wave graph while in Play Mode.");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogError($"PixelLightTest scene was not found at {ScenePath}.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject target = FindSceneGameObject(scene, TargetObjectName);
            if (target == null)
            {
                Debug.LogError($"{TargetObjectName} was not found in {ScenePath}.");
                return;
            }

            int addedComponents = EnsureScaleWave(target);
            int removedComponents = RemoveVisualScriptingComponents(target);

            if (addedComponents > 0 || removedComponents > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log(
                $"BeatingSpotLight Scale Wave graph replaced. AddedScaleWave={addedComponents}, RemovedVisualScriptingComponents={removedComponents}.");
        }
        finally
        {
            if (originalSetup != null && originalSetup.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
        }
    }

    private static int EnsureScaleWave(GameObject target)
    {
        ScaleWave scaleWave = target.GetComponent<ScaleWave>();
        int addedComponents = 0;
        if (scaleWave == null)
        {
            scaleWave = target.AddComponent<ScaleWave>();
            addedComponents++;
        }

        scaleWave.Speed = GraphSpeed;
        scaleWave.Amplitude = GraphAmplitude;
        EditorUtility.SetDirty(scaleWave);

        return addedComponents;
    }

    private static int RemoveVisualScriptingComponents(GameObject target)
    {
        List<Component> componentsToRemove = new();
        Component[] components = target.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            Type type = component.GetType();
            string fullName = type.FullName;
            if (fullName == "Unity.VisualScripting.ScriptMachine" ||
                fullName == "Unity.VisualScripting.Variables")
            {
                componentsToRemove.Add(component);
            }
        }

        for (int i = 0; i < componentsToRemove.Count; i++)
            UnityEngine.Object.DestroyImmediate(componentsToRemove[i]);

        return componentsToRemove.Count;
    }

    private static GameObject FindSceneGameObject(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(includeInactive: true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].gameObject.name == objectName)
                    return transforms[j].gameObject;
            }
        }

        return null;
    }
}

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임:
/// - PixelLightTest 씬의 BeatingSpotLight Visual Scripting Scale Wave 그래프를 코드 기반 ScaleWave 컴포넌트로 교체한다.
/// - Visual Scripting 패키지가 제거된 상태에서도 씬에 남은 missing script 잔여 컴포넌트를 정리할 수 있게 한다.
/// </summary>
public static class PixelLightTestScaleWaveReplacementTool
{
    private const string ScenePath = "Assets/_Project/Scenes/PixelLightTest.unity";
    private const string TargetObjectName = "BeatingSpotLight 2D";
    private const string VisualScriptingSceneVariablesObjectName = "VisualScripting SceneVariables";
    private const string MenuPath = "Tools/Rendering/Pixel Lighting/Replace BeatingSpotLight Scale Wave Graph";

    private const float GraphSpeed = 5f;
    private const float GraphAmplitude = 0.02f;

    [MenuItem(MenuPath)]
    public static void ReplaceBeatingSpotLightScaleWaveGraph()
    {
        ReplaceBeatingSpotLightScaleWaveGraph(promptBeforeSceneSwitch: true);
    }

    public static bool ReplaceBeatingSpotLightScaleWaveGraphForAssemblySplitCleanup()
    {
        return ReplaceBeatingSpotLightScaleWaveGraph(promptBeforeSceneSwitch: false);
    }

    private static bool ReplaceBeatingSpotLightScaleWaveGraph(bool promptBeforeSceneSwitch)
    {
        if (Application.isPlaying)
        {
            Debug.LogError("Cannot replace BeatingSpotLight Scale Wave graph while in Play Mode.");
            return false;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            Debug.LogError($"PixelLightTest scene was not found at {ScenePath}.");
            return false;
        }

        if (promptBeforeSceneSwitch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject target = FindSceneGameObject(scene, TargetObjectName);
            if (target == null)
            {
                Debug.LogError($"{TargetObjectName} was not found in {ScenePath}.");
                return false;
            }

            int addedComponents = EnsureScaleWave(target);
            int removedComponents = RemoveVisualScriptingComponents(target);
            int removedMissingComponents = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
            int removedSceneVariableChanges = RemoveVisualScriptingSceneVariables(scene);

            if (addedComponents > 0 ||
                removedComponents > 0 ||
                removedMissingComponents > 0 ||
                removedSceneVariableChanges > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log(
                "BeatingSpotLight Scale Wave graph replaced. " +
                $"AddedScaleWave={addedComponents}, " +
                $"RemovedVisualScriptingComponents={removedComponents}, " +
                $"RemovedMissingComponents={removedMissingComponents}, " +
                $"RemovedSceneVariableChanges={removedSceneVariableChanges}.");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
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

    private static int RemoveVisualScriptingSceneVariables(Scene scene)
    {
        GameObject sceneVariables = FindSceneGameObject(scene, VisualScriptingSceneVariablesObjectName);
        if (sceneVariables == null)
            return 0;

        int changeCount = RemoveVisualScriptingComponents(sceneVariables);
        changeCount += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(sceneVariables);

        Component[] remainingComponents = sceneVariables.GetComponents<Component>();
        bool hasRuntimeComponent = false;
        for (int i = 0; i < remainingComponents.Length; i++)
        {
            Component component = remainingComponents[i];
            if (component != null && !(component is Transform))
            {
                hasRuntimeComponent = true;
                break;
            }
        }

        if (!hasRuntimeComponent && sceneVariables.transform.childCount == 0)
        {
            UnityEngine.Object.DestroyImmediate(sceneVariables);
            changeCount++;
        }

        return changeCount;
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


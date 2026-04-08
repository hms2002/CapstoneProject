using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SceneSmokePlayModeTests
{
    private const string HubSceneName = "ProtoTypeHub";
    private const string BossSceneName = "ProtoTypeBoss 1";
    private const int SettleFrameCount = 5;
    private const int ExpectedGlobalCanvasLayerCount = 8;

    [UnityTest]
    public IEnumerator ProtoTypeHub_Loads_CoreSceneObjectsExist()
    {
        yield return LoadScene(HubSceneName);

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(HubSceneName));
        AssertSceneObjectExists("BossCam");
        AssertComponentTypeExists("GlobalUIRoot");
        AssertComponentTypeExists("InputBindingService");
        AssertComponentTypeExists("UIManager");
        AssertComponentTypeExists("WorldInteractionPromptController");
    }

    [UnityTest]
    public IEnumerator ProtoTypeBoss1_Loads_EncounterFlowDependenciesExist()
    {
        yield return LoadScene(BossSceneName);

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(BossSceneName));
        AssertSceneObjectExists("BossCam");
        AssertComponentTypeExists("GlobalUIRoot");
        AssertComponentTypeExists("WorldInteractionPromptController");
        AssertComponentTypeExists("BossEncounterDirector");
        AssertComponentTypeExists("UIManager");
    }

    [UnityTest]
    public IEnumerator ProtoTypeHub_GlobalUiRoot_ContainsServicesRoot_AndCanvasLayers()
    {
        yield return LoadScene(HubSceneName);

        GameObject globalUiRoot = AssertComponentGameObjectExists("GlobalUIRoot");
        Transform servicesRoot = FindChildTransformByName(globalUiRoot.transform, "Services");

        Assert.That(servicesRoot, Is.Not.Null, "Expected GlobalUIRoot to contain a 'Services' child.");

        Canvas[] canvases = globalUiRoot.GetComponentsInChildren<Canvas>(true);
        Assert.That(
            canvases.Length,
            Is.GreaterThanOrEqualTo(ExpectedGlobalCanvasLayerCount),
            $"Expected GlobalUIRoot to expose at least {ExpectedGlobalCanvasLayerCount} canvases.");
    }

    [UnityTest]
    public IEnumerator ProtoTypeHub_Loads_PersistentRuntimeServicesExist()
    {
        yield return LoadScene(HubSceneName);

        AssertComponentTypeExists("InputBindingService");
        AssertComponentTypeExists("GameSettingsService");
        AssertComponentTypeExists("SoundManager");
        AssertComponentTypeExists("LootManager");
    }

    [UnityTest]
    public IEnumerator ProtoTypeBoss1_Loads_BossPresentationAndControllerComponentsExist()
    {
        yield return LoadScene(BossSceneName);

        AssertComponentTypeExists("CameraPresentationDirector");
        AssertComponentTypeExists("BossDialogueRunner");
        AssertComponentTypeExists("PlayerInteractor2D");
        AssertComponentTypeExists("Witch");
    }

    private static IEnumerator LoadScene(string sceneName)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        Assert.That(operation, Is.Not.Null, $"Could not start loading scene '{sceneName}'. Check Build Settings.");

        while (!operation.isDone)
            yield return null;

        for (int i = 0; i < SettleFrameCount; i++)
            yield return null;
    }

    private static GameObject AssertSceneObjectExists(string objectName)
    {
        GameObject sceneObject = FindSceneObjectIncludingInactive(objectName);
        Assert.That(sceneObject, Is.Not.Null, $"Expected scene object '{objectName}' to exist.");
        return sceneObject;
    }

    private static void AssertComponentTypeExists(string typeName)
    {
        MonoBehaviour behaviour = FindComponentByTypeName(typeName);
        if (behaviour != null)
            return;

        Assert.Fail($"Expected a component of type '{typeName}' to exist in the active scene.");
    }

    private static GameObject AssertComponentGameObjectExists(string typeName)
    {
        MonoBehaviour behaviour = FindComponentByTypeName(typeName);
        Assert.That(behaviour, Is.Not.Null, $"Expected a component of type '{typeName}' to exist in the active scene.");
        return behaviour.gameObject;
    }

    private static MonoBehaviour FindComponentByTypeName(string typeName)
    {
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            if (behaviour.GetType().Name == typeName)
                return behaviour;
        }

        return null;
    }

    private static GameObject FindSceneObjectIncludingInactive(string objectName)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return null;

        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform[] transforms = rootObjects[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                Transform current = transforms[j];
                if (current != null && current.name == objectName)
                    return current.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildTransformByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }
}

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
        AssertComponentTypeExists("CameraBootstrap");
        AssertComponentTypeExists("GlobalUIRoot");
        AssertComponentTypeExists("InputBindingService");
        AssertComponentTypeExists("UIManager");
        AssertComponentTypeExists("WorldInteractionPromptController");
        Assert.That(FindPersistentChildObject("CameraBootstrap", "PlayerCam"), Is.Not.Null, "Expected CameraBootstrap to own a persistent PlayerCam in the hub scene.");
    }

    [UnityTest]
    public IEnumerator ProtoTypeBoss1_Loads_EncounterFlowDependenciesExist()
    {
        yield return LoadScene(BossSceneName);

        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(BossSceneName));
        AssertSceneObjectExists("BossCam");
        AssertComponentTypeExists("CameraBootstrap");
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
        Assert.That(CountSceneObjectsByName("BossCam"), Is.EqualTo(1), "Boss scene should contain exactly one scene-local BossCam.");
    }

    [UnityTest]
    public IEnumerator CameraBootstrap_Preserves_RuntimePlayerCam_AcrossSceneTransitions()
    {
        yield return LoadScene(HubSceneName);

        MonoBehaviour bootstrapBefore = FindComponentByTypeName("CameraBootstrap");
        Assert.That(bootstrapBefore, Is.Not.Null, "Expected CameraBootstrap to exist after loading the hub scene.");

        GameObject playerCamBefore = FindPersistentChildObject("CameraBootstrap", "PlayerCam");
        Assert.That(playerCamBefore, Is.Not.Null, "Expected CameraBootstrap to own a persistent PlayerCam after loading the hub scene.");

        int bootstrapInstanceId = bootstrapBefore.gameObject.GetInstanceID();
        int playerCamInstanceId = playerCamBefore.GetInstanceID();

        yield return LoadScene(BossSceneName);

        MonoBehaviour bootstrapAfter = FindComponentByTypeName("CameraBootstrap");
        Assert.That(bootstrapAfter, Is.Not.Null, "Expected CameraBootstrap to still exist after loading the boss scene.");
        Assert.That(bootstrapAfter.gameObject.GetInstanceID(), Is.EqualTo(bootstrapInstanceId), "CameraBootstrap should persist across scene transitions.");

        GameObject playerCamAfter = FindPersistentChildObject("CameraBootstrap", "PlayerCam");
        Assert.That(playerCamAfter, Is.Not.Null, "Expected CameraBootstrap to continue owning the persistent PlayerCam after loading the boss scene.");
        Assert.That(playerCamAfter.GetInstanceID(), Is.EqualTo(playerCamInstanceId), "PlayerCam should persist instead of being recreated during the scene transition.");
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

    private static int CountSceneObjectsByName(string objectName)
    {
        int count = 0;
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return count;

        GameObject[] rootObjects = activeScene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            Transform[] transforms = rootObjects[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                Transform current = transforms[j];
                if (current != null && current.name == objectName)
                    count++;
            }
        }

        return count;
    }

    private static GameObject FindPersistentChildObject(string rootName, string childName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current == null || current.hideFlags != HideFlags.None)
                continue;

            if (!current.gameObject.scene.IsValid())
                continue;

            if (current.name != childName)
                continue;

            Transform parent = current.parent;
            while (parent != null)
            {
                if (parent.name == rootName)
                    return current.gameObject;

                parent = parent.parent;
            }
        }

        return null;
    }
}

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

#if UNITY_EDITOR
/// <summary>Exercises the real Ink/tag/controller callback path without loading or saving a gameplay scene.</summary>
public sealed class DialogueBlockingPresentationPlayModeTests
{
    private const System.Reflection.BindingFlags PrivateInstance =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
    private GameObject root;
    private Component controller;
    private Component handler;
    private object state;
    private System.Action pending;
    private int featureCalls;

    [SetUp]
    public void SetUp()
    {
        // Keep lifecycle/service registration and scene UI discovery out of this focused fixture.
        root = new GameObject("DialogueBlockingPresentationTest");
        root.SetActive(false);
        controller = root.AddComponent(RuntimeType("DialogueController"));
        handler = root.AddComponent(RuntimeType("DialogueTagHandler"));
        Component view = root.AddComponent(RuntimeType("DialogueView"));
        SetField("view", view);
        SetField("tagHandler", handler);
        state = Field("sessionState");
        Invoke(state, "BeginSession");
        Invoke(state, "EndTransition");
        handler.GetType().GetField("OnFeatureRequested").SetValue(handler,
            (System.Action<string, System.Action>)((_, callback) => { featureCalls++; pending = callback; }));
        handler.GetType().GetField("OnChoiceFailureRequested").SetValue(handler,
            (System.Action<System.Action>)(callback => pending = callback));

        // Bind the real NPCData-typed affection event without adding a test assembly reference to UI/Gameplay.
        var affectionField = handler.GetType().GetField("OnAffectionRequested");
        System.Type[] arguments = affectionField.FieldType.GetGenericArguments();
        var npc = System.Linq.Expressions.Expression.Parameter(arguments[0]);
        var amount = System.Linq.Expressions.Expression.Parameter(arguments[1]);
        var completion = System.Linq.Expressions.Expression.Parameter(arguments[2]);
        var capture = System.Linq.Expressions.Expression.Call(
            System.Linq.Expressions.Expression.Constant(this),
            GetType().GetMethod(nameof(CaptureCompletion), PrivateInstance), completion);
        affectionField.SetValue(handler, System.Linq.Expressions.Expression.Lambda(
            affectionField.FieldType, capture, npc, amount, completion).Compile());
    }

    [TearDown]
    public void TearDown()
    {
        if (state != null) Invoke(state, "EndSession");
        Object.DestroyImmediate(root);
    }

    [Test]
    public void CameraCueWaitsForFeatureThenShowsNextSpokenLineWithoutCueCard()
    {
        SetStory("[camera cue] # feature: scribe_cue # presentation_only\nNext spoken line.\n-> END");
        Continue();
        Assert.That(featureCalls, Is.EqualTo(1));
        Assert.That(CurrentText, Is.Empty);
        Assert.That(pending, Is.Not.Null);
        Assert.That((bool)state.GetType().GetProperty("IsWaitingForCallback").GetValue(state), Is.True);
        pending();
        Assert.That(CurrentText.Trim(), Is.EqualTo("Next spoken line."));
        Assert.That(CurrentText, Does.Not.Contain("[camera cue]"));
        // Changing locale after the callback must refresh the spoken line, not revive the sentinel.
        controller.GetType().GetMethod("HandleLocaleChanged", PrivateInstance).Invoke(controller, new object[] { null });
        Assert.That(CurrentText.Trim(), Is.EqualTo("Next spoken line."));
    }

    [TestCase("add_aff: 1")]
    [TestCase("choice_fail")]
    [TestCase("feature: scribe_cue")]
    public void BlockingEffectPreservesItsRealSpokenLineBeforeAdvancing(string tag)
    {
        SetStory("First spoken line. # " + tag + "\nFollowing line.\n-> END");
        Continue();
        Assert.That(CurrentText, Is.Empty);
        Assert.That(pending, Is.Not.Null);
        pending();
        Assert.That(CurrentText.Trim(), Is.EqualTo("First spoken line."));
        Invoke(state, "EndTyping");
        Continue();
        Assert.That(CurrentText.Trim(), Is.EqualTo("Following line."));
    }

    [Test]
    public void ExplicitPresentationOnlyLineWithoutBlockingFeatureAlsoSkipsItsText()
    {
        SetStory("hidden marker # presentation_only\nVisible line.\n-> END");
        Continue();
        Assert.That(CurrentText.Trim(), Is.EqualTo("Visible line."));
        Assert.That(featureCalls, Is.Zero);
    }

    [Test]
    public void CallbackAfterSessionEndsDoesNotDisplayOrAdvance()
    {
        SetStory("Hidden marker # feature: scribe_cue # presentation_only\nMust not appear.\n-> END");
        Continue();
        Invoke(state, "EndSession");
        pending();
        Assert.That(CurrentText, Is.Empty);
    }

    [TestCase("intro", 9, 4)]
    [TestCase("audience", 2, 2)]
    [TestCase("repeat", 1, 0)]
    public void AuthoredScribeKeepsEverySpokenLineAndCameraCue(string knot, int spokenCount, int cueCount)
    {
        string json = System.IO.File.ReadAllText(
            "Assets/_Project/Data/Dialogue/Ink/GrandHallScribe.json");
        object story = System.Activator.CreateInstance(RuntimeType("Ink.Runtime.Story"), json);
        story.GetType().GetMethod("ChoosePathString").Invoke(story, new object[] { knot, false, new object[0] });
        int spoken = 0, cues = 0;
        while ((bool)story.GetType().GetProperty("canContinue").GetValue(story))
        {
            string text = (string)Invoke(story, "Continue");
            var tags = (List<string>)story.GetType().GetProperty("currentTags").GetValue(story);
            if (tags.Contains("feature: scribe_cue"))
            {
                Assert.That(tags, Does.Contain("presentation_only"));
                Assert.That(text.Trim(), Is.EqualTo("[camera cue]"));
                cues++;
            }
            else if (!string.IsNullOrWhiteSpace(text))
            {
                Assert.That(tags, Does.Not.Contain("presentation_only"));
                spoken++;
            }
        }
        Assert.That(spoken, Is.EqualTo(spokenCount));
        Assert.That(cues, Is.EqualTo(cueCount));
    }

    private void CaptureCompletion(System.Action callback) => pending = callback;
    private string CurrentText => (string)state.GetType().GetProperty("CurrentText").GetValue(state);
    private object Field(string name) => controller.GetType().GetField(name, PrivateInstance).GetValue(controller);
    private void SetField(string name, object value) => controller.GetType().GetField(name, PrivateInstance).SetValue(controller, value);
    private void Continue() => controller.GetType().GetMethod("ContinueStory", PrivateInstance).Invoke(controller, null);

    private void SetStory(string source)
    {
        System.Type compilerType = RuntimeType("Ink.Compiler");
        object options = System.Activator.CreateInstance(compilerType.GetNestedType("Options"));
        object compiler = System.Activator.CreateInstance(compilerType, source, options);
        SetField("currentStory", Invoke(compiler, "Compile"));
    }

    private static object Invoke(object target, string method) => target.GetType().GetMethod(method).Invoke(target, null);
    private static System.Type RuntimeType(string name)
    {
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType(name);
            if (type != null) return type;
        }
        throw new System.InvalidOperationException("Missing runtime type: " + name);
    }
}
#endif

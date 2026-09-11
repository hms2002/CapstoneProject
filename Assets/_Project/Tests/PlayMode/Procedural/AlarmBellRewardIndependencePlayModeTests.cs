using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Responsibility: verify that unclaimed level rewards never gate the bell or its
/// encounter completion, while restoring the shared run data after each test.
/// Also verify the authored completion chest and its normal open/save lifecycle.
/// </summary>
public sealed class AlarmBellRewardIndependencePlayModeTests
{
    private GamePlayData data;
    private bool originalRunActive;
    private LevelProgressionState originalProgression;
    private List<string> originalCompletedEvents;
    private GameObject bellObject;
    private AlarmBellInteractable bell;
    private AlarmBellEncounterDefinitionSO definition;
    private LevelProgressionConfigSO progressionConfig;
    private TestPlayerInteractor player;

    [SetUp]
    public void SetUp()
    {
        data = GamePlayDataManager.EnsureInstance().Data;
        originalRunActive = data.isRunActive;
        originalProgression = data.levelProgression;
        originalCompletedEvents = data.completedRunMapEventIds;
        data.isRunActive = true;
        data.completedRunMapEventIds = new List<string>();
        data.levelProgression = new LevelProgressionState
        {
            pendingRewardCount = 2,
            activeRewardOffer = new LevelRewardOfferState { isActive = true }
        };

        definition = ScriptableObject.CreateInstance<AlarmBellEncounterDefinitionSO>();
        progressionConfig = ScriptableObject.CreateInstance<LevelProgressionConfigSO>();
        SetField(definition, "eventId", "alarm_bell_reward_independence_test");
        SetField(definition, "activationDelaySeconds", 0f);
        SetField(definition, "nextWaveDelaySeconds", 0f);
        SetField(definition, "levelProgressionConfig", progressionConfig);
        bellObject = new GameObject("AlarmBellRewardIndependenceTest", typeof(CircleCollider2D));
        bell = bellObject.AddComponent<AlarmBellInteractable>();
        SetField(bell, "definition", definition);
        SetField(bell, "activatedPopupMessage", string.Empty);
        SetField(bell, "clearedPopupMessage", string.Empty);
        player = new TestPlayerInteractor(bellObject.transform);
    }

    [TearDown]
    public void TearDown()
    {
        if (bellObject != null)
            UnityEngine.Object.DestroyImmediate(bellObject);
        if (definition != null)
            UnityEngine.Object.DestroyImmediate(definition);
        if (progressionConfig != null)
            UnityEngine.Object.DestroyImmediate(progressionConfig);
        if (data != null)
        {
            data.isRunActive = originalRunActive;
            data.levelProgression = originalProgression;
            data.completedRunMapEventIds = originalCompletedEvents;
        }
    }

    [TestCase(0, false)]
    [TestCase(2, false)]
    [TestCase(0, true)]
    [TestCase(2, true)]
    public void CanInteract_DoesNotDependOnUnclaimedRewards(int pendingCount, bool savedOffer)
    {
        data.levelProgression.pendingRewardCount = pendingCount;
        data.levelProgression.activeRewardOffer.isActive = savedOffer;
        Assert.That(bell.CanInteract(player), Is.True);
    }

    [Test]
    public void CanInteract_PreservesExistingRunAndPlayerGates()
    {
        Assert.That(bell.CanInteract(null), Is.False);
        player.SetInteractState(InteractState.Shopping);
        Assert.That(bell.CanInteract(player), Is.False);
        player.SetInteractState(InteractState.Idle);
        data.isRunActive = false;
        Assert.That(bell.CanInteract(player), Is.False);
        data.isRunActive = true;
        data.completedRunMapEventIds.Add(definition.EventId);
        Assert.That(bell.CanInteract(player), Is.False);
        data.completedRunMapEventIds.Clear();
        SetField(bell, "definition", null);
        Assert.That(bell.CanInteract(player), Is.False);
    }

    [Test]
    public void ClearedWaves_ReleaseEncounterAndGrantExperienceWithoutRewardSelection()
    {
        TreasureChest completionChest = AddCompletionChest();
        Assert.That(completionChest.gameObject.activeSelf, Is.False);
        // Empty waves isolate already-cleared wave advancement from monster authoring/spawning.
        var tier = new AlarmBellEncounterTier();
        SetField(tier, "waves", new List<AlarmBellWaveDefinition>
        {
            new AlarmBellWaveDefinition(), new AlarmBellWaveDefinition()
        });
        SetField(tier, "completionExperience", 7);
        SetField(definition, "tiers", new List<AlarmBellEncounterTier> { tier });
        MonsterSpawnRoomGroup group = bellObject.AddComponent<MonsterSpawnRoomGroup>();
        SetField(bell, "roomGroup", group);

        MethodInfo method = typeof(AlarmBellInteractable).GetMethod(
            "RunEncounter", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null);
        var routine = (IEnumerator)method.Invoke(bell, new object[] { player });
        var stack = new Stack<IEnumerator>();
        stack.Push(routine);
        int clearedWaveWaits = 0;
        try
        {
            for (int steps = 0; stack.Count > 0 && steps < 20; steps++)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    (stack.Pop() as IDisposable)?.Dispose();
                    continue;
                }

                Assert.That(current.Current, Is.InstanceOf<IEnumerator>(),
                    "No reward/UI wait should be yielded by these zero-delay, cleared waves.");
                Assert.That(group.EncounterHoldCount, Is.EqualTo(1));
                stack.Push((IEnumerator)current.Current);
                clearedWaveWaits++;
            }

            Assert.That(stack, Is.Empty, "The encounter must not wait for unclaimed rewards.");
            Assert.That(clearedWaveWaits, Is.EqualTo(2));
            Assert.That(group.EncounterHoldCount, Is.Zero);
            Assert.That(RunMapEventProgress.IsEventCompleted(data, definition.EventId), Is.True);
            Assert.That(data.levelProgression.currentExperience, Is.EqualTo(7));
            Assert.That(data.levelProgression.pendingRewardCount, Is.EqualTo(2));
            Assert.That(data.levelProgression.activeRewardOffer.isActive, Is.True);
            Assert.That(bell.CanInteract(player), Is.False);
            Assert.That(completionChest.gameObject.activeInHierarchy, Is.True);
            Assert.That(bellObject.transform.Find("BellVisual").gameObject.activeSelf, Is.False);
            Assert.That(bellObject.GetComponent<Collider2D>().enabled, Is.False);
            Assert.That(completionChest.GetComponent<Collider2D>().enabled, Is.True);
            Assert.That(completionChest.GetComponent<ChestInteractable>().CanInteract(player), Is.True);
        }
        finally
        {
            while (stack.Count > 0)
                (stack.Pop() as IDisposable)?.Dispose();
        }
    }

    [Test]
    public void CompletedEvent_RevealsAuthoredChestWithoutResettingRestoredLoot()
    {
        TreasureChest chest = AddCompletionChest();
        data.completedRunMapEventIds.Add(definition.EventId);
        bellObject.SetActive(false);
        bellObject.SetActive(true);
        Assert.That(chest.gameObject.activeInHierarchy, Is.True);
        chest.RestoreOpenedStateForDungeon();
        bellObject.SetActive(false);
        bellObject.SetActive(true);
        Assert.That(chest.IsOpened, Is.True);
        Assert.That(chest.CaptureDungeonLootState(), Is.Empty);
        Assert.That(bellObject.GetComponentsInChildren<TreasureChest>(true).Length, Is.EqualTo(1));
    }

#if UNITY_EDITOR
    [UnityTest]
    public IEnumerator AuthoredModule_ConvertsToInteractableChestAndUsesNormalUiRequest()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/Map/Procedural/Events/AlarmBellEventModule.prefab");
        Assert.That(prefab, Is.Not.Null);
        var oldUiBackend = (IChestUiOpenBackend)typeof(ChestUiOpenPlayback)
            .GetField("backend", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        GameObject instance = null;
        try
        {
            var uiBackend = new RecordingChestUiBackend();
            ChestUiOpenPlayback.RegisterBackend(uiBackend);
            instance = UnityEngine.Object.Instantiate(prefab);
            var module = instance.GetComponent<AlarmBellInteractable>();
            var chest = instance.GetComponentInChildren<TreasureChest>(true);
            Assert.That(chest, Is.Not.Null);
            Assert.That(chest.gameObject.activeSelf, Is.False);
            Assert.That(chest.GetComponent<ChestMonsterKillLock>(), Is.Null);
            Assert.That(typeof(AlarmBellInteractable).GetField("bellAnimator",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(module), Is.Null,
                "The bell must not auto-bind the reward chest's Animator.");

            SetField(module, "definition", definition);
            data.completedRunMapEventIds.Add(definition.EventId);
            instance.SetActive(false);
            instance.SetActive(true);
            Assert.That(chest.gameObject.activeInHierarchy, Is.True);
            Assert.That(instance.GetComponent<Collider2D>().enabled, Is.False);
            var interactable = chest.GetComponent<ChestInteractable>();
            Assert.That(interactable.CanInteract(player), Is.True);
            chest.InitializeWithLoot(new List<ScriptableObject>());
            SetField(chest, "freezeTimeOnFirstOpen", false);
            interactable.OnPlayerInteract(player);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (!chest.IsOpened && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(chest.IsOpened, Is.True);
            Assert.That(uiBackend.OpenCount, Is.EqualTo(1));
            Assert.That(player.CurrentState, Is.EqualTo(InteractState.Shopping));
        }
        finally
        {
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            ChestUiOpenPlayback.RegisterBackend(oldUiBackend);
        }
    }
#endif

    private TreasureChest AddCompletionChest()
    {
        var visual = new GameObject("BellVisual");
        visual.transform.SetParent(bellObject.transform, false);
        var reward = new GameObject("CompletionChest");
        reward.SetActive(false);
        reward.transform.SetParent(bellObject.transform, false);
        reward.AddComponent<CircleCollider2D>().isTrigger = true;
        var chest = reward.AddComponent<TreasureChest>();
        reward.AddComponent<ChestInteractable>();
        SetField(bell, "bellVisualRoot", visual);
        SetField(bell, "completionChest", chest);
        return chest;
    }

    // Responsibility: observe the real chest's UI handoff without opening global UI during a test.
    private sealed class RecordingChestUiBackend : IChestUiOpenBackend
    {
        public int OpenCount;
        public bool OpenChest(TreasureChest chest, bool playSlideFadePresentation, GameFlowInputBlocker inputBlocker)
        {
            OpenCount++;
            return true;
        }
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }
}

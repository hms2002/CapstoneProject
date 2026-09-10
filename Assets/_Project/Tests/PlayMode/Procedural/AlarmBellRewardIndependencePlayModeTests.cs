using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Responsibility: verify that unclaimed level rewards never gate the bell or its
/// encounter completion, while restoring the shared run data after each test.
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
        }
        finally
        {
            while (stack.Count > 0)
                (stack.Pop() as IDisposable)?.Dispose();
        }
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }
}

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityGAS;
using UnityGAS.Sample;
using Object = UnityEngine.Object;

/// <summary>Checks alcohol charge, target ownership and real GE/modifier lifetimes.</summary>
public sealed class AlcoholExposurePlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> owned = new();
    private PlayerAlcoholExposure exposure;
    private GameplayEffectRunner runner;
    private AttributeSet attributes;
    private AlcoholPuddleArea sourceA;
    private AlcoholPuddleArea sourceB;
    private CombatBuffDebuffApplicationDefinition buff;
    private CombatBuffDebuffApplicationDefinition confusion;
    private AttributeDefinition move;
    private AttributeDefinition attack;
    private AffectionProgressStore affinity;
    private int previousAffinity;
    private IGameDataStoreBackend previousProfileBackend;
    private IRunSessionStoreBackend previousRunBackend;
    private TestProfileBackend profile;

    [SetUp]
    public void Setup()
    {
        previousProfileBackend = (IGameDataStoreBackend)typeof(GameDataStore)
            .GetField("backend", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        previousRunBackend = (IRunSessionStoreBackend)typeof(RunSessionStore)
            .GetField("backend", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        profile = new TestProfileBackend();
        GameDataStore.RegisterBackend(profile);
        if (previousRunBackend != null)
            RunSessionStore.UnregisterBackend(previousRunBackend);

        AffectionManager manager = AffectionManager.Instance;
        if (manager == null)
            manager = Own(new GameObject("Alcohol affinity fixture")).AddComponent<AffectionManager>();
        affinity = (AffectionProgressStore)typeof(AffectionManager).GetField("progressStore", Private).GetValue(manager);
        previousAffinity = affinity.GetAffection(2001);
        SetAffinity(0);

        if (Object.FindAnyObjectByType<PuddleManager>() == null)
            Own(new GameObject("Alcohol puddle fixture")).AddComponent<PuddleManager>();

        buff = CloneDefinition("CBD_Puddle_AlcoholBuff.asset");
        confusion = CloneDefinition("CBD_Puddle_Confusion.asset");
        move = AssetDatabase.LoadAssetAtPath<AttributeDefinition>(
            "Assets/_Project/Data/Attributes/Definitions/MoveSpeedMulAttribute.asset");
        attack = AssetDatabase.LoadAssetAtPath<AttributeDefinition>(
            "Assets/_Project/Data/Attributes/Definitions/DamageCalcAttribute/AttackSpeedAddAttribute.asset");
        Assert.That(move, Is.Not.Null);
        Assert.That(attack, Is.Not.Null);

        var initial = Own(ScriptableObject.CreateInstance<AttributeInitProfileSO>());
        SetField(initial, "entries", new[]
        {
            new AttributeInitProfileSO.Entry { attribute = move, baseValue = 1f },
            new AttributeInitProfileSO.Entry { attribute = attack, baseValue = 0f }
        });
        GameObject target = Own(new GameObject("Alcohol exposure recipient"));
        target.SetActive(false);
        attributes = target.AddComponent<AttributeSet>();
        SetField(attributes, "baseInitProfile", initial);
        target.AddComponent<TagSystem>();
        runner = target.AddComponent<GameplayEffectRunner>();
        target.AddComponent<AbilitySystem>();
        var status = target.AddComponent<PlayerStatusRuntime>();
        SetField(status, "logStatusLifecycle", false);
        var applier = target.AddComponent<CombatBuffDebuffApplier>();
        SetField(applier, "logApplyFlow", false);
        exposure = target.AddComponent<PlayerAlcoholExposure>();
        SetField(exposure, "alcoholBuffDefinition", buff);
        SetField(exposure, "confusionDefinition", confusion);
        target.SetActive(true);
        sourceA = Own(new GameObject("Alcohol source A")).AddComponent<AlcoholPuddleArea>();
        sourceB = Own(new GameObject("Alcohol source B")).AddComponent<AlcoholPuddleArea>();
    }

    [TearDown]
    public void Cleanup()
    {
        if (affinity != null)
            affinity.SetAffection(null, 2001, previousAffinity, syncToGameData: false);
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null)
                Object.DestroyImmediate(owned[i]);
        owned.Clear();
        GameDataStore.UnregisterBackend(profile);
        if (previousProfileBackend != null)
            GameDataStore.RegisterBackend(previousProfileBackend);
        if (previousRunBackend != null)
            RunSessionStore.RegisterBackend(previousRunBackend);
    }

    [TestCase(0, 2f)]
    [TestCase(1, 1f)]
    public void ChargeFillsAtRequiredTimeButEffectStartsOnlyAfterOverflow(int affection, float chargeSeconds)
    {
        SetAffinity(affection);
        exposure.EnterPuddle(sourceA);
        Tick(chargeSeconds);
        Assert.That(exposure.FillAmount, Is.EqualTo(1f));
        Assert.That(exposure.IsOverflowing, Is.False);
        Assert.That(runner.ActiveEffects, Is.Empty);
        Tick(0f);
        Assert.That(runner.ActiveEffects, Is.Empty, "Paused time does not overflow.");
        Tick(0.01f);
        Assert.That(exposure.IsOverflowing, Is.True);
        Assert.That(Active(affection > 0 ? buff : confusion), Is.Not.Null);
        Assert.That(Active(affection > 0 ? confusion : buff), Is.Null);
    }

    [Test]
    public void LeavingBeforeOverflowDrainsStartingFillForOneAndAHalfSeconds()
    {
        exposure.EnterPuddle(sourceA);
        Tick(1f);
        exposure.ExitPuddle(sourceA);
        Tick(0.75f);
        Assert.That(exposure.FillAmount, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(exposure.IsDraining, Is.True);
        Tick(0.75f);
        AssertEmptyGlass();
        Assert.That(runner.ActiveEffects, Is.Empty);
    }

    [Test]
    public void ReentryResumesRemainingFillInsteadOfRestartingCharge()
    {
        exposure.EnterPuddle(sourceA);
        Tick(1f);
        exposure.ExitPuddle(sourceA);
        Tick(0.75f);
        exposure.EnterPuddle(sourceB);
        Assert.That(exposure.IsDraining, Is.False);
        Assert.That(exposure.FillAmount, Is.EqualTo(0.25f).Within(0.0001f));
        Tick(1.5f);
        Assert.That(exposure.IsOverflowing, Is.False);
        Tick(0.01f);
        Assert.That(exposure.IsOverflowing, Is.True);
    }

    [Test]
    public void OverlappingAndRepeatedSourcesShareOneChargeAndEffect()
    {
        exposure.EnterPuddle(sourceA);
        exposure.EnterPuddle(sourceA);
        exposure.EnterPuddle(sourceB);
        Tick(1f);
        exposure.ExitPuddle(sourceA);
        Tick(1f);
        Assert.That(exposure.HasAlcoholContact, Is.True);
        Assert.That(exposure.IsDraining, Is.False);
        Assert.That(exposure.IsOverflowing, Is.False);
        Tick(0.01f);
        Assert.That(runner.ActiveEffects.Count, Is.EqualTo(1));
        Assert.That(Active(confusion).SourceObject, Is.SameAs(exposure));
        exposure.ExitPuddle(sourceB);
        Assert.That(exposure.IsDraining, Is.True);
    }

    [Test]
    public void DisabledAndDestroyedSourcesBeginDrainWithoutTriggerExit()
    {
        exposure.EnterPuddle(sourceA);
        exposure.EnterPuddle(sourceB);
        Tick(1.5f);
        sourceA.enabled = false;
        Tick(0f);
        Assert.That(exposure.HasAlcoholContact, Is.True);
        Object.DestroyImmediate(sourceB.gameObject);
        Tick(0f);
        Assert.That(exposure.HasAlcoholContact, Is.False);
        Assert.That(exposure.IsDraining, Is.True);
        Tick(1.5f);
        AssertEmptyGlass();
    }

    [Test]
    public void ConfusionPersistsDuringDrainAndClearsWhenGlassEmpties()
    {
        Overflow();
        exposure.ExitPuddle(sourceA);
        Tick(0.75f);
        Assert.That(Active(confusion), Is.Not.Null);
        Assert.That(exposure.IsOverflowing, Is.True);
        AssertHudDuration(false);
        Tick(0.75f);
        AssertEmptyGlass();
        Assert.That(Active(confusion), Is.Null);
        Assert.That(runner.ActiveEffects, Is.Empty);
    }

    [Test]
    public void EarnedBuffKeepsActualModifiersAndStartsFiveSecondsAfterDrain()
    {
        SetAffinity(1);
        Overflow();
        AssertHudDuration(true);
        for (int i = 0; i < 60; i++)
        {
            Advance(0.2f);
            AssertSpeedBuff();
        }
        exposure.ExitPuddle(sourceA);
        for (int i = 0; i < 7; i++)
            Advance(0.2f);
        Advance(0.1f);
        AssertEmptyGlass();
        Assert.That(Active(buff).TimeRemaining, Is.EqualTo(5f).Within(0.0001f));
        AssertSpeedBuff();
        Advance(4.99f);
        Assert.That(Active(buff).TimeRemaining, Is.EqualTo(0.01f).Within(0.0001f));
        AssertSpeedBuff();
        Advance(0.02f);
        Assert.That(Active(buff), Is.Null);
        Assert.That(attributes.GetCurrentValue(move), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(attributes.GetCurrentValue(attack), Is.EqualTo(0f).Within(0.0001f));
    }

    [Test]
    public void ReenteringWithResidualBuffRefreshesItBeforeNewOverflow()
    {
        SetAffinity(1);
        Overflow();
        exposure.ExitPuddle(sourceA);
        Advance(1.5f);
        Advance(2f);
        Assert.That(Active(buff).TimeRemaining, Is.EqualTo(3f).Within(0.0001f));
        exposure.EnterPuddle(sourceB);
        Assert.That(Active(buff).TimeRemaining, Is.EqualTo(5f).Within(0.0001f));
        Advance(0.2f);
        Assert.That(exposure.IsOverflowing, Is.False);
        AssertSpeedBuff();
        Assert.That(Active(buff).TimeRemaining, Is.EqualTo(5f).Within(0.0001f));
        exposure.ExitPuddle(sourceB);
        Advance(1.5f);
        Assert.That(Active(buff).TimeRemaining, Is.EqualTo(5f).Within(0.0001f));
    }

    [Test]
    public void LatestAffinitySwitchesOverflowEffectWithoutStacking()
    {
        Overflow();
        SetAffinity(1);
        Tick(0.2f);
        Assert.That(Active(confusion), Is.Null);
        Assert.That(Active(buff), Is.Not.Null);
        AssertSpeedBuff();
        SetAffinity(0);
        Tick(0.2f);
        Assert.That(Active(buff), Is.Null);
        Assert.That(Active(confusion), Is.Not.Null);
        Assert.That(runner.ActiveEffects.Count, Is.EqualTo(1));
    }

    [Test]
    public void SavedAffinityIsAvailableBeforeManagerCacheLoads()
    {
        profile.Data.affectionData.affectionRecords.Add(new AffectionRecord(2001, 1));
        Assert.That(affinity.GetAffection(2001), Is.Zero);
        Overflow();
        Assert.That(Active(buff), Is.Not.Null);
        Assert.That(Active(confusion), Is.Null);
    }

    [Test]
    public void CleanseIsReappliedOnlyWhileExposureStillMaintainsTheEffect()
    {
        SetAffinity(1);
        Overflow();
        runner.ClearAllActiveEffects();
        Tick(0.2f);
        Assert.That(Active(buff), Is.Not.Null);
        exposure.ExitPuddle(sourceA);
        Tick(1.5f);
        runner.ClearAllActiveEffects();
        Tick(0.2f);
        Assert.That(runner.ActiveEffects, Is.Empty);
        Assert.That(attributes.GetCurrentValue(move), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void RecipientDisableRemovesOwnedEffectsAndResetsAllExposureState()
    {
        Overflow();
        exposure.enabled = false;
        AssertEmptyGlass();
        Assert.That(exposure.HasAlcoholContact, Is.False);
        Assert.That(runner.ActiveEffects, Is.Empty);
        exposure.enabled = true;
        exposure.EnterPuddle(sourceA);
        Tick(1f);
        Assert.That(exposure.FillAmount, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(runner.ActiveEffects, Is.Empty);
    }

    [Test]
    public void SceneCaptureOmitsOwnedConfusionAndTagButPreservesUnrelatedState()
    {
        Overflow();
        var unrelatedEffect = Own(ScriptableObject.CreateInstance<GE_StatusOnlyDuration>());
        unrelatedEffect.name = "AlcoholCapture_UnrelatedEffect";
        unrelatedEffect.duration = 8f;
        runner.ApplyEffect(unrelatedEffect, exposure.gameObject, exposure.gameObject);
        GameplayTag unrelatedTag = Resources.Load<GameplayTag>("Tags/State.Status.Shielded");
        GameplayTag confusionTag = Resources.Load<GameplayTag>("Tags/State.Confusion");
        Assert.That(unrelatedTag, Is.Not.Null);
        Assert.That(confusionTag, Is.Not.Null);
        exposure.GetComponent<TagSystem>().AddTag(unrelatedTag, 2);

        PlayerRuntimeState state = CaptureSceneState();
        Assert.That(state.activeEffects.Exists(entry => entry.effectId == confusion.GameplayEffect.name), Is.False);
        Assert.That(state.explicitTags.Exists(entry => entry.tagName == confusionTag.Name || entry.tagName == confusionTag.Path), Is.False);
        Assert.That(state.activeEffects, Has.Count.EqualTo(1));
        Assert.That(state.activeEffects[0].effectId, Is.EqualTo(unrelatedEffect.name));
        Assert.That(state.activeEffects[0].remainingTime, Is.EqualTo(8f));
        ExplicitTagSnapshot independent = state.explicitTags.Find(entry => entry.tagName == unrelatedTag.Name);
        Assert.That(independent, Is.Not.Null);
        Assert.That(independent.count, Is.EqualTo(2));
        Assert.That(Active(confusion), Is.Not.Null, "Capture does not alter the current scene's effect.");
    }

    [Test]
    public void SceneCaptureOmitsResidualAlcoholBuffWithoutChangingCurrentHudTimer()
    {
        SetAffinity(1);
        Overflow();
        exposure.ExitPuddle(sourceA);
        Advance(1.5f);
        AssertEmptyGlass();
        PlayerRuntimeState state = CaptureSceneState();
        Assert.That(state.activeEffects, Is.Empty);
        Assert.That(Active(buff).TimeRemaining, Is.EqualTo(5f));
        AssertSpeedBuff();
        AssertHudDuration(true);
        Assert.That(state.attributes.Find(entry => entry.attributeId == move.name).baseValue, Is.EqualTo(1f));
        Assert.That(state.attributes.Find(entry => entry.attributeId == attack.name).baseValue, Is.Zero);
    }

    [TestCase(1f, 0f)]
    [TestCase(-1f, 0f)]
    [TestCase(0f, 1f)]
    [TestCase(0f, -1f)]
    [TestCase(1f, 1f)]
    public void ConfusionReversesWalkingAndDashButPreservesRawAndAbilityInput(float x, float y)
    {
        Overflow();
        GameplayTag confusionTag = Resources.Load<GameplayTag>("Tags/State.Confusion");
        GameplayTag blockedTag = Resources.Load<GameplayTag>("Tags/State.Move.Intent.Blocked");
        Assert.That(confusionTag, Is.Not.Null);
        Assert.That(blockedTag, Is.Not.Null);
        var tags = exposure.GetComponent<TagSystem>();
        Assert.That(tags.HasTag(confusionTag), Is.True, "Confusion GE grants its gameplay tag.");
        var input = exposure.gameObject.AddComponent<PlayerIntentInput2D>();
        SetField(input, "confusionTag", confusionTag);
        Vector2 expected = new Vector2(x, y).normalized;
        var backend = new TestInputBackend { BackendComponent = exposure, Move = expected };
        var backendField = typeof(InputActionQuery).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = (IInputActionQueryBackend)backendField.GetValue(null);
        InputActionQuery.RegisterBackend(backend);
        try
        {
            TickInput(input);
            Assert.That(input.MoveInput, Is.EqualTo(-expected));
            Assert.That(input.RawMoveInput, Is.EqualTo(expected));
            Assert.That(input.AbilityMoveInput, Is.EqualTo(expected));
            var dash = Own(ScriptableObject.CreateInstance<AbilityLogic_Dash2D>());
            Assert.That(ResolveDash(dash, false), Is.EqualTo(-expected));

            tags.AddTag(blockedTag);
            TickInput(input);
            Assert.That(input.MoveInput, Is.EqualTo(Vector2.zero), "Movement blocking still wins over confusion.");
            Assert.That(input.RawMoveInput, Is.EqualTo(expected));
            tags.RemoveTag(blockedTag);
            runner.RemoveEffect(confusion.GameplayEffect, exposure.gameObject);
            TickInput(input);
            Assert.That(input.MoveInput, Is.EqualTo(expected), "Clearing the GE restores ordinary walking.");
            Assert.That(ResolveDash(dash, false), Is.EqualTo(expected), "Clearing the GE also restores directional dash.");
        }
        finally { InputActionQuery.RegisterBackend(previous); }
    }

    [TestCase(1f, 0f)]
    [TestCase(-1f, 0f)]
    [TestCase(0f, 1f)]
    [TestCase(0f, -1f)]
    [TestCase(1f, 1f)]
    public void ConfusionReversesDashDirectInputBeforeIntentHasUpdated(float x, float y)
    {
        Overflow();
        var input = exposure.gameObject.AddComponent<PlayerIntentInput2D>();
        SetField(input, "confusionTag", Resources.Load<GameplayTag>("Tags/State.Confusion"));
        var backend = new TestInputBackend { BackendComponent = exposure };
        var backendField = typeof(InputActionQuery).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = (IInputActionQueryBackend)backendField.GetValue(null);
        InputActionQuery.RegisterBackend(backend);
        try
        {
            TickInput(input);
            Assert.That(input.RawMoveInput, Is.EqualTo(Vector2.zero));
            Vector2 expected = new Vector2(x, y).normalized;
            backend.Move = expected;
            var dash = Own(ScriptableObject.CreateInstance<AbilityLogic_Dash2D>());
            Assert.That(ResolveDash(dash, false), Is.EqualTo(-expected));
            Assert.That(input.RawMoveInput, Is.EqualTo(Vector2.zero), "Dash must not overwrite cached raw intent.");
            Assert.That(input.AbilityMoveInput, Is.EqualTo(expected));
            runner.RemoveEffect(confusion.GameplayEffect, exposure.gameObject);
            Assert.That(ResolveDash(dash, false), Is.EqualTo(expected));
        }
        finally { InputActionQuery.RegisterBackend(previous); }
    }

    [TestCase(1f, 0f)]
    [TestCase(-1f, 0f)]
    [TestCase(0f, 1f)]
    [TestCase(0f, -1f)]
    [TestCase(1f, 1f)]
    public void ConfusionLeavesAimFallbackDashUnchangedWithoutDirectionalInput(float x, float y)
    {
        Overflow();
        var aim = exposure.gameObject.AddComponent<PlayerAim2D>();
        var input = exposure.gameObject.AddComponent<PlayerIntentInput2D>();
        SetField(input, "confusionTag", Resources.Load<GameplayTag>("Tags/State.Confusion"));
        Vector2 expected = new Vector2(x, y).normalized;
        Assert.That(aim.SetAimDirectionForPresentation(expected), Is.True);
        var backend = new TestInputBackend { BackendComponent = exposure };
        var backendField = typeof(InputActionQuery).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = (IInputActionQueryBackend)backendField.GetValue(null);
        InputActionQuery.RegisterBackend(backend);
        try
        {
            TickInput(input);
            var dash = Own(ScriptableObject.CreateInstance<AbilityLogic_Dash2D>());
            Assert.That(ResolveDash(dash, true), Is.EqualTo(expected));
            Assert.That(input.RawMoveInput, Is.EqualTo(Vector2.zero));
            Assert.That(input.AbilityMoveInput, Is.EqualTo(Vector2.zero));
            Assert.That(aim.AimDirection, Is.EqualTo(expected));
            runner.RemoveEffect(confusion.GameplayEffect, exposure.gameObject);
            Assert.That(ResolveDash(dash, true), Is.EqualTo(expected));
        }
        finally { InputActionQuery.RegisterBackend(previous); }
    }

    private void Overflow()
    {
        exposure.EnterPuddle(sourceA);
        Tick(2.01f);
    }

    private void AssertEmptyGlass()
    {
        Assert.That(exposure.FillAmount, Is.Zero);
        Assert.That(exposure.IsOverflowing, Is.False);
        Assert.That(exposure.IsDraining, Is.False);
    }

    private void AssertSpeedBuff()
    {
        Assert.That(attributes.GetCurrentValue(move), Is.EqualTo(1.25f).Within(0.0001f));
        Assert.That(attributes.GetCurrentValue(attack), Is.EqualTo(0.4f).Within(0.0001f));
    }

    private void AssertHudDuration(bool expected)
    {
        var entries = new List<StatusHudEntry>();
        exposure.GetComponent<PlayerStatusRuntime>().CollectStatusHudEntries(entries);
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].ShowDuration, Is.EqualTo(expected));
    }

    private void SetAffinity(int amount) => affinity.SetAffection(null, 2001, amount, syncToGameData: false);
    private void Tick(float seconds) => typeof(PlayerAlcoholExposure).GetMethod("Tick", Private).Invoke(exposure, new object[] { seconds });
    private static void TickInput(PlayerIntentInput2D input) =>
        typeof(PlayerIntentInput2D).GetMethod("Update", Private).Invoke(input, null);
    private Vector2 ResolveDash(AbilityLogic_Dash2D dash, bool fallbackToAim) =>
        (Vector2)typeof(AbilityLogic_Dash2D).GetMethod("ResolveMoveDirection", Private)
            .Invoke(dash, new object[] { exposure.GetComponent<AbilitySystem>(), fallbackToAim });
    private ActiveGameplayEffect Active(CombatBuffDebuffApplicationDefinition definition) =>
        runner.FindActiveEffect(definition.GameplayEffect, exposure.gameObject);

    private PlayerRuntimeState CaptureSceneState() => PlayerRuntimeCaptureCoordinator.CaptureAll(new PlayerSystemContext
    {
        attributeSet = attributes,
        effectRunner = runner,
        tagSystem = exposure.GetComponent<TagSystem>(),
        abilitySystem = exposure.GetComponent<AbilitySystem>()
    });

    private void Advance(float seconds)
    {
        // Production GE and AttributeValue clocks remain authoritative. Tick them
        // before the exposure's endpoint refresh to detect mismatched durations.
        typeof(GameplayEffectRunner).GetMethod("TickEffects", Private).Invoke(runner, new object[] { seconds });
        foreach (AttributeDefinition definition in attributes.EnumerateDefinitions())
            attributes.GetAttribute(definition).Update(seconds);
        Tick(seconds);
    }

    private CombatBuffDebuffApplicationDefinition CloneDefinition(string filename)
    {
        var authored = AssetDatabase.LoadAssetAtPath<CombatBuffDebuffApplicationDefinition>(
            "Assets/_Project/Data/Abilities/Effects/" + filename);
        Assert.That(authored, Is.Not.Null, filename);
        Assert.That(authored.GameplayEffect, Is.Not.Null, filename);
        Assert.That(authored.GameplayEffect.duration, Is.EqualTo(5f), "Authored duration must match payload modifier lifetime.");
        var definition = Own(Object.Instantiate(authored));
        var effect = Own(Object.Instantiate(authored.GameplayEffect));
        SetField(definition, "gameplayEffect", effect);
        return definition;
    }

    private T Own<T>(T value) where T : Object
    {
        owned.Add(value);
        return value;
    }

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, Private).SetValue(target, value);

    private sealed class TestProfileBackend : IGameDataStoreBackend
    {
        public GameData Data { get; } = new();
        public int ActiveSlotIndex => 0;
        public event Action<GameData, int> OnDataLoaded { add { } remove { } }
        public GameData EnsureData() => Data;
        public void SaveData() { }
        public void RequestImmediateSave(Object requester) { }
        public void RequestDeferredSave(Object requester) { }
        public void FlushSave(Object requester) { }
    }

    private sealed class TestInputBackend : IInputActionQueryBackend
    {
        public Component BackendComponent { get; set; }
        public Vector2 Move;
        public Sprite GetBindingIcon(InputActionId action) => null;
        public bool WasPressedThisFrame(InputActionId action) => false;
        public bool WasReleasedThisFrame(InputActionId action) => false;
        public bool IsPressed(InputActionId action) => false;
        public bool IsKeyPressed(KeyCode key) => false;
        public bool WasKeyPressedThisFrame(KeyCode key) => false;
        public bool WasKeyReleasedThisFrame(KeyCode key) => false;
        public Vector2 GetMoveVectorRaw() => Move;
        public Vector2 GetMoveVectorNormalized() => Move;
        public Vector3 GetPointerWorldPosition(Camera camera, float z = 0f) => Vector3.zero;
    }
}
#endif

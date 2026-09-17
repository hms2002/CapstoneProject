#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class PlayerControlAndChestRegressionPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<Object> owned = new();
    private UnityEditor.EditorApplication.CallbackFunction pausedTraceFlush;
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, Private).Invoke(target, args);
    private static T Call<T>(object target, string method, params object[] args) =>
        (T)target.GetType().GetMethod(method, Private).Invoke(target, args);

    [UnityTest]
    public IEnumerator SkillCooldownBuffer_WaitsForReadyThenActivatesOnce()
    {
        var actor = Own(new GameObject("Skill cooldown input buffer"));
        actor.AddComponent<AttributeSet>();
        var system = actor.AddComponent<AbilitySystem>();
        var inventory = actor.AddComponent<WeaponInventory2D>();
        var combat = actor.AddComponent<PlayerCombatInput2D>();
        var logic = Own(ScriptableObject.CreateInstance<PlayerControlRegressionLogic>());
        var skill = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        var weapon = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        skill.logic = logic;
        skill.cooldown = 1f;
        skill.recoveryTime = 0f;
        weapon.skill1 = skill;

        Assert.That(inventory.TrySetWeaponSlot(0, weapon), Is.True);
        Assert.That(system.TrySetCooldownRemaining(skill, 0.05f), Is.True);

        Assert.That(
            Call<bool>(combat, "TryActivateSafe", WeaponAbilitySlot.Skill1, skill),
            Is.True,
            "An input inside the 0.08 second window should be accepted as buffered.");
        Assert.That(system.IsBusy, Is.False, "Buffering must not execute the skill before cooldown is ready.");

        Call(combat, "TryConsumeCooldownSkillInput");
        Assert.That(system.IsBusy, Is.False);

        Assert.That(system.TrySetCooldownRemaining(skill, 0f), Is.True);
        Call(combat, "TryConsumeCooldownSkillInput");
        Assert.That(system.CurrentExecSpec?.Definition, Is.EqualTo(skill));

        logic.Complete = true;
        yield return null;
    }

    [UnityTest]
    public IEnumerator ActualExecution_OutlivesEstimatedRecovery_AndCancellationUnlocks()
    {
        var actor = Own(new GameObject("Actual attack lifecycle"));
        actor.AddComponent<AttributeSet>();
        var system = actor.AddComponent<AbilitySystem>();
        var combat = actor.AddComponent<PlayerCombatInput2D>();
        var logic = Own(ScriptableObject.CreateInstance<PlayerControlRegressionLogic>());
        var attack = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        attack.logic = logic;
        attack.recoveryTime = 0f;
        system.GiveAbility(attack);
        Call(combat, "RememberBasicAttack", WeaponAbilitySlot.Attack, attack);
        Assert.That(system.TryActivateAbility(attack), Is.True);
        yield return null;
        Assert.That(combat.IsBasicAttackMovementLocked, Is.True);
        // Old timer expired in 0.016s without an Animator, even though logic was still running.
        for (int frame = 0; frame < 12; frame++)
        {
            Assert.That(system.IsExecuting, Is.True);
            Assert.That(combat.IsBasicAttackMovementLocked, Is.True);
            yield return null;
        }
        system.CancelExecution(true);
        yield return null;
        Assert.That(combat.IsBasicAttackMovementLocked, Is.False);
    }

    [UnityTest]
    public IEnumerator BufferedRequest_DoesNotLock_ButItsDeferredExecutionDoes()
    {
        var actor = Own(new GameObject("Buffered attack lifecycle"));
        actor.AddComponent<AttributeSet>();
        var system = actor.AddComponent<AbilitySystem>();
        Set(system, "enableExclusiveActivationBuffer", true);
        var combat = actor.AddComponent<PlayerCombatInput2D>();
        var blockerLogic = Own(ScriptableObject.CreateInstance<PlayerControlRegressionLogic>());
        var attackLogic = Own(ScriptableObject.CreateInstance<PlayerControlRegressionLogic>());
        var blocker = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        var attack = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        blocker.logic = blockerLogic;
        attack.logic = attackLogic;
        blocker.recoveryTime = attack.recoveryTime = 0f;
        system.GiveAbility(blocker);
        system.GiveAbility(attack);
        Call(combat, "RememberBasicAttack", WeaponAbilitySlot.Attack, attack);
        Assert.That(system.TryActivateAbility(blocker), Is.True);
        yield return null;
        Assert.That(system.TryActivateAbility(attack), Is.True);
        Assert.That(combat.IsBasicAttackMovementLocked, Is.False, "Buffered success is not execution.");
        blockerLogic.Complete = true;
        for (int frame = 0; frame < 30 && system.CurrentExecSpec?.Definition != attack; frame++)
            yield return null;
        Assert.That(system.CurrentExecSpec?.Definition, Is.EqualTo(attack));
        Assert.That(combat.IsBasicAttackMovementLocked, Is.True,
            "Deferred execution must lock even without another input call or selected weapon.");
        attackLogic.Complete = true;
        for (int frame = 0; frame < 30 && system.IsExecuting; frame++) yield return null;
        yield return null;
        Assert.That(combat.IsBasicAttackMovementLocked, Is.False);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        if (pausedTraceFlush != null)
            UnityEditor.EditorApplication.update += pausedTraceFlush;
        pausedTraceFlush = null;
    }

    [Test]
    public void AttackMotionLocksWalking_RepressWithoutNewAttackDoesNotRelock()
    {
        var actor = Own(new GameObject("Attack lock regression"));
        var combat = actor.AddComponent<PlayerCombatInput2D>();
        var intent = actor.AddComponent<PlayerIntentInput2D>();
        Set(intent, "<MoveInput>k__BackingField", Vector2.up);
        Set(combat, "isHoldingAttack", true);
        Assert.That(combat.IsMeleeControlLocked, Is.False, "Next swing must not deadlock on the hold lock.");
        Assert.That(intent.GetIntent().Direction, Is.EqualTo(Vector2.up),
            "Input alone must not lock walking before a new attack starts.");

        Set(combat, "meleeControlLockActive", true);
        Set(combat, "meleeTickFrame", Time.frameCount);
        Assert.That(intent.GetIntent().Direction, Is.EqualTo(Vector2.zero));
        Call(combat, "ReleaseAttackHoldIfNeeded");
        Assert.That(combat.IsBasicAttackMovementLocked, Is.True, "Releasing cannot cancel the current phase lock.");
        Set(combat, "meleeControlLockActive", false);
        Assert.That(intent.GetIntent().Direction, Is.EqualTo(Vector2.up));

        Set(combat, "isHoldingAttack", true);
        Assert.That(intent.GetIntent().Direction, Is.EqualTo(Vector2.up),
            "Repressing during the released motion tail must not lock walking.");
        Set(combat, "meleeControlLockActive", true);
        Assert.That(intent.GetIntent().Direction, Is.EqualTo(Vector2.zero),
            "A newly started attack must lock walking again.");
        combat.enabled = false;
        Assert.That(combat.IsHoldingPrimaryAttack, Is.False, "Disable must clear held input.");
        Assert.That(combat.IsBasicAttackMovementLocked, Is.False);
    }

    [Test]
    public void InputActionPressBlock_IsOwnerScoped_AndDoesNotBecomeAFullWeaponBlock()
    {
        var actor = Own(new GameObject("Skill input block regression"));
        var combat = actor.AddComponent<PlayerCombatInput2D>();
        var firstOwner = new object();
        var secondOwner = new object();

        try
        {
            InputActionQuery.SetPressBlocked(InputActionId.Skill1, firstOwner, true);
            InputActionQuery.SetPressBlocked(InputActionId.Skill1, secondOwner, true);

            Assert.That(InputActionQuery.IsPressBlocked(InputActionId.Skill1), Is.True);
            Assert.That(combat.IsWeaponInputBlocked, Is.False,
                "Tooltip inspection must not hide the weapon HUD or block basic attacks.");

            InputActionQuery.SetPressBlocked(InputActionId.Skill1, firstOwner, false);
            Assert.That(InputActionQuery.IsPressBlocked(InputActionId.Skill1), Is.True,
                "One tooltip owner cannot release another owner's block.");

            InputActionQuery.SetPressBlocked(InputActionId.Skill1, secondOwner, false);
            Assert.That(InputActionQuery.IsPressBlocked(InputActionId.Skill1), Is.False);
        }
        finally
        {
            InputActionQuery.SetPressBlocked(InputActionId.Skill1, firstOwner, false);
            InputActionQuery.SetPressBlocked(InputActionId.Skill1, secondOwner, false);
        }
    }

    [Test]
    public void WorldItemHover_OwnsBothSkillPressBlocksUntilHidden()
    {
        var anchor = Own(new GameObject("World item hover input owner"));
        var relic = Own(ScriptableObject.CreateInstance<RelicDefinition>());
        FieldInfo backendField = typeof(WorldItemHoverPlayback).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = (IWorldItemHoverBackend)backendField.GetValue(null);
        WorldItemHoverPlayback.RegisterBackend(null);

        try
        {
            WorldItemHoverPlayback.Show(anchor.transform, relic);
            Assert.That(InputActionQuery.IsPressBlocked(InputActionId.Skill1), Is.True);
            Assert.That(InputActionQuery.IsPressBlocked(InputActionId.Skill2), Is.True);

            WorldItemHoverPlayback.Hide(anchor.transform);
            Assert.That(InputActionQuery.IsPressBlocked(InputActionId.Skill1), Is.False);
            Assert.That(InputActionQuery.IsPressBlocked(InputActionId.Skill2), Is.False);
        }
        finally
        {
            WorldItemHoverPlayback.Hide();
            WorldItemHoverPlayback.RegisterBackend(previous);
        }
    }

    [Test]
    public void SkillInputBlock_PreventsPressedSkillFromStarting()
    {
        var actor = Own(new GameObject("Blocked skill activation regression"));
        actor.AddComponent<AttributeSet>();
        var system = actor.AddComponent<AbilitySystem>();
        var inventory = actor.AddComponent<WeaponInventory2D>();
        var combat = actor.AddComponent<PlayerCombatInput2D>();
        var logic = Own(ScriptableObject.CreateInstance<PlayerControlRegressionLogic>());
        var skill = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        var weapon = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        skill.logic = logic;
        skill.recoveryTime = 0f;
        weapon.skill1 = skill;
        Assert.That(inventory.TrySetWeaponSlot(0, weapon), Is.True);

        var input = new PlayerControlTestInput
        {
            BackendComponent = actor.transform,
            PressedAction = InputActionId.Skill1,
        };
        FieldInfo backendField = typeof(InputActionQuery).GetField("backend", BindingFlags.Static | BindingFlags.NonPublic);
        var previous = (IInputActionQueryBackend)backendField.GetValue(null);
        InputActionQuery.RegisterBackend(input);

        try
        {
            InputActionQuery.SetPressBlocked(InputActionId.Skill1, this, true);
            Call(combat, "Update");
            Assert.That(system.IsBusy, Is.False);

            InputActionQuery.SetPressBlocked(InputActionId.Skill1, this, false);
            Call(combat, "Update");
            Assert.That(system.CurrentExecSpec?.Definition, Is.EqualTo(skill));
        }
        finally
        {
            InputActionQuery.SetPressBlocked(InputActionId.Skill1, this, false);
            InputActionQuery.RegisterBackend(previous);
        }
    }

    [Test]
    public void DuplicatePotion_HighlightBudgetTracksAcquisitionsAndReturns()
    {
        // UI is intentionally accessed through reflection: this test assembly references Gameplay,
        // while the production assembly boundary keeps UI out of Gameplay.
        Type screenType = Type.GetType("ChestScreen, UI", true);
        Type slotType = Type.GetType("ItemSlotUI, UI", true);
        var host = Own(new GameObject("Chest marker regression"));
        host.SetActive(false);
        Component screen = host.AddComponent(screenType);
        var potion = Own(ScriptableObject.CreateInstance<ConsumableDefinition>());
        var inventory = new TestContainer();
        inventory.Items[0] = potion; // Pre-owned copy.
        var chest = new ChestInventory();
        Array slots = Array.CreateInstance(slotType, 3);
        for (int i = 0; i < 3; i++)
        {
            var go = Own(new GameObject("Slot " + i, typeof(RectTransform)));
            go.SetActive(false);
            Component slot = go.AddComponent(slotType);
            Set(slot, "container", inventory);
            Set(slot, "index", i);
            slots.SetValue(slot, i);
        }
        Set(screen, "counterInventory", chest);
        Set(screen, "returnHighlightSlots", slots);
        Set(screen, "previousReturnItems", new ScriptableObject[3]);
        Set(screen, "nextReturnHighlights", new bool[3]);
        Call(screen, "RefreshReturnHighlights");

        inventory.Items[1] = potion;
        chest.RecordAcquisition(potion);
        Call(screen, "RefreshReturnHighlights");
        AssertMarkers(false, true, false);
        Call(screen, "RefreshReturnHighlights");
        AssertMarkers(false, true, false);
        inventory.Items[2] = potion;
        chest.RecordAcquisition(potion);
        Call(screen, "RefreshReturnHighlights");
        AssertMarkers(false, true, true);

        inventory.Items[1] = null;
        chest.RecordReturn(potion);
        Call(screen, "RefreshReturnHighlights");
        AssertMarkers(false, false, true);
        chest.RecordReturn(potion);
        Call(screen, "RefreshReturnHighlights");
        AssertMarkers(false, false, false);

        void AssertMarkers(params bool[] expected)
        {
            for (int i = 0; i < expected.Length; i++)
                Assert.That(slotType.GetProperty("IsChestReturnHighlighted").GetValue(slots.GetValue(i)),
                    Is.EqualTo(expected[i]), "Slot " + i);
        }
    }

    [Test]
    public void ChestSelection_ReservesDistinctSlots_AndRejectsWholeSelectionWhenOnlyOneFits()
    {
        var weapon = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        var chest = new ChestInventory(2);
        chest.Set(0, weapon);
        chest.Set(1, weapon);
        using var source = new ChestContainerAdapter(chest);
        var target = new TestContainer();
        target.Items[0] = weapon;
        target.Items[1] = weapon;
        var planner = Type.GetType("ChestSelectionTransferService, UI", true).GetMethod("TryCreatePlan");
        object[] args = { source, new List<int> { 0, 1 }, null, target, null, null, null };

        Assert.That(planner.Invoke(null, args), Is.False);
        Assert.That(chest.Get(0), Is.SameAs(weapon));
        Assert.That(chest.Get(1), Is.SameAs(weapon));
        Assert.That(chest.AcquiredCount, Is.Zero);
        Assert.That(target.Get(2), Is.Null);
        StringAssert.Contains("드래그", (string)args[6]);

        target.Items[1] = null;
        Assert.That(planner.Invoke(null, args), Is.True);
        var requests = (IList)args[5];
        Assert.That(requests.Count, Is.EqualTo(2));
        Type requestType = requests[0].GetType();
        Assert.That(requestType.GetProperty("TargetIndex").GetValue(requests[0]),
            Is.Not.EqualTo(requestType.GetProperty("TargetIndex").GetValue(requests[1])));
        var transfer = Type.GetType("InventoryTransferService, UI", true).GetMethod("TryTransfer");
        foreach (object request in requests)
        {
            object result = transfer.Invoke(null, new[] { request });
            Assert.That(result.GetType().GetProperty("Succeeded").GetValue(result), Is.True);
        }
        Assert.That(chest.AcquiredCount, Is.EqualTo(2));
        Assert.That(chest.Get(0), Is.Null);
        Assert.That(chest.Get(1), Is.Null);
    }

    [Test]
    public void ChestSelection_ReadOnlyAdapterRejectsTransfersBeforeRelicMerge()
    {
        var relic = Own(ScriptableObject.CreateInstance<RelicDefinition>());
        relic.relicId = "selection-merge";
        relic.maxLevel = 5;
        var player = Own(new GameObject("Selection relic inventory"));
        var relicInventory = player.AddComponent<RelicInventory>();
        Assert.That(relicInventory.TrySetRelicSlotWithLevel(0, relic, 1), Is.True);
        using var target = new PlayerRelicContainerAdapter(relicInventory);
        var chest = new ChestInventory(2);
        chest.SetRelicWithLevel(0, relic, 1);
        using var source = new ChestContainerAdapter(chest, selectionOnly: true);
        Type requestType = Type.GetType("InventoryTransferRequest, UI", true);
        object request = Activator.CreateInstance(requestType, source, 0, target, 0, 1);
        object result = Type.GetType("InventoryTransferService, UI", true)
            .GetMethod("TryTransfer").Invoke(null, new[] { request });
        Assert.That(result.GetType().GetProperty("Succeeded").GetValue(result), Is.False);
        Assert.That(relicInventory.GetRelicLevelInSlot(0), Is.EqualTo(1));
        Assert.That(chest.Get(0), Is.SameAs(relic));
        Assert.That(source.TrySet(0, null), Is.False);
    }

    [Test]
    public void ChestSelection_CompletionClearsRemainingLoot_AndStaysHiddenAfterRestore()
    {
        var weapon = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        var host = Own(new GameObject("Completed chest"));
        var chest = host.AddComponent<TreasureChest>();
        chest.InitializeWithLoot(new List<ScriptableObject> { weapon, weapon });
        chest.GetInventory().RecordAcquisition(weapon);
        chest.CompleteLootSelection();
        Assert.That(host.activeSelf, Is.False);
        Assert.That(chest.CaptureDungeonLootState(), Is.Empty);
        Assert.That(chest.AcquiredCount, Is.EqualTo(1));
        host.SetActive(true);
        chest.RestoreOpenedStateForDungeon(chest.CaptureDungeonLootState(), chest.AcquiredCount);
        Assert.That(host.activeSelf, Is.False);
    }

    [Test]
    public void ChestSelection_AllAuthoredChestPrefabsHavePanelAndConfirmReferences()
    {
        Type screenType = Type.GetType("ChestScreen, UI", true);
        foreach (string name in new[] { "ChestUI", "GlobalUIRoot", "GlobalUIRoot_Deafiso", "GlobalUIRoot_DialogueUpdate",
            "GlobalUIRoot_Salryojo", "GlobalUIRoot_Sub", "GlobalUIRoot_Water" })
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/UI/{name}.prefab");
            var screen = prefab.GetComponentInChildren(screenType, true);
            Assert.That(screen, Is.Not.Null, name);
            foreach (string field in new[] { "selectedItemsRoot", "selectedItemsGroup", "confirmSelectionButton", "acquisitionCountLabel" })
                Assert.That(screenType.GetField(field, Private).GetValue(screen), Is.Not.Null, $"{name}.{field}");
        }
    }

    [Test]
    public void ChestSelection_LaterTransferFailureRollsBackEarlierItemAndAcquisitionCount()
    {
        var weapon = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        var chest = new ChestInventory(2);
        chest.Set(0, weapon);
        chest.Set(1, weapon);
        using var source = new ChestContainerAdapter(chest);
        var target = new TestContainer { RejectNonEmptyIndex = 1 };
        Type service = Type.GetType("ChestSelectionTransferService, UI", true);
        object[] args = { source, new List<int> { 0, 1 }, null, target, null, null, null };
        Assert.That(service.GetMethod("TryCreatePlan").Invoke(null, args), Is.True);
        object result = service.GetMethod("TryCommitPlan").Invoke(null, new[] { args[5] });
        Assert.That(result.GetType().GetProperty("Succeeded").GetValue(result), Is.False);
        Assert.That(chest.Get(0), Is.SameAs(weapon));
        Assert.That(chest.Get(1), Is.SameAs(weapon));
        Assert.That(chest.AcquiredCount, Is.Zero);
        Assert.That(chest.OutstandingAcquisitions, Is.Empty);
        Assert.That(target.Get(0), Is.Null);
        Assert.That(target.Get(1), Is.Null);
    }

    [TestCase(2, false)]
    [TestCase(3, true)]
    public void ChestSelection_FullRelicInventoryReservesCombinedLevels(int maxLevel, bool expected)
    {
        var relic = Own(ScriptableObject.CreateInstance<RelicDefinition>());
        relic.relicId = "selection-levels";
        relic.maxLevel = maxLevel;
        var host = Own(new GameObject("Full relic inventory"));
        var inventory = host.AddComponent<RelicInventory>();
        inventory.TrySetRelicSlotWithLevel(0, relic, 1);
        for (int i = 1; i < inventory.Capacity; i++)
        {
            var filler = Own(ScriptableObject.CreateInstance<RelicDefinition>());
            filler.relicId = $"filler-{i}";
            inventory.TrySetRelicSlotWithLevel(i, filler, 1);
        }
        using var target = new PlayerRelicContainerAdapter(inventory);
        var chest = new ChestInventory(2);
        chest.SetRelicWithLevel(0, relic, 1);
        chest.SetRelicWithLevel(1, relic, 1);
        using var source = new ChestContainerAdapter(chest);
        Type service = Type.GetType("ChestSelectionTransferService, UI", true);
        object[] args = { source, new List<int> { 0, 1 }, null, null, target, null, null };
        Assert.That(service.GetMethod("TryCreatePlan").Invoke(null, args), Is.EqualTo(expected));
        Assert.That(inventory.GetRelicLevelInSlot(0), Is.EqualTo(1));
        if (!expected) return;
        object result = service.GetMethod("TryCommitPlan").Invoke(null, new[] { args[5] });
        Assert.That(result.GetType().GetProperty("Succeeded").GetValue(result), Is.True);
        Assert.That(inventory.GetRelicLevelInSlot(0), Is.EqualTo(3));
        Assert.That(chest.AcquiredCount, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator ChestSelection_SlotsMoveAndReturnInOriginalOrderWhilePaused_ClosingCancelsSelection()
    {
        // Editor trace-file flushing can race AssetDatabase import in batch mode.
        // Isolate this UI test from that unrelated disk recorder without hiding runtime errors.
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type trace = assembly.GetType("PrewarmTraceRuntime");
            if (trace == null) continue;
            MethodInfo flush = trace.GetMethod("FlushIfNeeded", BindingFlags.Static | BindingFlags.NonPublic);
            pausedTraceFlush = (UnityEditor.EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                typeof(UnityEditor.EditorApplication.CallbackFunction), flush);
            UnityEditor.EditorApplication.update -= pausedTraceFlush;
            break;
        }
        Type screenType = Type.GetType("ChestScreen, UI", true);
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/GlobalUIRoot.prefab");
        var prefabScreen = (Component)prefab.GetComponentInChildren(screenType, true);
        var canvas = Own(new GameObject("Chest selection test canvas", typeof(RectTransform), typeof(Canvas)));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var clone = Own(Object.Instantiate(prefabScreen.gameObject, canvas.transform));
        clone.SetActive(true);
        var screen = clone.GetComponent(screenType);
        var weapon = Own(ScriptableObject.CreateInstance<WeaponDefinition>());
        var chest = new ChestInventory(16);
        chest.Set(0, weapon);
        chest.Set(3, weapon);
        chest.Set(7, weapon);
        screenType.GetMethod("BindChestOnly").Invoke(screen, new object[] { chest, null });
        object presentation = screenType.GetField("firstOpenRevealPresentation", Private).GetValue(screen);
        presentation.GetType().GetMethod("SnapOpen").Invoke(presentation, null);
        var grid = (Transform)screenType.GetField("chestGridRoot", Private).GetValue(screen);
        var selected = (Transform)screenType.GetField("selectedItemsRoot", Private).GetValue(screen);
        var slots = (IList)screenType.GetField("spawnedChestSlots", Private).GetValue(screen);
        Assert.That(slots.Count, Is.EqualTo(3), "Empty chest slots must not be instantiated.");
        object middle = slots[1];
        float oldTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            Call(screen, "ToggleSelection", middle);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(selected.childCount, Is.EqualTo(1));
            Assert.That(grid.childCount, Is.EqualTo(2));
            Assert.That(chest.Get(3), Is.SameAs(weapon), "Selection must not acquire the item.");
            Call(screen, "ToggleSelection", middle);
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.That(selected.childCount, Is.Zero);
            Assert.That(grid.GetChild(1), Is.SameAs(((Component)middle).transform));
            Call(screen, "ToggleSelection", middle);
            clone.SetActive(false); // Interrupt the tween through the same disable cleanup used on close.
            Assert.That(chest.Get(3), Is.SameAs(weapon));
            Assert.That(chest.AcquiredCount, Is.Zero);
            Assert.That(((IList)screenType.GetField("selectedSlots", Private).GetValue(screen)).Count, Is.Zero);
        }
        finally { Time.timeScale = oldTimeScale; }
    }

    private sealed class TestContainer : IItemContainer
    {
        public readonly ScriptableObject[] Items = new ScriptableObject[3];
        public int RejectNonEmptyIndex = -1;
        public int SlotCount => Items.Length;
        public event Action OnChanged { add { } remove { } }
        public ScriptableObject Get(int index) => Items[index];
        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1) => true;
        public bool TrySet(int index, ScriptableObject item)
        {
            if (index == RejectNonEmptyIndex && item != null) return false;
            Items[index] = item;
            return true;
        }
        public bool TrySwap(int a, int b) { (Items[a], Items[b]) = (Items[b], Items[a]); return true; }
    }

    private sealed class PlayerControlTestInput : IInputActionQueryBackend
    {
        public Component BackendComponent { get; set; }
        public InputActionId PressedAction;

        public bool WasPressedThisFrame(InputActionId action) => action == PressedAction;
        public bool WasReleasedThisFrame(InputActionId action) => false;
        public bool IsPressed(InputActionId action) => false;
        public bool IsKeyPressed(KeyCode key) => false;
        public bool WasKeyPressedThisFrame(KeyCode key) => false;
        public bool WasKeyReleasedThisFrame(KeyCode key) => false;
        public Vector2 GetMoveVectorRaw() => Vector2.zero;
        public Vector2 GetMoveVectorNormalized() => Vector2.zero;
        public Vector3 GetPointerWorldPosition(Camera camera, float z = 0f) => Vector3.zero;
    }
}

public sealed class PlayerControlRegressionLogic : AbilityLogic
{
    public bool Complete;
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        while (!Complete && spec.Token != null && !spec.Token.IsCancelled) yield return null;
    }
}
#endif

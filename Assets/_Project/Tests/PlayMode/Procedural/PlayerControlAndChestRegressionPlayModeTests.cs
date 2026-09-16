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

    private sealed class TestContainer : IItemContainer
    {
        public readonly ScriptableObject[] Items = new ScriptableObject[3];
        public int SlotCount => Items.Length;
        public event Action OnChanged { add { } remove { } }
        public ScriptableObject Get(int index) => Items[index];
        public bool CanPlace(ScriptableObject item, int index, int ignoreIndex = -1) => true;
        public bool TrySet(int index, ScriptableObject item) { Items[index] = item; return true; }
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

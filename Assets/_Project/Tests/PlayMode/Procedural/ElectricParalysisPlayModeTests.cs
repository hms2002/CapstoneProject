#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityGAS;
using Object = UnityEngine.Object;

public sealed class ElectricParalysisPlayModeTests
{
    private readonly List<Object> owned = new();
    private float previousScale, previousMaximum, previousFixed;
    private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

    [SetUp]
    public void SetUp()
    {
        previousScale = Time.timeScale;
        previousMaximum = Time.maximumDeltaTime;
        previousFixed = Time.fixedDeltaTime;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.01f;
        Time.maximumDeltaTime = 0.02f;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        Time.timeScale = previousScale;
        Time.maximumDeltaTime = previousMaximum;
        Time.fixedDeltaTime = previousFixed;
    }

    private GameObject Actor(string name) => Own(new GameObject(name));
    private static IEnumerator WaitGameSeconds(float seconds)
    {
        double end = Time.timeAsDouble + seconds;
        while (Time.timeAsDouble < end) yield return null;
    }

    [UnityTest]
    public IEnumerator Paralysis_FreezesActionClockAnimationAndMotion_ThenContinuesSameMotion()
    {
        var actor = Actor("Paralysis motion owner");
        actor.transform.position = new Vector3(1500f, 1500f, 0f);
        actor.SetActive(false);
        var body = actor.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        var animator = actor.AddComponent<Animator>();
        animator.speed = 0.75f;
        var motion = actor.AddComponent<AbilityMotionController2D>();
        var motor = actor.AddComponent<MovementMotor2D>();
        actor.SetActive(true);
        motion.StartDash(Vector2.right, 2f, 0.3f);
        yield return null;
        int version = motion.MotionVersion;
        Vector2 pausedPosition = body.position;
        double actionTime = CombatHitPause2D.GetActionTimeAsDouble(actor);
        CombatHitPause2D.ApplyParalysis(actor, 0.1f);
        Assert.That(animator.speed, Is.Zero);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        yield return WaitGameSeconds(0.04f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(actor), Is.True);
        Assert.That(CombatHitPause2D.GetActionTimeAsDouble(actor), Is.EqualTo(actionTime).Within(0.0001d));
        Assert.That(Vector2.Distance(body.position, pausedPosition), Is.LessThan(0.0001f));
        Assert.That(motor.LastFinalVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(motion.MotionVersion, Is.EqualTo(version));
        Assert.That(motion.HasActiveMotion, Is.True);
        yield return WaitGameSeconds(0.08f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(actor), Is.False);
        Assert.That(animator.speed, Is.EqualTo(0.75f));
        Assert.That(motion.HasActiveMotion, Is.True, "The interrupted motion has not been cancelled or restarted.");
        Assert.That(motion.MotionVersion, Is.EqualTo(version));
        yield return WaitGameSeconds(0.08f);
        Assert.That(body.position.x, Is.GreaterThan(pausedPosition.x));
        yield return WaitGameSeconds(0.3f);
        Assert.That(motion.HasActiveMotion, Is.False);
    }

    [UnityTest]
    public IEnumerator Paralysis_RenewsWithoutAddingDuration_AndKeepsLongerHitPause()
    {
        var actor = Actor("Paralysis renewal owner");
        var animator = actor.AddComponent<Animator>();
        animator.speed = 0.6f;
        CombatHitPause2D.Apply(actor, 0.3f);
        double start = CombatHitPause2D.GetActionTimeAsDouble(actor);
        CombatHitPause2D.ApplyParalysis(actor, 0.1f);
        yield return WaitGameSeconds(0.04f);
        CombatHitPause2D.ApplyParalysis(actor, 0.1f);
        yield return WaitGameSeconds(0.07f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(actor), Is.True);
        Assert.That(CombatHitPause2D.GetActionTimeAsDouble(actor), Is.EqualTo(start).Within(0.0001d));
        yield return WaitGameSeconds(0.05f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(actor), Is.False);
        Assert.That(CombatHitPause2D.IsPausedOn(actor), Is.True, "Ending paralysis cannot release a longer ordinary hit pause.");
        Assert.That(animator.speed, Is.Zero);
        double resumed = CombatHitPause2D.GetActionTimeAsDouble(actor);
        yield return WaitGameSeconds(0.03f);
        Assert.That(CombatHitPause2D.GetActionTimeAsDouble(actor), Is.GreaterThan(resumed));
        yield return WaitGameSeconds(0.15f);
        Assert.That(animator.speed, Is.EqualTo(0.6f));
    }

    [UnityTest]
    public IEnumerator Paralysis_DisableAndClearReleaseOnlyItsHold_AndNewIntervalFreezesAtCurrentPoint()
    {
        var actor = Actor("Paralysis cleanup owner");
        var animator = actor.AddComponent<Animator>();
        animator.speed = 0.8f;
        CombatHitPause2D.ApplyParalysis(actor, 0.1f);
        yield return WaitGameSeconds(0.04f);
        CombatHitPause2D.ClearParalysis(actor);
        Assert.That(CombatHitPause2D.IsParalyzedOn(actor), Is.False);
        Assert.That(animator.speed, Is.EqualTo(0.8f));
        yield return WaitGameSeconds(0.04f);
        double secondStart = CombatHitPause2D.GetActionTimeAsDouble(actor);
        CombatHitPause2D.ApplyParalysis(actor, 0.1f);
        yield return WaitGameSeconds(0.03f);
        Assert.That(CombatHitPause2D.GetActionTimeAsDouble(actor), Is.EqualTo(secondStart).Within(0.0001d));
        actor.SetActive(false);
        Assert.That(animator.speed, Is.EqualTo(0.8f));
        actor.SetActive(true);
        Assert.That(CombatHitPause2D.IsParalyzedOn(actor), Is.False);
        Assert.That(CombatHitPause2D.GetActionTime(actor), Is.EqualTo(Time.time));
    }

    [UnityTest]
    public IEnumerator Paralysis_MenuPauseKeepsRemainingTime_AndActorClockNeverFreezesOtherActor()
    {
        var actor = Actor("Paralysis menu owner");
        var other = Actor("Unrelated actor");
        CombatHitPause2D.ApplyParalysis(actor, 0.1f);
        double actorStart = CombatHitPause2D.GetActionTimeAsDouble(actor);
        double otherStart = CombatHitPause2D.GetActionTimeAsDouble(other);
        yield return WaitGameSeconds(0.03f);
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(0.15f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(actor), Is.True);
        Assert.That(CombatHitPause2D.GetActionTimeAsDouble(actor), Is.EqualTo(actorStart).Within(0.0001d));
        Time.timeScale = 1f;
        yield return WaitGameSeconds(0.03f);
        Assert.That(CombatHitPause2D.GetActionTimeAsDouble(other), Is.GreaterThan(otherStart));
        yield return WaitGameSeconds(0.1f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(actor), Is.False);
    }

    [UnityTest]
    public IEnumerator Paralysis_EventWaitKeepsRemainingTimeout_AndDeliversEventAfterResume()
    {
        var actor = Actor("Paralysis event waiter");
        var system = actor.AddComponent<AbilitySystem>();
        var definition = Own(ScriptableObject.CreateInstance<AbilityDefinition>());
        var tag = Own(ScriptableObject.CreateInstance<GameplayTag>());
        var spec = new AbilitySpec(definition);
        bool received = false;
        var run = CombatHitPause2D.Run(system, spec,
            AbilityTasks.WaitGameplayEvent(system, spec, tag, _ => received = true, 0.08f));
        try
        {
            Assert.That(run.MoveNext(), Is.True);
            CombatHitPause2D.ApplyParalysis(actor, 0.1f);
            yield return WaitGameSeconds(0.12f);
            Assert.That(run.MoveNext(), Is.True, "Only unpaused action time may consume the event timeout.");
            system.SendGameplayEvent(tag, new AbilityEventData());
            Assert.That(run.MoveNext(), Is.False);
            Assert.That(received, Is.True);
        }
        finally { (run as IDisposable)?.Dispose(); }
    }

    [UnityTest]
    public IEnumerator Paralysis_TelegraphProgressRemainsAtPausedPoint_ThenResumes()
    {
        var actor = Actor("Paralysis telegraph owner");
        var warningPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/VFX/Telegraphs/AttackTelegraphView.prefab");
        Assert.That(warningPrefab, Is.Not.Null);
        Type viewType = Type.GetType("UnityGAS.AttackTelegraphView, Presentation", true);
        var view = Own(Object.Instantiate(warningPrefab)).GetComponent(viewType);
        Assert.That(view, Is.Not.Null);
        viewType.GetMethod("Show").Invoke(view, new object[] { AttackTelegraphSpec.CreateCircle(Vector3.zero, 2f, 0.4f), null, actor });
        yield return WaitGameSeconds(0.04f);
        var progress = viewType.GetMethod("GetCurrentNormalizedProgress", BindingFlags.Instance | BindingFlags.NonPublic);
        float before = (float)progress.Invoke(view, null);
        CombatHitPause2D.ApplyParalysis(actor, 0.1f);
        yield return WaitGameSeconds(0.04f);
        Assert.That((float)progress.Invoke(view, null), Is.EqualTo(before).Within(0.001f));
        yield return WaitGameSeconds(0.1f);
        Assert.That((float)progress.Invoke(view, null), Is.GreaterThan(before));
    }

    [UnityTest]
    public IEnumerator Paralysis_ActualMobRecoveryPreservesCurrentStateAndRemainingTime()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/SlimeCorridor/Pawn.prefab");
        Assert.That(prefab, Is.Not.Null);
        var actor = Own(Object.Instantiate(prefab, new Vector3(1700f, 1700f), Quaternion.identity));
        var mob = actor.GetComponent<Mob>();
        Assert.That(mob, Is.Not.Null);
        mob.enabled = false; // Drive this existing state machine explicitly, keeping the actor's other components alive.
        var context = new MobAIContext(mob, null, null, null, Array.Empty<IMobPatternRunner>(),
            Array.Empty<IMobPresentationCleanup>(), 0f, 0f, 0f);
        var machine = new MobStateMachine();
        var recover = new MobRecoverState(0.08f);
        machine.SetInitialState(recover, context);
        float end = (float)typeof(MobRecoverState).GetField("recoverEndTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(recover);
        float beforePause = Mathf.Max(0f, end - CombatHitPause2D.GetActionTime(actor) - 0.08f);
        yield return WaitGameSeconds(beforePause);
        machine.Tick(context);
        Assert.That(machine.CurrentState, Is.SameAs(recover));
        CombatHitPause2D.ApplyParalysis(actor, 0.1f);
        yield return WaitGameSeconds(0.12f);
        machine.Tick(context);
        Assert.That(machine.CurrentState, Is.SameAs(recover), "Global time has passed the old deadline, but the same recovery still has remaining action time.");
        yield return WaitGameSeconds(0.1f);
        machine.Tick(context);
        Assert.That(machine.CurrentState, Is.Not.SameAs(recover));
        machine.Shutdown(context);
    }

    [UnityTest]
    public IEnumerator Paralysis_ActualEnemyDeathClearsHoldAndCannotResumeMotion()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Monsters/SlimeCorridor/Pawn.prefab");
        Assert.That(prefab, Is.Not.Null);
        var actor = Own(Object.Instantiate(prefab, new Vector3(1800f, 1800f), Quaternion.identity));
        var enemy = actor.GetComponent<Enemy>();
        var motion = actor.GetComponent<AbilityMotionController2D>();
        Assert.That(enemy, Is.Not.Null);
        Assert.That(motion, Is.Not.Null);
        motion.StartDash(Vector2.right, 2f, 0.3f);
        CombatHitPause2D.ApplyParalysis(actor, 0.1f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(actor), Is.True);
        enemy.RequestDeath();
        Assert.That(enemy.IsDead, Is.True);
        Assert.That(CombatHitPause2D.IsParalyzedOn(actor), Is.False);
        Assert.That(motion.HasActiveMotion, Is.False);
        CombatHitPause2D.ApplyParalysis(actor, 0.1f);
        Assert.That(CombatHitPause2D.IsParalyzedOn(actor), Is.False, "Dead actors cannot acquire a new action hold.");
        yield return WaitGameSeconds(0.15f);
        if (enemy != null) Assert.That(enemy.IsDead, Is.True);
        if (motion != null) Assert.That(motion.HasActiveMotion, Is.False);
    }
}
#endif

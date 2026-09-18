using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

// Run in an isolated Unity project with the freshly built game assemblies.
public static class BossFeedbackRegression
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Set(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(name);
    }

    private static GameObject Inactive(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        return go;
    }

    public static void Run()
    {
        try
        {
            TestOrigin();
            TestReflection();
            TestFinalTransition();
            TestEncounterCompletion();
            Debug.Log("BOSS_FEEDBACK_PASS: launch radius/offset/scaling/wall, circle-cast reflection, final-state re-entry/idempotence, encounter-owned completion.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void TestOrigin()
    {
        var go = new GameObject("Origin probe");
        var ability = go.AddComponent<AbilitySystem>();
        var circle = go.AddComponent<CircleCollider2D>();
        circle.radius = 0.5f;
        circle.offset = new Vector2(0.2f, 0.3f);
        go.transform.localScale = Vector3.one * 2f;
        Physics2D.SyncTransforms();
        foreach (Vector2 direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down, Vector2.one.normalized })
        {
            Vector2 expected = (Vector2)circle.bounds.center + direction * 1.08f;
            Check(Vector2.Distance(PlayerAttackOrigin.Resolve(ability, direction), expected) < 0.001f, "Launch radius is not consistent");
        }
        var wall = new GameObject("Origin wall");
        wall.layer = 30;
        wall.transform.position = new Vector3(1.3f, 0.6f, 0f);
        var collider = wall.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.1f, 5f);
        Physics2D.SyncTransforms();
        Check(PlayerAttackOrigin.Resolve(ability, Vector2.right, 1 << 30).x < collider.bounds.min.x, "Launch crossed wall");
        UnityEngine.Object.DestroyImmediate(wall);
        UnityEngine.Object.DestroyImmediate(go);
    }

    private static void TestReflection()
    {
        var swordObject = Inactive("Reflection sword");
        var sword = swordObject.AddComponent<EgoSwordActor>();
        Set(sword, "contactRadius", 0.25f);
        var right = new GameObject("Right wall");
        var left = new GameObject("Left wall");
        right.layer = left.layer = 30;
        right.transform.position = Vector3.right * 5;
        left.transform.position = Vector3.left * 5;
        right.AddComponent<BoxCollider2D>().size = new Vector2(1, 20);
        left.AddComponent<BoxCollider2D>().size = new Vector2(1, 20);
        Physics2D.SyncTransforms();
        Check(sword.ResolveThrowWarningPath(Vector2.zero, Vector2.right, 1 << 30, out Vector2 end, out Vector2 bounce, out Vector2 reflected), "Missing reflection");
        Check(Mathf.Abs(end.x - 4.25f) < 0.015f, "First cast ignored sword radius: " + end);
        Check(bounce.x < end.x && Mathf.Abs(reflected.x + 4.25f) < 0.015f, "Reflection does not match second wall: " + reflected);
        Check(!sword.ResolveThrowWarningPath(Vector2.zero, Vector2.up, 1 << 30, out _, out _, out _), "Invented reflection without a wall");
        UnityEngine.Object.DestroyImmediate(right);
        UnityEngine.Object.DestroyImmediate(left);
    }

    private sealed class ExecuteProbe : BossPatternExecuteState
    {
        public int Entries;
        public BossPatternEntry Captured;
        public ExecuteProbe(BossControllerBase boss) : base(boss) { }
        public override void OnEnter() { Entries++; Captured = boss.PatternRuntime.ReservedPattern; }
    }

    private static void TestFinalTransition()
    {
        var go = Inactive("Final transition probe");
        var demon = go.AddComponent<DemonKingController>();
        var sword = Inactive("Held sword").AddComponent<EgoSwordActor>();
        Set(demon, "egoSword", sword);
        Set(demon, "runtimePatternsConfigured", true);
        var blackboard = new BossBlackboard(go.transform);
        var machine = new BossStateMachine(blackboard);
        var patterns = new BossPatternRuntimeState();
        var execute = new ExecuteProbe(demon);
        Set(demon, "blackboard", blackboard);
        Set(demon, "stateMachine", machine);
        Set(demon, "patternRuntime", patterns);
        Set(demon, "combatIdleState", new BossCombatIdleState(demon));
        Set(demon, "patternExecuteState", execute);
        var finalPattern = new BossPatternEntry();
        Set(demon, "finalDesperationEntry", finalPattern);
        patterns.ReservePattern(new BossPatternEntry());
        machine.ChangeState(execute);
        var force = typeof(DemonKingController).GetMethod("ForceFinalDesperationNow", BindingFlags.NonPublic | BindingFlags.Instance);
        force.Invoke(demon, null);
        Check(execute.Entries == 2 && ReferenceEquals(execute.Captured, finalPattern), "Final pattern did not re-enter or reservation was cleared by Exit");
        force.Invoke(demon, null);
        Check(execute.Entries == 2, "Final transition ran twice");
    }

    private sealed class ProgressProbe : IRunProgressBackend
    {
        public int Defeats;
        public event Action<BossRewardContext> BossRewardsReady { add { } remove { } }
        public void NotifyBossCombatStarted(BossControllerBase boss) { }
        public void NotifyBossCombatEnded(BossControllerBase boss) { }
        public void NotifyBossDefeated(BossControllerBase boss) { Defeats++; }
        public void NotifyBossRewardsReady(BossControllerBase boss) { }
        public bool IsBossDefeatedThisRun(string id) => false;
    }

    private static void TestEncounterCompletion()
    {
        var boss = Inactive("Phase one probe").AddComponent<SlimeQueen>();
        var host = Inactive("Encounter probe");
        var condition = host.AddComponent<SingleBossEncounterClearCondition>();
        condition.Bind(boss);
        var director = host.AddComponent<BossEncounterEndDirector>();
        Set(director, "clearCondition", condition);
        Set(director, "rewardDelayAfterClearSeconds", 1f);
        typeof(BossEncounterEndDirector).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(director, null);
        var progress = new ProgressProbe();
        RunProgressPlayback.RegisterBackend(progress);
        typeof(BossControllerBase).GetMethod("OnDeathStarted", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss, null);
        Check(progress.Defeats == 0, "Managed individual death completed the encounter");
        var routine = (IEnumerator)typeof(BossEncounterEndDirector).GetMethod("CompleteEncounterRoutine", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(director, null);
        Check(routine.MoveNext() && progress.Defeats == 1, "Encounter completion did not report before reward delay");
        (routine as IDisposable)?.Dispose();
        typeof(BossEncounterEndDirector).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(director, null);
        RunProgressPlayback.UnregisterBackend(progress);
    }
}

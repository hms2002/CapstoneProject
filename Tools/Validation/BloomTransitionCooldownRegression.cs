// Run in an isolated Unity project with freshly built Core/Gameplay assemblies.
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

public static class BloomTransitionCooldownRegression
{
    private const string Pending = "BloomTransitionCooldownRegression.Pending";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    [InitializeOnLoadMethod]
    private static void Resume() { if (SessionState.GetBool(Pending, false)) EditorApplication.update += Tick; }
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.update -= Tick;
        SessionState.SetBool(Pending, false);
        try
        {
            CheckCase(true, false, 240f);
            CheckCase(true, true, 0f);
            CheckCase(false, false, 0f);
            Debug.Log("BLOOM_TRANSITION_COOLDOWN_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    private static void CheckCase(bool deferred, bool skip, float expected)
    {
        var owner = new GameObject("Departing Bloom");
        var arrival = new GameObject("Arriving Bloom");
        var def = ScriptableObject.CreateInstance<AbilityDefinition>();
        var logic = ScriptableObject.CreateInstance<BloomTransitionHoldingLogic>();
        try
        {
            def.name = "Bloom";
            def.cooldown = 240f;
            def.startCooldownOnEnd = deferred;
            def.logic = logic;
            var system = owner.AddComponent<AbilitySystem>();
            var spec = system.GiveAbility(def);
            var routine = system.StartCoroutine(new AbilityExecutionCoordinator().RunParallel(system, spec, null));
            typeof(AbilitySystem).GetMethod("AttachParallelExecutionCoroutine", Hidden)
                .Invoke(system, new object[] { spec, routine });
            spec.SkipCooldownOnEnd = skip;
            system.CancelAllForSceneTransition();
            Require(system.GetCooldownRemaining(def) == expected, "Forced-stop cooldown");
            var restored = arrival.AddComponent<AbilitySystem>();
            restored.ImportPersistentState(system.ExportPersistentState(def), id => def);
            Require(restored.GetCooldownRemaining(def) == expected, "Saved/restored cooldown");
            system.TrySetCooldownRemaining(def, 17f);
            system.CancelAllForSceneTransition();
            Require(system.GetCooldownRemaining(def) == 17f, "Repeated cleanup restarted cooldown");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(arrival);
            UnityEngine.Object.DestroyImmediate(def);
            UnityEngine.Object.DestroyImmediate(logic);
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new Exception(message); }
}

public sealed class BloomTransitionHoldingLogic : AbilityLogic
{
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject target)
    { while (true) yield return null; }
}

// Isolated Unity Editor regression; does not load the game scene.
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityGAS;

public sealed class DragonJumpTestPresenter : MonoBehaviour, IAttackTelegraphPresenter
{
    public sealed class Handle : IAttackTelegraphHandle
    {
        public bool IsVisible { get; private set; } = true;
        public void Show(AttackTelegraphSpec spec) { IsVisible = true; }
        public void UpdateGeometry(AttackTelegraphSpec spec) { }
        public void HideImmediate() { IsVisible = false; }
        public void Release() { IsVisible = false; }
    }
    public Handle Last;
    public bool HasActiveTelegraph => Last != null && Last.IsVisible;
    public void Show(AttackTelegraphSpec spec) { }
    public void UpdateCurrentGeometry(AttackTelegraphSpec spec) { }
    public void HideCurrent() { }
    public void ClearAll() { Last?.HideImmediate(); }
    public IAttackTelegraphHandle SpawnDetachedView(AttackTelegraphSpec spec, Transform parent = null)
    { Last = new Handle(); return Last; }
}

public static class DragonJumpBreathRegression
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run()
    {
        GameObject host = null;
        AbilityLogic_DragonFireBreath breath = null;
        AbilityLogic_DragonAbsorbPuddles absorb = null;
        AbilityDefinition definition = null;
        try
        {
            host = new GameObject("Dragon sequence fixture"); host.SetActive(false);
            var system = host.AddComponent<AbilitySystem>();
            var height = host.AddComponent<CombatHeightState2D>();
            var presenter = host.AddComponent<DragonJumpTestPresenter>();
            var dragon = host.AddComponent<DragonController>();
            host.SetActive(true);
            breath = ScriptableObject.CreateInstance<AbilityLogic_DragonFireBreath>();
            absorb = ScriptableObject.CreateInstance<AbilityLogic_DragonAbsorbPuddles>();
            definition = ScriptableObject.CreateInstance<AbilityDefinition>();
            var spec = new AbilitySpec(definition);
            var token = new AbilityCancellationToken();
            typeof(AbilitySpec).GetProperty("Token").SetValue(spec, token);

            IEnumerator routine = breath.Activate(system, spec, null);
            Check(routine.MoveNext() && routine.Current is IEnumerator, "Breath must yield its jump first");
            IEnumerator jump = (IEnumerator)routine.Current;
            Check(jump.GetType().Name.Contains("MoveToArenaCenter"), "First phase is not center jump");
            Check(jump.MoveNext() && height.IsAirborne && presenter.HasActiveTelegraph, "Jump did not start height/warning");
            token.Cancel();
            Check(!jump.MoveNext(), "Cancelled jump continued");
            Check(height.IsGrounded && !presenter.HasActiveTelegraph, "Cancelled jump leaked height/warning");
            Check((int)typeof(DragonController).GetField("faceTargetLockCount", Flags).GetValue(dragon) == 0, "Jump leaked facing lock");
            Check(!routine.MoveNext(), "Cancelled jump proceeded into breath");

            // Traverse the parent schedule without executing each fire phase.
            routine = breath.Activate(system, null, null);
            Check(routine.MoveNext() && routine.Current.GetType().Name.Contains("MoveToArenaCenter"), "Missing initial jump");
            for (int i = 0; i < 3; i++)
                Check(routine.MoveNext() && routine.Current.GetType().Name.Contains("RunFireBreathSequence"), "Jump repeated or breath count/order changed");
            Check(!routine.MoveNext(), "Unexpected extra phase");
            routine = absorb.Activate(system, null, null);
            Check(routine.MoveNext() && routine.Current.GetType().Name.Contains("RunAbsorb"), "Absorb still jumps before inhaling");
            ((IDisposable)routine).Dispose();
            Check((int)typeof(DragonController).GetField("faceTargetLockCount", Flags).GetValue(dragon) == 0, "Absorb cleanup leaked lock");
            Debug.Log("DRAGON_JUMP_BREATH_PASS: jump once before three breaths, absorb without jump, cancelled jump stops chain and cleans height/warning/facing.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        finally
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (breath != null) UnityEngine.Object.DestroyImmediate(breath);
            if (absorb != null) UnityEngine.Object.DestroyImmediate(absorb);
            if (definition != null) UnityEngine.Object.DestroyImmediate(definition);
        }
    }
}

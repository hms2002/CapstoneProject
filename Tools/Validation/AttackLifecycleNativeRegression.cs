// Isolated Unity probe. Supply current Core/Gameplay DLLs and their referenced runtime DLLs.
// -batchmode -nographics -executeMethod AttackLifecycleNativeRegression.Run
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityGAS;

[InitializeOnLoad]
public static class AttackLifecycleNativeRegression
{
    private const string Pending = "Capstone.AttackLifecycleProbe";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static AttackLifecycleNativeRegression() => EditorApplication.playModeStateChanged += OnPlayMode;
    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    private static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        var actor = new GameObject("Attack lifecycle probe");
        actor.AddComponent<AttributeSet>();
        var system = actor.AddComponent<AbilitySystem>();
        var input = actor.AddComponent<PlayerCombatInput2D>();
        system.StartCoroutine(Guard(Exercise(system, input)));
    }
    private static IEnumerator Guard(IEnumerator test)
    {
        while (true)
        {
            object next;
            try
            {
                if (!test.MoveNext()) break;
                next = test.Current;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                yield break;
            }
            yield return next;
        }
        Debug.Log("ATTACK_LIFECYCLE_REGRESSION_PASS: real execution, walking intent, no Animator, cancellation, buffer request/start, natural completion, animation tail.");
        EditorApplication.Exit(0);
    }
    private static AbilityDefinition Definition(out AttackLifecycleProbeLogic logic)
    {
        logic = ScriptableObject.CreateInstance<AttackLifecycleProbeLogic>();
        var definition = ScriptableObject.CreateInstance<AbilityDefinition>();
        definition.logic = logic;
        definition.recoveryTime = 0f;
        return definition;
    }
    private static IEnumerator Exercise(AbilitySystem system, PlayerCombatInput2D input)
    {
        var attack = Definition(out var attackLogic);
        var movement = system.gameObject.AddComponent<PlayerIntentInput2D>();
        system.GiveAbility(attack);
        typeof(PlayerCombatInput2D).GetMethod("RememberBasicAttack", Private)
            .Invoke(input, new object[] { WeaponAbilitySlot.Attack, attack });
        Check(system.TryActivateAbility(attack), "Attack activation failed.");
        for (int i = 0; i < 20; i++)
        {
            yield return null;
            Check(system.IsExecuting && input.IsBasicAttackMovementLocked,
                "Walking unlocked while the real attack still executed.");
            Set(movement, "<MoveInput>k__BackingField", Vector2.up);
            Check(movement.GetIntent().Direction == Vector2.zero, "Walking intent bypassed the active attack.");
        }
        system.CancelExecution(true);
        for (int i = 0; i < 30 && system.IsExecuting; i++) yield return null;
        yield return null;
        Check(!input.IsBasicAttackMovementLocked, "Cancelled attack retained its lock.");

        var blocker = Definition(out var blockerLogic);
        system.GiveAbility(blocker);
        Set(system, "enableExclusiveActivationBuffer", true);
        Check(system.TryActivateAbility(blocker), "Blocker activation failed.");
        yield return null;
        Check(system.TryActivateAbility(attack), "Attack buffer request failed.");
        Check(!input.IsBasicAttackMovementLocked, "Buffer-only request locked walking.");
        blockerLogic.Complete = true;
        for (int i = 0; i < 30 && system.CurrentExecSpec?.Definition != attack; i++) yield return null;
        Check(system.CurrentExecSpec?.Definition == attack && input.IsBasicAttackMovementLocked,
            "Buffered attack executed without movement lock.");
        attackLogic.Complete = true;
        for (int i = 0; i < 30 && system.IsExecuting; i++) yield return null;
        yield return null;
        Check(!input.IsBasicAttackMovementLocked, "Completed attack retained its lock.");
        Set(movement, "<MoveInput>k__BackingField", Vector2.up);
        Check(movement.GetIntent().Direction == Vector2.up, "Walking did not resume after attack completion.");

        // Observe the actual Animator, including a Weapon-channel request falling back to Player.
        var animatedActor = new GameObject("Animation tail probe");
        animatedActor.AddComponent<AttributeSet>();
        var animator = animatedActor.AddComponent<Animator>();
        var controller = new AnimatorController();
        controller.AddLayer("Base Layer");
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "localScale.x", AnimationCurve.Linear(0f, 1f, 1f, 1f));
        var machine = controller.layers[0].stateMachine;
        var idle = machine.AddState("Idle");
        idle.motion = clip;
        machine.AddState("Attack").motion = clip;
        machine.defaultState = idle;
        animator.runtimeAnimatorController = controller;
        animator.Play("Idle");
        animator.Update(0f);
        var animatedSystem = animatedActor.AddComponent<AbilitySystem>();
        var animatedInput = animatedActor.AddComponent<PlayerCombatInput2D>();
        var animatedAttack = Definition(out var animatedLogic);
        animatedAttack.animationChannel = AbilityDefinition.AnimationChannel.Weapon;
        animatedSystem.GiveAbility(animatedAttack);
        typeof(PlayerCombatInput2D).GetMethod("RememberBasicAttack", Private)
            .Invoke(animatedInput, new object[] { WeaponAbilitySlot.Attack, animatedAttack });
        Check(animatedSystem.GetAnimationTarget(animatedAttack) == animator, "Animator fallback diverged from playback.");
        Check(animatedSystem.TryActivateAbility(animatedAttack), "Animated attack activation failed.");
        animator.Play("Attack", 0, 0.2f);
        animator.Update(0f);
        yield return null;
        animatedLogic.Complete = true;
        for (int i = 0; i < 30 && animatedSystem.IsExecuting; i++) yield return null;
        animator.Play("Attack", 0, 0.9f);
        animator.Update(0f);
        Set(animatedInput, "meleeTickFrame", -1);
        Check(animatedInput.IsBasicAttackMovementLocked, "Animation tail unlocked at the old 80% threshold.");
        animator.Play("Attack", 0, 1.01f);
        animator.Update(0f);
        Set(animatedInput, "meleeTickFrame", -1);
        Check(!animatedInput.IsBasicAttackMovementLocked, "Finished animation retained its lock.");
        UnityEngine.Object.Destroy(animatedActor);
        UnityEngine.Object.Destroy(controller);
        UnityEngine.Object.Destroy(clip);
        UnityEngine.Object.Destroy(animatedAttack);
        UnityEngine.Object.Destroy(animatedLogic);
        UnityEngine.Object.Destroy(system.gameObject);
        UnityEngine.Object.Destroy(attack);
        UnityEngine.Object.Destroy(attackLogic);
        UnityEngine.Object.Destroy(blocker);
        UnityEngine.Object.Destroy(blockerLogic);
    }
}

public sealed class AttackLifecycleProbeLogic : AbilityLogic
{
    public bool Complete;
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        while (!Complete && spec.Token != null && !spec.Token.IsCancelled) yield return null;
    }
}

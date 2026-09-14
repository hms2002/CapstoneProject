using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>Unscales pure VFX during combat slow motion; full pause and pooled cleanup retain ownership.</summary>
[DisallowMultipleComponent]
public sealed class CombatPresentationClock2D : MonoBehaviour
{
    private readonly Dictionary<ParticleSystem, (bool Unscaled, float Speed)> particles = new();
    private readonly Dictionary<Animator, AnimatorUpdateMode> animators = new();
    private bool activeOverride;

    public bool HasGameplayTiming => GetComponentInChildren<ITimedHitEffect2D>() != null ||
        GetComponentInChildren<IAttackCollisionSource2D>() != null ||
        GetComponentInChildren<AbilitySystem>() != null;

    public static void Attach(GameObject root)
    {
        if (root == null) return;
        if (!root.TryGetComponent<CombatPresentationClock2D>(out var clock))
            clock = root.AddComponent<CombatPresentationClock2D>();
        clock.LateUpdate();
    }

    private void Update() => LateUpdate();

    private void LateUpdate()
    {
        if (!TimeScalePausePlayback.IsCombatSlowMotion)
        {
            Restore();
            return;
        }
        if (!activeOverride)
        {
            foreach (var particle in GetComponentsInChildren<ParticleSystem>())
            {
                if (particle.collision.enabled || particle.trigger.enabled) continue;
                particles[particle] = (particle.main.useUnscaledTime, particle.main.simulationSpeed);
            }
            // Animation Events on attack visuals can own damage windows; never accelerate those.
            if (!HasGameplayTiming)
                foreach (var animator in GetComponentsInChildren<Animator>())
                    animators[animator] = animator.updateMode;
            activeOverride = true;
        }
        foreach (var pair in particles)
        {
            if (pair.Key == null) continue;
            var main = pair.Key.main;
            main.useUnscaledTime = true;
            main.simulationSpeed = TimeScalePausePlayback.IsPaused ? 0f : pair.Value.Speed;
        }
        foreach (var pair in animators)
            if (pair.Key != null)
                pair.Key.updateMode = TimeScalePausePlayback.IsPaused
                    ? AnimatorUpdateMode.Normal : AnimatorUpdateMode.UnscaledTime;
    }

    private void OnDisable() => Restore();

    private void Restore()
    {
        if (!activeOverride) return;
        foreach (var pair in particles)
        {
            if (pair.Key == null) continue;
            var main = pair.Key.main;
            main.useUnscaledTime = pair.Value.Unscaled;
            main.simulationSpeed = pair.Value.Speed;
        }
        foreach (var pair in animators) if (pair.Key != null) pair.Key.updateMode = pair.Value;
        particles.Clear();
        animators.Clear();
        activeOverride = false;
    }
}

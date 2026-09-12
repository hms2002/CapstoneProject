using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    public interface ICombatHitPauseImmune { }

    [Serializable]
    public struct CombatHitFeelTiming
    {
        [Min(0f)] public float targetStunSeconds;
        [Min(0f)] public float attackerStopSeconds;
    }

    /// <summary>Owns local target stun and a separately timed global hitstop request.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatHitPause2D : MonoBehaviour
    {
        private float until;
        private float worldPauseUntil;
        private bool ownsWorldPause;
        private readonly Dictionary<Animator, float> animationSpeeds = new();
        public bool IsPaused => isActiveAndEnabled && Time.time < until;

        public static bool IsPausedOn(GameObject owner) => TimeScalePausePlayback.IsPaused ||
            (owner != null && owner.TryGetComponent<CombatHitPause2D>(out var pause) && pause.IsPaused);

        public static void ApplyWorldPause(GameObject owner, float seconds)
        {
            if (owner == null || !owner.activeInHierarchy || !float.IsFinite(seconds) || seconds <= 0f) return;
            var pause = owner.GetComponent<CombatHitPause2D>();
            if (pause == null) pause = owner.AddComponent<CombatHitPause2D>();
            if (!pause.isActiveAndEnabled) return;
            if (!pause.ownsWorldPause)
                pause.ownsWorldPause = TimeScalePausePlayback.IsHeldBy(pause) || TimeScalePausePlayback.Acquire(pause);
            if (pause.ownsWorldPause)
                pause.worldPauseUntil = Mathf.Max(pause.worldPauseUntil, Time.unscaledTime + seconds);
        }

        private void ReleaseWorldPause()
        {
            if (!ownsWorldPause) return;
            TimeScalePausePlayback.Release(this);
            ownsWorldPause = false;
            worldPauseUntil = 0f;
        }

        public static void Apply(GameObject owner, float seconds)
        {
            if (owner == null || !owner.activeInHierarchy || !float.IsFinite(seconds) || seconds <= 0f) return;
            if (owner.GetComponentInParent<ICombatHitPauseImmune>() != null) return;
            var pause = owner.GetComponent<CombatHitPause2D>();
            if (pause == null) pause = owner.AddComponent<CombatHitPause2D>();
            pause.Begin(seconds);
        }

        public static float GetUnpausedAnimatorSpeed(Animator animator)
        {
            if (animator == null) return 1f;
            var pause = animator.GetComponentInParent<CombatHitPause2D>();
            return pause != null && pause.animationSpeeds.TryGetValue(animator, out float speed)
                ? speed : animator.speed;
        }

        private void Begin(float seconds)
        {
            if (!IsPaused) RestoreAnimations();
            until = Mathf.Max(until, Time.time + seconds);
            foreach (Animator animator in GetComponentsInChildren<Animator>())
            {
                if (!animationSpeeds.ContainsKey(animator)) animationSpeeds.Add(animator, animator.speed);
                animator.speed = 0f;
            }
            GetComponent<MovementMotor2D>()?.StopAllMotion(clearExternal: false, clearMotion: false);
        }

        private void LateUpdate()
        {
            if (ownsWorldPause && Time.unscaledTime >= worldPauseUntil) ReleaseWorldPause();
            if (GetComponentInParent<ICombatHitPauseImmune>() != null) until = 0f;
            if (!IsPaused) { RestoreAnimations(); return; }
            foreach (var entry in animationSpeeds)
                if (entry.Key != null) entry.Key.speed = 0f;
        }

        private void OnDisable() { ReleaseWorldPause(); until = 0f; RestoreAnimations(); }
        private void RestoreAnimations()
        {
            foreach (var entry in animationSpeeds)
                if (entry.Key != null) entry.Key.speed = entry.Value;
            animationSpeeds.Clear();
        }

        // Explicit nested iteration keeps ability logic paused and disposes every finally on cancellation.
        public static IEnumerator Run(AbilitySystem system, AbilitySpec spec, IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            try
            {
                while (stack.Count > 0)
                {
                    if (system == null || (spec.Token != null && spec.Token.IsCancelled)) yield break;
                    if (IsPausedOn(system.gameObject)) { yield return null; continue; }
                    IEnumerator current = stack.Peek();
                    if (!current.MoveNext())
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested) stack.Push(nested);
                    else yield return current.Current;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            }
        }
    }
}

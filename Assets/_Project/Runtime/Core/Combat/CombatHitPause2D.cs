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

    /// <summary>Owns local stun, real-time actor impact freeze and a separately owned world slowdown.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatHitPause2D : MonoBehaviour
    {
        private float until;
        private float worldPauseUntil;
        private bool ownsWorldPause;
        private float impactRemaining;
        private Rigidbody2D impactBody;
        private RigidbodyConstraints2D impactConstraints;
        private bool holdsBody;
        private readonly Dictionary<Animator, float> animationSpeeds = new();
        public bool IsPaused => isActiveAndEnabled && (Time.time < until || impactRemaining > 0f);

        public static bool IsPausedOn(GameObject owner) => TimeScalePausePlayback.IsPaused ||
            (owner != null && owner.TryGetComponent<CombatHitPause2D>(out var pause) && pause.IsPaused);

        public static void ApplyWorldPause(GameObject owner, float seconds)
        {
            if (owner == null || !owner.activeInHierarchy || !float.IsFinite(seconds) || seconds <= 0f) return;
            var pause = owner.GetComponent<CombatHitPause2D>();
            if (pause == null) pause = owner.AddComponent<CombatHitPause2D>();
            if (!pause.isActiveAndEnabled) return;
            if (!pause.ownsWorldPause)
                pause.ownsWorldPause = TimeScalePausePlayback.IsHeldBy(pause) || TimeScalePausePlayback.AcquireCombatSlowMotion(pause);
            if (pause.ownsWorldPause)
                pause.worldPauseUntil = Mathf.Max(pause.worldPauseUntil, seconds);
            pause.BeginImpact(seconds);
        }

        public static void ApplyImpact(GameObject attacker, GameObject victim, float seconds, Vector3 direction, CameraShakeRequest? cameraOverride = null)
        {
            ApplyWorldPause(attacker, seconds);
            FreezeVictim(victim, seconds);
            if (cameraOverride.HasValue)
            {
                CameraShakePlayback.Play(cameraOverride.Value);
                return;
            }
            // Prototype strong-hit tier follows the authored impact duration.
            bool strongImpact = seconds >= 0.09f;
            // Activation grouping already deduplicates impact; prior cast/hit shakes must not suppress it.
            CameraShakePlayback.Play(new CameraShakeRequest(1f, direction, attacker, strongImpact ? 0f : 0.04f,
                "CombatImpact", hasManualShakeSettingsOverride: true,
                manualShakeSettingsOverride: CameraManualShakeSettings.Create(
                    strongImpact ? 0.18f : 0.08f, strongImpact ? 0.12f : 0.045f, 32f, 0.3f),
                punchDistance: strongImpact ? 1.8f : 0.08f, punchSeconds: strongImpact ? 0.12f : 0.035f));
        }

        public static void FreezeVictim(GameObject victim, float seconds)
        {
            if (victim == null || !victim.activeInHierarchy || !float.IsFinite(seconds) || seconds <= 0f) return;
            var pause = victim.GetComponent<CombatHitPause2D>();
            if (pause == null) pause = victim.AddComponent<CombatHitPause2D>();
            pause.BeginImpact(seconds);
        }

        private void BeginImpact(float seconds)
        {
            if (!isActiveAndEnabled) return;
            if (!IsPaused) RestoreAnimations();
            impactRemaining = Mathf.Max(impactRemaining, seconds);
            CaptureAnimations();
            if (!holdsBody && TryGetComponent(out impactBody))
            {
                impactConstraints = impactBody.constraints;
                impactBody.constraints = RigidbodyConstraints2D.FreezeAll;
                holdsBody = true;
            }
            GetComponent<MovementMotor2D>()?.StopAllMotion(clearExternal: false, clearMotion: false);
        }

        private void CaptureAnimations()
        {
            foreach (Animator animator in GetComponentsInChildren<Animator>())
            {
                var visualClock = animator.GetComponentInParent<CombatPresentationClock2D>();
                if (visualClock != null && !visualClock.HasGameplayTiming) continue;
                if (!animationSpeeds.ContainsKey(animator)) animationSpeeds.Add(animator, animator.speed);
                animator.speed = 0f;
            }
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
            CaptureAnimations();
            GetComponent<MovementMotor2D>()?.StopAllMotion(clearExternal: false, clearMotion: false);
        }

        private void LateUpdate()
        {
            float delta = TimeScalePausePlayback.IsPaused ? 0f : Time.unscaledDeltaTime;
            impactRemaining = Mathf.Max(0f, impactRemaining - delta);
            if (ownsWorldPause)
            {
                worldPauseUntil -= delta;
                if (worldPauseUntil <= 0f) ReleaseWorldPause();
            }
            if (GetComponentInParent<ICombatHitPauseImmune>() != null) until = 0f;
            if (!IsPaused) { RestoreAnimations(); return; }
            foreach (var entry in animationSpeeds)
                if (entry.Key != null) entry.Key.speed = 0f;
        }

        private void OnDisable() { ReleaseWorldPause(); until = 0f; impactRemaining = 0f; RestoreAnimations(); }
        private void RestoreAnimations()
        {
            if (holdsBody && impactBody != null) impactBody.constraints = impactConstraints;
            holdsBody = false;
            foreach (var entry in animationSpeeds)
                if (entry.Key != null) entry.Key.speed = entry.Value;
            animationSpeeds.Clear();
        }

        /// <summary>Only for unscaled presentation that must finish to release its own pause.</summary>
        public static IEnumerator RunUnpausedPresentation(IEnumerator routine) => new UnpausedPresentation(routine);

        private sealed class UnpausedPresentation : IEnumerator, IDisposable
        {
            private readonly IEnumerator routine;
            public UnpausedPresentation(IEnumerator routine) => this.routine = routine;
            public object Current => routine.Current;
            public bool MoveNext() => routine.MoveNext();
            public void Reset() => throw new NotSupportedException();
            public void Dispose() => (routine as IDisposable)?.Dispose();
        }

        // Explicit nested iteration keeps ability logic paused and disposes every finally on cancellation.
        public static IEnumerator Run(AbilitySystem system, AbilitySpec spec, IEnumerator routine)
        {
            var stack = new Stack<(IEnumerator Routine, bool Unpaused)>();
            stack.Push((routine, routine is UnpausedPresentation));
            try
            {
                while (stack.Count > 0)
                {
                    if (system == null || (spec.Token != null && spec.Token.IsCancelled)) yield break;
                    var frame = stack.Peek();
                    if (!frame.Unpaused && IsPausedOn(system.gameObject)) { yield return null; continue; }
                    IEnumerator current = frame.Routine;
                    if (!current.MoveNext())
                    {
                        (stack.Pop().Routine as IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator nested)
                        stack.Push((nested, frame.Unpaused || nested is UnpausedPresentation));
                    else yield return current.Current;
                }
            }
            finally
            {
                while (stack.Count > 0) (stack.Pop().Routine as IDisposable)?.Dispose();
            }
        }
    }
}

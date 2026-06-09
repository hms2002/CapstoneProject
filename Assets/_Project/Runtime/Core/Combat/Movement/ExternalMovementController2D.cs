using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 외부 이동(넉백, 끌기, 바람, 플랫폼 이동 등)을 관리한다.
    /// - 최신 넉백만 유지한다
    /// - 넉백 우세 시간(dominance)을 관리한다
    /// - 지속 외압은 여러 개 공존 가능하다
    /// - Rigidbody2D는 직접 만지지 않는다
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExternalMovementController2D : MonoBehaviour
    {
        [Serializable]
        private struct TimedExternalVelocity
        {
            public UnityEngine.Object source;
            public Vector2 velocity;
            public float remainingTime;
            public float damping;
        }

        [Header("Knockback")]
        [SerializeField] private float knockbackDamping = 18f;
        [SerializeField] private float defaultKnockbackDominanceTime = 0.12f;
        [SerializeField] private float maxKnockbackMagnitude = 25f;

        [Header("Persistent External Velocity")]
        [SerializeField] private float maxCombinedPersistentVelocityMagnitude = 25f;

        private Vector2 activeKnockbackVelocity;
        private float knockbackDominanceRemaining;

        private readonly List<TimedExternalVelocity> timedVelocities = new();

        public bool HasKnockbackDominance => knockbackDominanceRemaining > 0f;
        public bool HasActiveKnockback => activeKnockbackVelocity.sqrMagnitude > 0.000001f;

        /// <summary>
        /// 최신 넉백만 적용한다.
        /// 기존 넉백은 덮어쓴다.
        /// </summary>
        public void ApplyKnockback(Vector2 knockbackVelocity, float dominanceTime = -1f)
        {
            float mag = knockbackVelocity.magnitude;
            if (mag > maxKnockbackMagnitude)
                knockbackVelocity = knockbackVelocity / mag * maxKnockbackMagnitude;

            activeKnockbackVelocity = knockbackVelocity;
            knockbackDominanceRemaining = dominanceTime > 0f
                ? dominanceTime
                : defaultKnockbackDominanceTime;
        }

        /// <summary>
        /// 일정 시간 유지되는 외압 속도를 추가한다.
        /// 예: 바람, 컨베이어, 지속 당김
        /// </summary>
        public void AddTimedVelocity(
            Vector2 velocity,
            float duration,
            float damping = 0f,
            UnityEngine.Object source = null)
        {
            if (duration <= 0f)
                return;

            timedVelocities.Add(new TimedExternalVelocity
            {
                source = source,
                velocity = velocity,
                remainingTime = duration,
                damping = Mathf.Max(0f, damping)
            });

            ClampCombinedTimedVelocity();
        }

        public void RemoveTimedVelocitiesFromSource(UnityEngine.Object source)
        {
            if (source == null)
                return;

            for (int i = timedVelocities.Count - 1; i >= 0; i--)
            {
                if (timedVelocities[i].source == source)
                    timedVelocities.RemoveAt(i);
            }
        }

        public Vector2 GetCurrentExternalVelocity()
        {
            Vector2 sum = activeKnockbackVelocity;

            for (int i = 0; i < timedVelocities.Count; i++)
                sum += timedVelocities[i].velocity;

            return sum;
        }

        public void ClearAll()
        {
            activeKnockbackVelocity = Vector2.zero;
            knockbackDominanceRemaining = 0f;
            timedVelocities.Clear();
        }

        public void ClearKnockbackOnly()
        {
            activeKnockbackVelocity = Vector2.zero;
            knockbackDominanceRemaining = 0f;
        }

        public void Tick(float dt)
        {
            TickKnockback(dt);
            TickTimedVelocities(dt);
        }

        private void TickKnockback(float dt)
        {
            if (dt <= 0f)
                return;

            if (knockbackDominanceRemaining > 0f)
                knockbackDominanceRemaining -= dt;

            if (activeKnockbackVelocity.sqrMagnitude > 0.000001f)
            {
                activeKnockbackVelocity = Vector2.Lerp(
                    activeKnockbackVelocity,
                    Vector2.zero,
                    knockbackDamping * dt);

                if (activeKnockbackVelocity.sqrMagnitude <= 0.000001f)
                    activeKnockbackVelocity = Vector2.zero;
            }

            if (knockbackDominanceRemaining < 0f)
                knockbackDominanceRemaining = 0f;
        }

        private void TickTimedVelocities(float dt)
        {
            for (int i = timedVelocities.Count - 1; i >= 0; i--)
            {
                TimedExternalVelocity item = timedVelocities[i];
                item.remainingTime -= dt;

                if (item.damping > 0f)
                {
                    item.velocity = Vector2.Lerp(item.velocity, Vector2.zero, item.damping * dt);
                }

                if (item.remainingTime <= 0f || item.velocity.sqrMagnitude <= 0.000001f)
                {
                    timedVelocities.RemoveAt(i);
                    continue;
                }

                timedVelocities[i] = item;
            }
        }

        private void ClampCombinedTimedVelocity()
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < timedVelocities.Count; i++)
                sum += timedVelocities[i].velocity;

            float mag = sum.magnitude;
            if (mag <= maxCombinedPersistentVelocityMagnitude || mag <= 0.000001f)
                return;

            float scale = maxCombinedPersistentVelocityMagnitude / mag;
            for (int i = 0; i < timedVelocities.Count; i++)
            {
                var item = timedVelocities[i];
                item.velocity *= scale;
                timedVelocities[i] = item;
            }
        }
    }
}
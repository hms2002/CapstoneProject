using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 능력 전용 파티클 프리팹을 follow target 주변에 붙여 재생하고, 이동 방향에 맞춰 월드 회전을 갱신한다.
    /// - emission multiplier 조정과 정지/강제 clear를 공통 API로 제공해 Rush 같은 지속 연출이 로직 타이밍만 관리하게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MotionAlignedParticleVisual2D : MonoBehaviour
    {
        /// <summary>
        /// 책임 :
        /// - 개별 ParticleSystem이 authoring 단계에서 가진 원본 emission multiplier를 보관한다.
        /// - 런타임 multiplier 변경 시 원본 값에 비례해 안전하게 조정할 수 있게 한다.
        /// </summary>
        private sealed class EmissionBaseline
        {
            public ParticleSystem System;
            public float RateOverTimeMultiplier;
            public float RateOverDistanceMultiplier;
        }

        private readonly List<EmissionBaseline> emissionBaselines = new();

        private Transform followTarget;
        private MovementMotor2D movementMotor;
        private GameObject currentPrefab;
        private GameObject instanceRoot;
        private Vector3 localOffset;
        private float angleOffset;
        private bool alignToMovementDirection = true;

        /// <summary>
        /// 책임 :
        /// - 지정한 파티클 프리팹을 현재 follow target 문맥에 맞게 생성 또는 재사용한다.
        /// - target, offset, 정렬 규칙이 바뀌어도 같은 visual을 갱신하며 재생 상태로 전환한다.
        /// </summary>
        public void Begin(
            GameObject particlePrefab,
            Transform followTarget,
            MovementMotor2D movementMotor,
            Vector3 localOffset,
            float angleOffset,
            bool alignToMovementDirection)
        {
            this.followTarget = followTarget != null ? followTarget : transform;
            this.movementMotor = movementMotor;
            this.localOffset = localOffset;
            this.angleOffset = angleOffset;
            this.alignToMovementDirection = alignToMovementDirection;

            EnsureInstance(particlePrefab);
            ApplyPlacement();
            PlayAll();
        }

        /// <summary>
        /// 책임 :
        /// - 파티클 프리팹이 가진 원본 emission 값을 보존한 채 stage별 강도 multiplier만 덮어쓴다.
        /// - 같은 프리팹을 유지하면서 Rush 단계 상승에 따라 바람 강도를 높이는 용도로 사용한다.
        /// </summary>
        public void SetEmissionMultiplier(float multiplier)
        {
            float clampedMultiplier = Mathf.Max(0f, multiplier);

            for (int i = 0; i < emissionBaselines.Count; i++)
            {
                EmissionBaseline baseline = emissionBaselines[i];
                if (baseline == null || baseline.System == null)
                    continue;

                var emission = baseline.System.emission;
                emission.rateOverTimeMultiplier = baseline.RateOverTimeMultiplier * clampedMultiplier;
                emission.rateOverDistanceMultiplier = baseline.RateOverDistanceMultiplier * clampedMultiplier;
            }
        }

        /// <summary>
        /// 책임 :
        /// - 더 이상 새 파티클이 나오지 않도록 emission만 멈춘다.
        /// - clearParticles=false면 이미 나온 입자는 자연스럽게 사라지게 두고, true면 즉시 비운다.
        /// </summary>
        public void StopEmission(bool clearParticles)
        {
            if (instanceRoot == null)
                return;

            ParticleSystem[] particleSystems = instanceRoot.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            ParticleSystemStopBehavior stopBehavior = clearParticles
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;

            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                particleSystem.Stop(withChildren: true, stopBehavior);
            }
        }

        private void LateUpdate()
        {
            ApplyPlacement();
        }

        private void OnDestroy()
        {
            if (instanceRoot != null)
                Destroy(instanceRoot);

            instanceRoot = null;
            currentPrefab = null;
            emissionBaselines.Clear();
        }

        private void EnsureInstance(GameObject particlePrefab)
        {
            if (particlePrefab == null)
                return;

            if (instanceRoot != null && currentPrefab == particlePrefab)
                return;

            if (instanceRoot != null)
                Destroy(instanceRoot);

            currentPrefab = particlePrefab;
            instanceRoot = Instantiate(particlePrefab);
            instanceRoot.name = $"{particlePrefab.name}_Runtime";

            CacheEmissionBaselines();
        }

        private void CacheEmissionBaselines()
        {
            emissionBaselines.Clear();

            if (instanceRoot == null)
                return;

            ParticleSystem[] particleSystems = instanceRoot.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                var emission = particleSystem.emission;
                emissionBaselines.Add(new EmissionBaseline
                {
                    System = particleSystem,
                    RateOverTimeMultiplier = emission.rateOverTimeMultiplier,
                    RateOverDistanceMultiplier = emission.rateOverDistanceMultiplier
                });
            }
        }

        private void PlayAll()
        {
            if (instanceRoot == null)
                return;

            ParticleSystem[] particleSystems = instanceRoot.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem particleSystem = particleSystems[i];
                if (particleSystem == null)
                    continue;

                if (!particleSystem.isPlaying)
                    particleSystem.Play(withChildren: true);
            }
        }

        private void ApplyPlacement()
        {
            if (instanceRoot == null || followTarget == null)
                return;

            float worldAngle = angleOffset;
            Vector3 worldOffset = localOffset;

            if (alignToMovementDirection && movementMotor != null)
            {
                Vector2 velocity = movementMotor.LastFinalVelocity;
                if (velocity.sqrMagnitude > 0.0001f)
                {
                    worldAngle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg + angleOffset;
                    worldOffset = Quaternion.Euler(0f, 0f, worldAngle) * localOffset;
                }
            }

            instanceRoot.transform.SetPositionAndRotation(
                followTarget.position + worldOffset,
                Quaternion.Euler(0f, 0f, worldAngle));
        }
    }
}

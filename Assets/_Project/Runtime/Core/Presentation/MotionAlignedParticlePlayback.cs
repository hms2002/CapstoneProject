using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 이동 방향에 맞춰 정렬되는 능력 전용 파티클 visual이 제공해야 하는 최소 조작 API를 정의한다.
    /// - Gameplay ability 로직이 concrete ParticleSystem presentation component를 직접 참조하지 않게 한다.
    /// </summary>
    public interface IMotionAlignedParticleVisual2D
    {
        void Begin(
            GameObject particlePrefab,
            Transform followTarget,
            MovementMotor2D movementMotor,
            Vector3 localOffset,
            float angleOffset,
            bool alignToMovementDirection);

        void SetEmissionMultiplier(float multiplier);
        void StopEmission(bool clearParticles);
    }

    /// <summary>
    /// 책임 :
    /// - Ability spec 수명에 묶이는 이동 정렬 파티클 visual의 Core 요청 진입점을 제공한다.
    /// - concrete visual component 생성/조회는 Presentation backend로 위임한다.
    /// </summary>
    public static class MotionAlignedParticlePlayback
    {
        private static IMotionAlignedParticleBackend backend = NullMotionAlignedParticleBackend.Instance;

        public static void RegisterBackend(IMotionAlignedParticleBackend newBackend)
        {
            backend = newBackend ?? NullMotionAlignedParticleBackend.Instance;
        }

        public static IMotionAlignedParticleVisual2D GetOwned(AbilityVisualRouter router, AbilitySpec spec)
        {
            return backend.GetOwned(router, spec);
        }

        public static IMotionAlignedParticleVisual2D GetOrAddOwned(AbilityVisualRouter router, AbilitySpec spec)
        {
            return backend.GetOrAddOwned(router, spec);
        }

        /// <summary>
        /// 책임 :
        /// - Core 파티클 playback 요청을 Presentation visual component 생성/조회로 연결하는 backend 계약을 정의한다.
        /// - AbilityVisualRouter의 spec별 수명 등록을 concrete 구현 계층이 처리하게 한다.
        /// </summary>
        public interface IMotionAlignedParticleBackend
        {
            IMotionAlignedParticleVisual2D GetOwned(AbilityVisualRouter router, AbilitySpec spec);
            IMotionAlignedParticleVisual2D GetOrAddOwned(AbilityVisualRouter router, AbilitySpec spec);
        }

        private sealed class NullMotionAlignedParticleBackend : IMotionAlignedParticleBackend
        {
            public static readonly NullMotionAlignedParticleBackend Instance = new();

            public IMotionAlignedParticleVisual2D GetOwned(AbilityVisualRouter router, AbilitySpec spec) => null;
            public IMotionAlignedParticleVisual2D GetOrAddOwned(AbilityVisualRouter router, AbilitySpec spec) => null;
        }
    }
}

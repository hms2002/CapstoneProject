using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 잔상 emitter가 제공해야 하는 시작, 중지, 밀도 갱신, 강제 정리 API를 정의한다.
    /// - Gameplay ability/boss 로직이 concrete SpriteRenderer 복제 구현을 직접 참조하지 않게 한다.
    /// </summary>
    public interface IAfterimageEmitter2D
    {
        bool IsEmitting { get; }
        void Begin(Transform sourceRoot, float emissionInterval, float lifetime, Color tintColor);
        void SetEmissionInterval(float emissionInterval);
        void StopEmission();
        void ClearSpawnedGhosts();
    }

    /// <summary>
    /// 책임 :
    /// - Gameplay 코드가 잔상 emitter를 owner 또는 Ability spec 수명 단위로 요청하는 Core 진입점을 제공한다.
    /// - concrete emitter component 생성/조회는 Presentation backend에 위임해 Gameplay-to-Presentation 의존을 끊는다.
    /// </summary>
    public static class AfterimageEmitterPlayback
    {
        private static IAfterimageEmitterBackend backend = NullAfterimageEmitterBackend.Instance;

        public static void RegisterBackend(IAfterimageEmitterBackend newBackend)
        {
            backend = newBackend ?? NullAfterimageEmitterBackend.Instance;
        }

        public static IAfterimageEmitter2D Get(GameObject owner)
        {
            return backend.Get(owner);
        }

        public static IAfterimageEmitter2D GetOrAdd(GameObject owner)
        {
            return backend.GetOrAdd(owner);
        }

        public static IAfterimageEmitter2D GetOwned(AbilityVisualRouter router, AbilitySpec spec)
        {
            return backend.GetOwned(router, spec);
        }

        public static IAfterimageEmitter2D GetOrAddOwned(AbilityVisualRouter router, AbilitySpec spec)
        {
            return backend.GetOrAddOwned(router, spec);
        }

        /// <summary>
        /// 책임 :
        /// - Core 잔상 playback 요청을 실제 Presentation emitter 생성/조회로 연결하는 backend 계약을 정의한다.
        /// - AbilityVisualRouter의 spec별 수명 등록까지 concrete 구현 계층이 처리하게 한다.
        /// </summary>
        public interface IAfterimageEmitterBackend
        {
            IAfterimageEmitter2D Get(GameObject owner);
            IAfterimageEmitter2D GetOrAdd(GameObject owner);
            IAfterimageEmitter2D GetOwned(AbilityVisualRouter router, AbilitySpec spec);
            IAfterimageEmitter2D GetOrAddOwned(AbilityVisualRouter router, AbilitySpec spec);
        }

        private sealed class NullAfterimageEmitterBackend : IAfterimageEmitterBackend
        {
            public static readonly NullAfterimageEmitterBackend Instance = new();

            public IAfterimageEmitter2D Get(GameObject owner) => null;
            public IAfterimageEmitter2D GetOrAdd(GameObject owner) => null;
            public IAfterimageEmitter2D GetOwned(AbilityVisualRouter router, AbilitySpec spec) => null;
            public IAfterimageEmitter2D GetOrAddOwned(AbilityVisualRouter router, AbilitySpec spec) => null;
        }
    }
}

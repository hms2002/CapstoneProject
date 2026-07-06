using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임:
    /// - GameplayCueManager가 구체 cue prefab 타입을 알지 않고 커스텀 생성/반납/수명 정책을 위임하게 한다.
    /// - Presentation cue가 자체 풀링처럼 특수한 런타임 정책을 Core 계약으로 등록할 수 있게 한다.
    /// </summary>
    public interface IGameplayCuePrefabInstanceProvider
    {
        bool CanAcquire(GameObject cuePrefab);
        GameObject Acquire(GameObject cuePrefab);
        bool TryRelease(GameObject instance, GameplayCueNotify notify);
        bool ShouldSuppressAutoDestroy(GameplayCueNotify notify);
    }

    /// <summary>
    /// 책임: 등록된 cue prefab provider를 순서대로 조회해 Core cue 실행 경로의 커스텀 인스턴스 정책을 중계한다.
    /// </summary>
    public static class GameplayCuePrefabInstanceProviders
    {
        private static readonly List<IGameplayCuePrefabInstanceProvider> Providers = new();

        public static void Register(IGameplayCuePrefabInstanceProvider provider)
        {
            if (provider == null || Providers.Contains(provider))
                return;

            Providers.Add(provider);
        }

        public static bool TryAcquire(GameObject cuePrefab, out GameObject instance)
        {
            instance = null;
            if (cuePrefab == null)
                return false;

            for (int i = 0; i < Providers.Count; i++)
            {
                IGameplayCuePrefabInstanceProvider provider = Providers[i];
                if (provider == null || !provider.CanAcquire(cuePrefab))
                    continue;

                instance = provider.Acquire(cuePrefab);
                return instance != null;
            }

            return false;
        }

        public static bool TryRelease(GameObject instance, GameplayCueNotify notify)
        {
            if (instance == null)
                return false;

            for (int i = 0; i < Providers.Count; i++)
            {
                IGameplayCuePrefabInstanceProvider provider = Providers[i];
                if (provider != null && provider.TryRelease(instance, notify))
                    return true;
            }

            return false;
        }

        public static bool ShouldSuppressAutoDestroy(GameplayCueNotify notify)
        {
            if (notify == null)
                return false;

            for (int i = 0; i < Providers.Count; i++)
            {
                IGameplayCuePrefabInstanceProvider provider = Providers[i];
                if (provider != null && provider.ShouldSuppressAutoDestroy(notify))
                    return true;
            }

            return false;
        }
    }
}

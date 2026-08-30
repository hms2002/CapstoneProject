using System;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>게이지 시스템과 독립된 정수형 몬스터 상태를 UI에 투영하는 계약입니다.</summary>
    public interface IMonsterStackStatusSource
    {
        string StatusId { get; }
        int CurrentStacks { get; }
        int MaxStacks { get; }
        Color DisplayColor { get; }
        event Action StackChanged;
        event Action PulseRequested;
    }

    public interface IMonsterStackStatusViewBackend
    {
        void Attach(GameObject target, IMonsterStackStatusSource source);
        void Detach(GameObject target, IMonsterStackStatusSource source);
    }

    public static class MonsterStackStatusViewPlayback
    {
        private static IMonsterStackStatusViewBackend backend;

        public static void RegisterBackend(IMonsterStackStatusViewBackend value) => backend = value;

        public static void UnregisterBackend(IMonsterStackStatusViewBackend value)
        {
            if (ReferenceEquals(backend, value))
                backend = null;
        }

        public static void Attach(GameObject target, IMonsterStackStatusSource source)
        {
            backend?.Attach(target, source);
        }

        public static void Detach(GameObject target, IMonsterStackStatusSource source)
        {
            backend?.Detach(target, source);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => backend = null;
    }
}

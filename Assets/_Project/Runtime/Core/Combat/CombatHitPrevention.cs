using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>회피 후, 피해와 부가 효과를 적용하기 전에 대상 소유의 방어 판정을 실행한다.</summary>
    public static class CombatHitPrevention
    {
        private static readonly List<Registration> Registrations = new();

        public static IDisposable Register(GameObject target, Func<bool> tryPrevent)
        {
            var registration = new Registration(target, tryPrevent);
            Registrations.Add(registration);
            return registration;
        }

        public static bool TryPrevent(GameObject target)
        {
            if (target == null || !target.activeInHierarchy) return false;
            for (int i = Registrations.Count - 1; i >= 0; i--)
            {
                Registration registration = Registrations[i];
                if (registration.Target == target && registration.TryPrevent?.Invoke() == true)
                    return true;
            }
            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Registrations.Clear();

        private sealed class Registration : IDisposable
        {
            public readonly GameObject Target;
            public Func<bool> TryPrevent;

            public Registration(GameObject target, Func<bool> tryPrevent)
            {
                Target = target;
                TryPrevent = tryPrevent;
            }

            public void Dispose()
            {
                TryPrevent = null;
                Registrations.Remove(this);
            }
        }
    }
}

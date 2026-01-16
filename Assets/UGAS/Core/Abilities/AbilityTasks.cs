using System;
using System.Collections;
using UnityEngine;

namespace UnityGAS
{
    public static class AbilityTasks
    {
        public static IEnumerator WaitDelay(AbilitySystem system, AbilitySpec spec, float seconds)
        {
            if (seconds <= 0f) yield break;

            float end = Time.time + seconds;
            while (Time.time < end)
            {
                if (system == null || spec == null) yield break;
                if (spec.Token != null && spec.Token.IsCancelled) yield break;
                yield return null;
            }
        }

        /// <summary>
        /// 특정 태그 이벤트를 기다림. (Spec 소유 waiter 사용)
        /// timeout <= 0이면 무한 대기.
        /// predicate가 있으면 이벤트 데이터 필터링(조건 불만족이면 계속 대기).
        /// </summary>
        public static IEnumerator WaitGameplayEvent(
            AbilitySystem system,
            AbilitySpec spec,
            GameplayTag tag,
            Action<AbilityEventData> onReceived,
            float timeout = 0f,
            Func<AbilityEventData, bool> predicate = null)
        {
            if (system == null || spec == null || tag == null) yield break;

            float end = timeout > 0f ? (Time.time + timeout) : float.PositiveInfinity;

            while (Time.time < end)
            {
                if (spec.Token != null && spec.Token.IsCancelled) yield break;

                var waiter = system.WaitGameplayEvent(tag, spec);
                // waiter는 "tag만" 필터링하므로, predicate는 여기서 체크하고 실패하면 다시 대기
                while (!waiter.Done)
                {
                    if (spec.Token != null && spec.Token.IsCancelled)
                    {
                        waiter.Cancel();
                        yield break;
                    }
                    if (Time.time >= end)
                    {
                        waiter.Cancel();
                        yield break;
                    }
                    yield return null;
                }

                var data = waiter.Data;
                waiter.Cancel(); // 구독 정리

                if (predicate == null || predicate(data))
                {
                    onReceived?.Invoke(data);
                    yield break;
                }

                // predicate 실패면 다시 대기 (남은 시간 내에서)
                yield return null;
            }
        }
    }
}

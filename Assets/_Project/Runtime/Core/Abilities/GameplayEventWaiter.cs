using System;

namespace UnityGAS
{
    /// <summary>
    /// 코루틴에서 out 대신 쓰는 "대기 핸들".
    /// </summary>
    public sealed class GameplayEventWaiter
    {
        public bool Done { get; internal set; }
        public AbilityEventData Data { get; internal set; }

        // AbilitySystem이 등록/해제 관리용으로 쓰는 cleanup
        internal Action Cleanup { get; set; }

        /// <summary>대기를 강제로 종료(구독 해제 포함)</summary>
        public void Cancel()
        {
            if (Done) return;
            Done = true;
            Cleanup?.Invoke();
            Cleanup = null;
        }
    }
}

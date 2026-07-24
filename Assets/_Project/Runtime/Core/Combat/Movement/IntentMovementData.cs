using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 의도 이동 공급자가 만들어내는 이동 데이터.
    /// - Direction: 이동하고 싶은 방향
    /// - SpeedScale: 현재 이동속도 Attribute에 곱할 배율
    /// - Blocked: 이 공급자가 현재 의도 이동을 내지 않겠다는 명시적 신호
    /// </summary>
    public struct IntentMovementData
    {
        public Vector2 Direction;
        public float SpeedScale;

        public static IntentMovementData None => new IntentMovementData
        {
            Direction = Vector2.zero,
            SpeedScale = 1f,
        };

        public static IntentMovementData FromDirection(Vector2 direction, float speedScale = 1f)
        {
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            return new IntentMovementData
            {
                Direction = direction,
                SpeedScale = speedScale,
            };
        }
    }
}
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 대쉬/런지/백스텝 같은 "능동적 특수이동"을 관리한다.
    /// - 최종 적용은 하지 않는다
    /// - 현재 프레임의 특수이동 속도만 제공한다
    /// - Rigidbody2D는 직접 만지지 않는다
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityMotionController2D : MonoBehaviour
    {
        private enum MotionKind
        {
            None,
            ConstantVelocity,
            DampedVelocity,
            MoveToPoint
        }

        private MotionKind activeKind = MotionKind.None;

        private Vector2 motionVelocity;
        private float motionRemainingTime;
        private float motionDamping;

        private Vector2 moveToStart;
        private Vector2 moveToEnd;
        private float moveToDuration;
        private float moveToElapsed;

        public bool HasActiveMotion => activeKind != MotionKind.None;

        /// <summary>
        /// 일정 시간 동안 일정 속도로 이동하는 특수이동 시작.
        /// 예: 대쉬, 짧은 돌진
        /// </summary>
        public void StartDash(Vector2 direction, float speed, float duration)
        {
            if (duration <= 0f || speed <= 0f)
                return;

            if (direction.sqrMagnitude <= 0.000001f)
                direction = Vector2.right;
            else
                direction.Normalize();

            activeKind = MotionKind.ConstantVelocity;
            motionVelocity = direction * speed;
            motionRemainingTime = duration;
            motionDamping = 0f;

            moveToStart = Vector2.zero;
            moveToEnd = Vector2.zero;
            moveToDuration = 0f;
            moveToElapsed = 0f;
        }

        /// <summary>
        /// 시작 속도를 강하게 주고 시간에 따라 감쇠시키는 특수이동 시작.
        /// 예: 몸통박치기, 짧은 임펄스 대쉬
        /// </summary>
        public void StartDampedDash(Vector2 direction, float initialSpeed, float duration, float damping)
        {
            if (duration <= 0f || initialSpeed <= 0f)
                return;

            if (direction.sqrMagnitude <= 0.000001f)
                direction = Vector2.right;
            else
                direction.Normalize();

            activeKind = MotionKind.DampedVelocity;
            motionVelocity = direction * initialSpeed;
            motionRemainingTime = duration;
            motionDamping = Mathf.Max(0f, damping);

            moveToStart = Vector2.zero;
            moveToEnd = Vector2.zero;
            moveToDuration = 0f;
            moveToElapsed = 0f;
        }

        /// <summary>
        /// 일정 시간 동안 시작점에서 끝점까지 보간 이동하는 특수이동 시작.
        /// 예: 런지, 지정 거리 슬라이드
        /// </summary>
        public void StartLunge(Vector2 start, Vector2 direction, float distance, float duration)
        {
            if (duration <= 0f || distance <= 0f)
                return;

            if (direction.sqrMagnitude <= 0.000001f)
                direction = Vector2.right;
            else
                direction.Normalize();

            activeKind = MotionKind.MoveToPoint;
            moveToStart = start;
            moveToEnd = start + direction * distance;
            moveToDuration = duration;
            moveToElapsed = 0f;

            motionVelocity = Vector2.zero;
            motionRemainingTime = duration;
            motionDamping = 0f;
        }

        public void CancelMotion()
        {
            Debug.Log("캔슬?");
            activeKind = MotionKind.None;
            motionVelocity = Vector2.zero;
            motionRemainingTime = 0f;
            motionDamping = 0f;
            moveToStart = Vector2.zero;
            moveToEnd = Vector2.zero;
            moveToDuration = 0f;
            moveToElapsed = 0f;
        }

        /// <summary>
        /// 현재 프레임의 특수이동 속도를 계산해 반환한다.
        /// FixedUpdate에서만 읽는 것을 권장.
        /// </summary>
        public Vector2 TickAndGetMotionVelocity(float dt)
        {
            
            if (activeKind == MotionKind.None)
                return Vector2.zero;

            if (dt <= 0f)
                return Vector2.zero;
            switch (activeKind)
            {
                case MotionKind.ConstantVelocity:
                    {
                        motionRemainingTime -= dt;
                        Vector2 result = motionVelocity;

                        if (motionRemainingTime <= 0f)
                            CancelMotion();

                        return result;
                    }

                case MotionKind.DampedVelocity:
                    {
                        motionRemainingTime -= dt;
                        Vector2 result = motionVelocity;

                        if (motionDamping > 0f)
                            motionVelocity = Vector2.Lerp(motionVelocity, Vector2.zero, motionDamping * dt);

                        if (motionRemainingTime <= 0f || motionVelocity.sqrMagnitude <= 0.000001f)
                            CancelMotion();

                        return result;
                    }

                case MotionKind.MoveToPoint:
                    {
                        Vector2 prevPos = Vector2.Lerp(
                            moveToStart,
                            moveToEnd,
                            Mathf.Clamp01(moveToElapsed / moveToDuration));

                        moveToElapsed += dt;

                        Vector2 nextPos = Vector2.Lerp(
                            moveToStart,
                            moveToEnd,
                            Mathf.Clamp01(moveToElapsed / moveToDuration));

                        motionRemainingTime = Mathf.Max(0f, moveToDuration - moveToElapsed);

                        if (moveToElapsed >= moveToDuration)
                        {
                            Vector2 lastVelocity = (nextPos - prevPos) / dt;
                            CancelMotion();
                            return lastVelocity;
                        }

                        return (nextPos - prevPos) / dt;
                    }
            }

            return Vector2.zero;
        }
    }
}

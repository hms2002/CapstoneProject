using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - dash, damped dash, lunge 같은 능동적 특수이동을 단일 motion 채널에서 관리한다.
    /// - motion 종류 간 덮어쓰기/거절 규칙을 적용하고, 현재 프레임의 특수이동 속도만 계산해 제공한다.
    /// - Rigidbody2D를 직접 만지지 않고 MovementMotor2D가 읽을 motion velocity만 반환한다.
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
        private float moveToEaseOutPower = 2f;

        public bool HasActiveMotion => activeKind != MotionKind.None;

        private bool IsDashLikeActive =>
            activeKind == MotionKind.ConstantVelocity ||
            activeKind == MotionKind.DampedVelocity;

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

            CancelMotion();

            activeKind = MotionKind.ConstantVelocity;
            motionVelocity = direction * speed;
            motionRemainingTime = duration;
            motionDamping = 0f;

            moveToStart = Vector2.zero;
            moveToEnd = Vector2.zero;
            moveToDuration = 0f;
            moveToElapsed = 0f;
            moveToEaseOutPower = 2f;
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

            CancelMotion();

            activeKind = MotionKind.DampedVelocity;
            motionVelocity = direction * initialSpeed;
            motionRemainingTime = duration;
            motionDamping = Mathf.Max(0f, damping);

            moveToStart = Vector2.zero;
            moveToEnd = Vector2.zero;
            moveToDuration = 0f;
            moveToElapsed = 0f;
            moveToEaseOutPower = 2f;
        }

        /// <summary>
        /// 일정 시간 동안 시작점에서 끝점까지 보간 이동하는 특수이동 시작.
        /// 예: 런지, 지정 거리 슬라이드
        /// </summary>
        public void StartLunge(Vector2 start, Vector2 direction, float distance, float duration)
        {
            StartLunge(start, direction, distance, duration, 2f);
        }

        /// <summary>
        /// 일정 시간 동안 시작점에서 끝점까지 보간 이동하되 ease-out 강도를 지정한다.
        /// 예: 보스 도약처럼 초반 가속감과 후반 착지감을 더 강하게 줘야 하는 특수이동.
        /// </summary>
        public void StartLunge(Vector2 start, Vector2 direction, float distance, float duration, float easeOutPower)
        {
            if (duration <= 0f || distance <= 0f)
                return;

            if (IsDashLikeActive)
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
            moveToEaseOutPower = Mathf.Max(1f, easeOutPower);

            motionVelocity = Vector2.zero;
            motionRemainingTime = duration;
            motionDamping = 0f;
        }

        public void CancelMotion()
        {
            activeKind = MotionKind.None;
            motionVelocity = Vector2.zero;
            motionRemainingTime = 0f;
            motionDamping = 0f;
            moveToStart = Vector2.zero;
            moveToEnd = Vector2.zero;
            moveToDuration = 0f;
            moveToElapsed = 0f;
            moveToEaseOutPower = 2f;
        }

        /// <summary>
        /// 책임 :
        /// - MoveToPoint 계열 특수이동의 시간 진행도를 이동 진행도로 변환한다.
        /// - 런지 시작은 빠르게, 끝은 천천히 감속되는 ease-out 감각을 공통으로 제공한다.
        /// </summary>
        private float EvaluateMoveToProgress(float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            float inv = 1f - t;
            return 1f - Mathf.Pow(inv, moveToEaseOutPower);
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
                        float prevProgress = EvaluateMoveToProgress(moveToElapsed / moveToDuration);
                        Vector2 prevPos = Vector2.Lerp(
                            moveToStart,
                            moveToEnd,
                            prevProgress);

                        moveToElapsed += dt;

                        float nextProgress = EvaluateMoveToProgress(moveToElapsed / moveToDuration);
                        Vector2 nextPos = Vector2.Lerp(
                            moveToStart,
                            moveToEnd,
                            nextProgress);

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

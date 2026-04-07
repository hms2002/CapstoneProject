using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공격 예고 연출이 월드 어디에, 어떤 크기/회전/지속시간으로 표시될지 전달한다.
    /// </summary>
    public struct AttackTelegraphSpec
    {
        public AttackTelegraphShape shape;
        public Vector3 center;
        public Vector2 size;
        public float rotationDeg;
        public float duration;
        public AttackTelegraphStyle style;

        public static AttackTelegraphSpec CreateRectangle(
            Vector3 center,
            Vector2 size,
            float rotationDeg,
            float duration,
            AttackTelegraphStyle style = null)
        {
            return new AttackTelegraphSpec
            {
                shape = AttackTelegraphShape.Rectangle,
                center = center,
                size = size,
                rotationDeg = rotationDeg,
                duration = duration,
                style = style
            };
        }

        public static AttackTelegraphSpec CreateCircle(
            Vector3 center,
            float diameter,
            float duration,
            AttackTelegraphStyle style = null)
        {
            return new AttackTelegraphSpec
            {
                shape = AttackTelegraphShape.Circle,
                center = center,
                size = new Vector2(diameter, diameter),
                rotationDeg = 0f,
                duration = duration,
                style = style
            };
        }
    }
}

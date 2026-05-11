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
        public float innerDiameter;
        public float sectorAngleDeg;
        public float rotationDeg;
        public float duration;
        public AttackTelegraphStyle style;
        public Vector3 origin;
        public bool useWallClipping;
        public LayerMask wallClipLayers;
        public int wallClipSampleCount;
        public float wallClipSkinWidth;

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
                innerDiameter = 0f,
                sectorAngleDeg = 0f,
                rotationDeg = rotationDeg,
                duration = duration,
                style = style,
                origin = center
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
                innerDiameter = 0f,
                sectorAngleDeg = 0f,
                rotationDeg = 0f,
                duration = duration,
                style = style,
                origin = center
            };
        }

        /// <summary>
        /// 책임 :
        /// - 바깥 반경과 안쪽 빈 반경을 함께 가지는 도넛형 공격 예고를 생성한다.
        /// </summary>
        public static AttackTelegraphSpec CreateRing(
            Vector3 center,
            float outerDiameter,
            float innerDiameter,
            float duration,
            AttackTelegraphStyle style = null)
        {
            return new AttackTelegraphSpec
            {
                shape = AttackTelegraphShape.Ring,
                center = center,
                size = new Vector2(outerDiameter, outerDiameter),
                innerDiameter = Mathf.Clamp(innerDiameter, 0f, outerDiameter),
                sectorAngleDeg = 0f,
                rotationDeg = 0f,
                duration = duration,
                style = style,
                origin = center
            };
        }

        /// <summary>
        /// 책임 :
        /// - 원점에서 지정 방향으로 뻗는 부채꼴 텔레그래프를 생성한다.
        /// - center는 렌더링용 중심으로 자동 환산되며, 호출자는 실제 원점만 넘기면 된다.
        /// </summary>
        public static AttackTelegraphSpec CreateSector(
            Vector3 origin,
            float radius,
            float angleDeg,
            float rotationDeg,
            float duration,
            AttackTelegraphStyle style = null)
        {
            float safeRadius = Mathf.Max(0.01f, radius);
            Vector3 renderCenter = origin + (Quaternion.Euler(0f, 0f, rotationDeg) * Vector3.right) * (safeRadius * 0.5f);

            return new AttackTelegraphSpec
            {
                shape = AttackTelegraphShape.Sector,
                center = renderCenter,
                size = new Vector2(safeRadius, safeRadius * 2f),
                innerDiameter = 0f,
                sectorAngleDeg = Mathf.Clamp(angleDeg, 0.1f, 360f),
                rotationDeg = rotationDeg,
                duration = duration,
                style = style,
                origin = origin
            };
        }

        /// <summary>
        /// 책임 :
        /// - 텔레그래프 렌더링만 벽 레이어 기준으로 잘리게 하는 표시 옵션을 추가한다.
        /// - 실제 공격 판정 차단 여부는 각 ability/pattern의 판정 로직이 별도로 결정한다.
        /// </summary>
        public AttackTelegraphSpec WithWallClipping(
            LayerMask wallLayers,
            int sampleCount = 48,
            float skinWidth = 0.03f)
        {
            useWallClipping = wallLayers.value != 0;
            wallClipLayers = wallLayers;
            wallClipSampleCount = Mathf.Max(3, sampleCount);
            wallClipSkinWidth = Mathf.Max(0f, skinWidth);
            return this;
        }
    }
}

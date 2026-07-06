using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공격 예고 연출이 월드 어디에, 어떤 크기/회전/지속시간으로 표시될지 전달한다.
    /// </summary>
    public struct AttackTelegraphSpec
    {
        public const float TopDownCircleWarningYScale = TopDownEllipseHitUtility2D.DefaultTopDownCircleYScale;

        public AttackTelegraphShape shape;
        public Vector3 center;
        public Vector2 size;
        public float innerDiameter;
        public float sectorAngleDeg;
        public float rotationDeg;
        public float duration;
        public AttackTelegraphStyle style;
        public Vector3 origin;
        public Vector3 lineStart;
        public Vector3 lineEnd;
        public bool useMeshOutline;
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
                origin = center,
                lineStart = center,
                lineEnd = center
            };
        }

        public static AttackTelegraphSpec CreateCircle(
            Vector3 center,
            float diameter,
            float duration,
            AttackTelegraphStyle style = null)
        {
            float safeDiameter = Mathf.Max(0.01f, diameter);
            return CreateEllipse(
                center,
                new Vector2(safeDiameter, safeDiameter),
                duration,
                style);
        }

        public static AttackTelegraphSpec CreateTopDownCircle(
            Vector3 center,
            float diameter,
            float duration,
            AttackTelegraphStyle style = null)
        {
            float safeDiameter = Mathf.Max(0.01f, diameter);
            return CreateEllipse(
                center,
                new Vector2(safeDiameter, safeDiameter * TopDownCircleWarningYScale),
                duration,
                style);
        }

        public static AttackTelegraphSpec CreateEllipse(
            Vector3 center,
            Vector2 size,
            float duration,
            AttackTelegraphStyle style = null)
        {
            return new AttackTelegraphSpec
            {
                shape = AttackTelegraphShape.Circle,
                center = center,
                size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y)),
                innerDiameter = 0f,
                sectorAngleDeg = 0f,
                rotationDeg = 0f,
                duration = duration,
                style = style,
                origin = center,
                lineStart = center,
                lineEnd = center
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
                origin = center,
                lineStart = center,
                lineEnd = center
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
                origin = origin,
                lineStart = origin,
                lineEnd = origin
            };
        }

        /// <summary>
        /// 책임 :
        /// - 원거리 조준선처럼 실제 시작점과 끝점이 중요한 선형 공격 예고를 생성한다.
        /// - 색상은 Style의 borderColor를 사용하고, size.x는 길이, size.y는 선 두께로 보관한다.
        /// </summary>
        public static AttackTelegraphSpec CreateLine(
            Vector3 start,
            Vector3 end,
            float width,
            float duration,
            AttackTelegraphStyle style = null)
        {
            Vector3 delta = end - start;
            float length = Mathf.Max(0.0001f, delta.magnitude);
            float rotationDeg = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            return new AttackTelegraphSpec
            {
                shape = AttackTelegraphShape.Line,
                center = (start + end) * 0.5f,
                size = new Vector2(length, Mathf.Max(0.001f, width)),
                innerDiameter = 0f,
                sectorAngleDeg = 0f,
                rotationDeg = rotationDeg,
                duration = duration,
                style = style,
                origin = start,
                lineStart = start,
                lineEnd = end
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

        /// <summary>
        /// 책임 :
        /// - 벽 기준 자르기는 켜지 않고, mesh/LineRenderer 기반의 얇은 외곽선 렌더링만 요청한다.
        /// - 실제 공격 판정이 원본 사각형/원형 기준인 패턴에서 경고와 피해 범위를 일치시키는 데 사용한다.
        /// </summary>
        public AttackTelegraphSpec WithMeshOutline(int sampleCount = 48)
        {
            useMeshOutline = true;
            wallClipSampleCount = Mathf.Max(3, sampleCount);
            return this;
        }
    }

    /// <summary>
    /// 책임 :
    /// - 여러 보스/몬스터 패턴이 최신 얇은 외곽선 경고 렌더 옵션을 같은 규칙으로 적용하게 돕는다.
    /// - 패턴별 피해 판정 정책에 맞춰 wall clipping 포함/미포함 얇은 외곽선을 선택하게 한다.
    /// </summary>
    public static class AttackTelegraphSpecUtility
    {
        private const int DefaultWarningWallLayer = 30;
        private const int ThinWarningOutlineSampleCount = 48;
        private const float ThinWarningOutlineSkinWidth = 0.03f;

        public static AttackTelegraphSpec WithThinWarningOutline(AttackTelegraphSpec spec)
        {
            LayerMask wallLayers = default;
            wallLayers.value = 1 << DefaultWarningWallLayer;
            return spec.WithWallClipping(
                wallLayers,
                ThinWarningOutlineSampleCount,
                ThinWarningOutlineSkinWidth);
        }

        public static AttackTelegraphSpec WithThinWarningOutlineOnly(AttackTelegraphSpec spec)
        {
            return spec.WithMeshOutline(ThinWarningOutlineSampleCount);
        }
    }
}

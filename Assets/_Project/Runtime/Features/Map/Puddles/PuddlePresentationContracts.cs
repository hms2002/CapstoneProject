using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 : 장판 gameplay가 concrete shader visual 구현 없이 장판 본체 렌더링 상태를 전달하게 하는 계약이다.
    /// </summary>
    public interface IPuddleShaderVisual
    {
        void SetElementType(PuddleElementType newElementType);
        void SetMode(PuddleAreaMode newMode);
        void SetIgnitionProgress(float normalizedProgress);
        void SetRadii(float newGroundRadius, float newProjectileRadius);
        void SetAbsorbAnchor(Transform newAbsorbAnchor);
    }

    /// <summary>
    /// 책임 : 장판 gameplay가 concrete particle visual 구현 없이 장판 보조 파티클 상태를 전달하게 하는 계약이다.
    /// </summary>
    public interface IPuddleParticleVisual
    {
        void SetElementType(PuddleElementType newElementType);
        void SetSurfaceRadius(float radius);
        void ApplyMode(PuddleAreaMode mode);
    }

    /// <summary>
    /// 책임 : 장판 gameplay가 concrete blob visual 구현 없이 흡수/탄막 상태를 전달하게 하는 계약이다.
    /// </summary>
    public interface IPuddleBlobVisual
    {
        void SetAbsorbTarget(float normalizedProgress);
        void SetProjectileMotion(bool isProjectile);
    }
}

using UnityEngine;
using UnityGAS;

/// <summary>
/// 슬라임 여왕 계열 보스가 랜덤 점프 이동 패턴 실행에 필요한 동작을 제공합니다.
/// </summary>
public interface ISlimeQueenRandomJumpHost
{
    float JumpDurationSeconds { get; }

    /// <summary>랜덤 점프 착지 위치를 계산합니다.</summary>
    bool TryGetRandomJumpLandingPosition(out Vector3 landingPosition);

    /// <summary>현재 타겟 방향으로 보스 방향을 갱신합니다.</summary>
    void FaceCurrentTarget();

    /// <summary>랜덤 점프 착지 경고를 표시합니다.</summary>
    void ShowJumpWarning(Vector3 landingPosition);

    /// <summary>이동형 패턴 중 피격과 접촉 피해 차단 상태를 변경합니다.</summary>
    void SetPatternMoveDamageBlocked(bool isBlocked);

    /// <summary>착지 위치 위로 이동한 뒤 체공/급강하하는 진행도를 적용합니다.</summary>
    void SetJumpPose(Vector3 startPosition, Vector3 landingPosition, float normalizedTime);

    /// <summary>점프 종료 위치로 보스 좌표를 확정합니다.</summary>
    void SnapToJumpLanding(Vector3 landingPosition);

    /// <summary>착지 범위 안의 플레이어에게 효과를 적용합니다.</summary>
    void ApplyJumpLandingDamage(AbilitySpec sourceSpec, Vector3 landingPosition);
}

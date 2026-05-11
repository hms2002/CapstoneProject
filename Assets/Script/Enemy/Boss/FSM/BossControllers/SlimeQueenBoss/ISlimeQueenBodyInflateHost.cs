using UnityGAS;

/// <summary>
/// 슬라임 여왕 계열 보스가 몸 부풀림 패턴 실행에 필요한 동작을 제공합니다.
/// </summary>
public interface ISlimeQueenBodyInflateHost
{
    float BodyInflateWarningSeconds { get; }

    /// <summary>현재 타겟 방향으로 보스 방향을 갱신합니다.</summary>
    void FaceCurrentTarget();

    /// <summary>몸 부풀림 원형 경고를 표시합니다.</summary>
    void ShowBodyInflateWarning();

    /// <summary>몸 부풀림 범위 안의 플레이어에게 효과를 적용합니다.</summary>
    void ApplyBodyInflateImpact(AbilitySpec sourceSpec);
}

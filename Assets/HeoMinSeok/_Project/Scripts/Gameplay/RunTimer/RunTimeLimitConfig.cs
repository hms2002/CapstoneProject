using UnityEngine;

/// <summary>
/// 책임 :
/// - 런 시간 제한 시스템이 사용할 기본 제한 시간과 경고 임계값을 제공한다.
/// - 시간 초과 시 어떤 RunEndReason으로 런을 종료할지 데이터로 정의한다.
/// </summary>
[CreateAssetMenu(
    fileName = "RunTimeLimitConfig",
    menuName = "Gameplay/Run Timer/Run Time Limit Config")]
public sealed class RunTimeLimitConfig : ScriptableObject
{
    [Header("Time Limit")]
    [Min(1f)]
    [SerializeField] private float initialLimitSeconds = 600f;
    [Min(0f)]
    [SerializeField] private float lowTimeWarningSeconds = 60f;

    [Header("Failure")]
    [SerializeField] private RunEndReason timeoutReason = RunEndReason.TimeOver;

    public float InitialLimitSeconds => initialLimitSeconds;
    public float LowTimeWarningSeconds => lowTimeWarningSeconds;
    public RunEndReason TimeoutReason => timeoutReason;
}

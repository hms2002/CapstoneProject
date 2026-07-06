using UnityEngine;

/// <summary>
/// 책임: 카메라 흔들림 실행에 필요한 세기, 방향, source, 최소 간격, 수동 fallback 설정을 전달하는 Core 요청 값 타입이다.
/// </summary>
public readonly struct CameraShakeRequest
{
    public readonly float Amplitude;
    public readonly Vector3 Direction;
    public readonly GameObject Source;
    public readonly float MinIntervalSeconds;
    public readonly string DebugReason;
    public readonly bool IgnoreScreenShakeSetting;
    public readonly bool HasManualShakeSettingsOverride;
    public readonly CameraManualShakeSettings ManualShakeSettingsOverride;

    public CameraShakeRequest(
        float amplitude,
        Vector3 direction,
        GameObject source = null,
        float minIntervalSeconds = 0f,
        string debugReason = null,
        bool ignoreScreenShakeSetting = false,
        bool hasManualShakeSettingsOverride = false,
        CameraManualShakeSettings manualShakeSettingsOverride = default)
    {
        Amplitude = amplitude;
        Direction = direction;
        Source = source;
        MinIntervalSeconds = Mathf.Max(0f, minIntervalSeconds);
        DebugReason = debugReason;
        IgnoreScreenShakeSetting = ignoreScreenShakeSetting;
        HasManualShakeSettingsOverride = hasManualShakeSettingsOverride;
        ManualShakeSettingsOverride = manualShakeSettingsOverride;
    }
}

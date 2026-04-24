using UnityEngine;

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

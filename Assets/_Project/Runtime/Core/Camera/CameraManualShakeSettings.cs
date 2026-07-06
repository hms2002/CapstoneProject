using UnityEngine;

/// <summary>
/// 책임: 수동 카메라 흔들림 fallback이 사용할 지속시간, 위치 진폭, 노이즈, 방향성 가중치를 전달하는 값 타입이다.
/// </summary>
[System.Serializable]
public struct CameraManualShakeSettings
{
    [Min(0f)] public float duration;
    [Min(0f)] public float positionAmplitudeScale;
    [Min(0f)] public float noiseFrequency;
    [Range(0f, 1f)] public float directionalBiasWeight;

    public static CameraManualShakeSettings Create(
        float duration,
        float positionAmplitudeScale = 0.35f,
        float noiseFrequency = 24f,
        float directionalBiasWeight = 0.35f)
    {
        return new CameraManualShakeSettings
        {
            duration = Mathf.Max(0f, duration),
            positionAmplitudeScale = Mathf.Max(0f, positionAmplitudeScale),
            noiseFrequency = Mathf.Max(0f, noiseFrequency),
            directionalBiasWeight = Mathf.Clamp01(directionalBiasWeight),
        };
    }

    public static CameraManualShakeSettings Default =>
        Create(
            duration: 0.12f);
}

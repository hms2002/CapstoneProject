using UnityEngine;

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

[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class CameraManualShakeDriver : MonoBehaviour
{
    private Vector3 basePosition;
    private Quaternion baseRotation;
    private Vector3 directionBias;
    private float amplitude;
    private float duration;
    private float remaining;
    private float seed;
    private CameraManualShakeSettings settings;
    private bool hasAppliedOffset;

    public void Play(float shakeAmplitude, Vector3 direction, in CameraManualShakeSettings shakeSettings)
    {
        float clampedAmplitude = Mathf.Max(0f, shakeAmplitude);
        if (clampedAmplitude <= 0f)
            return;

        settings = shakeSettings;
        amplitude = clampedAmplitude;
        duration = settings.duration;
        if (duration <= 0f)
            return;

        remaining = duration;
        seed = Random.value * 1000f;

        direction.z = 0f;
        directionBias = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;

        enabled = true;
    }

    private void OnDisable()
    {
        RestoreBaseTransform();
        enabled = false;
    }

    private void LateUpdate()
    {
        RestoreBaseTransform();

        if (remaining <= 0f)
        {
            enabled = false;
            return;
        }

        basePosition = transform.position;
        baseRotation = transform.rotation;

        float deltaTime = Time.unscaledDeltaTime;
        remaining = Mathf.Max(0f, remaining - deltaTime);

        float progress = duration <= 0f ? 1f : 1f - (remaining / duration);
        float fade = 1f - SmoothStep(progress);

        float noiseTime = Time.unscaledTime * settings.noiseFrequency;
        float noiseX = (Mathf.PerlinNoise(seed, noiseTime) - 0.5f) * 2f;
        float noiseY = (Mathf.PerlinNoise(seed + 17.31f, noiseTime * 1.17f) - 0.5f) * 2f;

        Vector3 right = transform.right;
        Vector3 up = transform.up;
        Vector3 directional = (right * directionBias.x) + (up * directionBias.y);
        Vector3 noise = (right * noiseX) + (up * noiseY);
        Vector3 rawOffset = directional * settings.directionalBiasWeight + noise;
        if (rawOffset.sqrMagnitude <= 0.0001f)
            rawOffset = right;

        Vector3 offset = rawOffset.normalized * (amplitude * settings.positionAmplitudeScale * fade);
        transform.position = basePosition + offset;
        hasAppliedOffset = true;
    }

    private void RestoreBaseTransform()
    {
        if (!hasAppliedOffset)
            return;

        transform.position = basePosition;
        transform.rotation = baseRotation;
        hasAppliedOffset = false;
    }

    private static float SmoothStep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}

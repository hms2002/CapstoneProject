using UnityEngine;

/// <summary>
/// 책임: Cinemachine impulse를 사용할 수 없거나 일시정지 상태일 때 카메라 Transform에 짧은 수동 흔들림을 적용한다.
/// </summary>
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
    private float punchDistance;
    private float punchSeconds;
    private CameraManualShakeSettings settings;
    private bool hasAppliedOffset;
    private bool playWhilePaused;

    public void Play(float shakeAmplitude, Vector3 direction, in CameraManualShakeSettings shakeSettings, float impactPunchDistance = 0f, float impactPunchSeconds = 0f, bool allowDuringPause = false)
    {
        float clampedAmplitude = Mathf.Max(0f, shakeAmplitude);
        if (clampedAmplitude <= 0f)
            return;

        settings = shakeSettings;
        playWhilePaused = allowDuringPause;
        punchDistance = impactPunchDistance;
        punchSeconds = impactPunchSeconds;
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

    // Remove last frame's offset before Cinemachine writes this frame's follow pose.
    private void Update() => RestoreBaseTransform();

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

        if (TimeScalePausePlayback.IsPaused && !playWhilePaused) return;
        float deltaTime = Time.unscaledDeltaTime;
        remaining = Mathf.Max(0f, remaining - deltaTime);

        float elapsed = duration - remaining;
        // The following shake owns its own decay; the punch must not consume its amplitude.
        float shakeDuration = Mathf.Max(0.0001f, duration - punchSeconds);
        float progress = punchSeconds > 0f
            ? Mathf.Clamp01((elapsed - punchSeconds) / shakeDuration)
            : (duration <= 0f ? 1f : 1f - (remaining / duration));
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
        if (punchSeconds > 0f && elapsed < punchSeconds)
        {
            float t = elapsed / punchSeconds;
            float envelope = t < 0.2f ? t / 0.2f : 1f - SmoothStep((t - 0.2f) / 0.8f);
            offset = directionBias * (punchDistance * envelope);
        }
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

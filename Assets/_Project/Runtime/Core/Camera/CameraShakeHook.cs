using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임: Core 카메라 흔들림 요청을 실제 카메라 구현으로 넘기는 최소 backend 계약이다.
/// </summary>
public interface ICameraShakeBackend
{
    bool Play(in CameraShakeRequest request);
}

/// <summary>
/// 책임: Core/Gameplay 호출자가 구체 CameraShakeService 구현 없이 카메라 흔들림을 요청하게 한다.
/// </summary>
public static class CameraShakePlayback
{
    private static ICameraShakeBackend backend;

    public static void RegisterBackend(ICameraShakeBackend cameraShakeBackend)
    {
        backend = cameraShakeBackend;
    }

    public static bool Play(in CameraShakeRequest request)
    {
        return backend != null && backend.Play(request);
    }
}

/// <summary>
/// 책임: 카메라 흔들림 방향을 fallback 방향으로 처리할지, hook에 지정된 커스텀 방향으로 처리할지 나타낸다.
/// </summary>
public enum CameraShakeDirectionMode
{
    UseFallback = 0,
    UseCustom = 1,
}

/// <summary>
/// 책임: Core/Gameplay 데이터가 구체 카메라 서비스 없이 카메라 흔들림 세기와 방향 정책을 전달하는 직렬화 값 타입이다.
/// </summary>
[Serializable]
public struct CameraShakeHook
{
    [Min(0f)] public float amplitude;
    [Min(0f)] public float amplitudeMultiplier;
    [Min(0f)] public float maxAmplitude;
    [Min(0f)] public float minIntervalSeconds;
    public CameraShakeDirectionMode directionMode;
    public Vector3 customDirection;
    public bool ignoreScreenShakeSetting;

    public readonly float EffectiveAmplitudeMultiplier =>
        Mathf.Approximately(amplitudeMultiplier, 0f) ? 1f : amplitudeMultiplier;

    public readonly bool TryPlay(
        GameObject source,
        Vector3 fallbackDirection,
        float amplitudeScale = 1f,
        string debugReason = null)
    {
        float scaledAmplitude = Mathf.Max(0f, amplitude) * Mathf.Max(0f, amplitudeScale);
        return TryPlayOverrideAmplitude(scaledAmplitude, source, fallbackDirection, debugReason);
    }

    public readonly bool TryPlayOverrideAmplitude(
        float overrideAmplitude,
        GameObject source,
        Vector3 fallbackDirection,
        string debugReason = null)
    {
        float finalAmplitude = Mathf.Max(0f, overrideAmplitude) * EffectiveAmplitudeMultiplier;
        if (maxAmplitude > 0f)
            finalAmplitude = Mathf.Min(maxAmplitude, finalAmplitude);

        if (finalAmplitude <= 0f)
            return false;

        Vector3 direction = ResolveDirection(fallbackDirection);
        return CameraShakePlayback.Play(new CameraShakeRequest(
            finalAmplitude,
            direction,
            source,
            minIntervalSeconds,
            debugReason,
            ignoreScreenShakeSetting));
    }

    public readonly bool TryPlayFromCueParams(
        in GameplayCueParams cueParams,
        float amplitudeScale = 1f,
        string debugReason = null)
    {
        GameObject source = cueParams.Causer != null ? cueParams.Causer : cueParams.Instigator;
        return TryPlay(source, ResolveDirectionFromCueParams(cueParams), amplitudeScale, debugReason);
    }

    public readonly bool TryPlayOverrideAmplitudeFromCueParams(
        float overrideAmplitude,
        in GameplayCueParams cueParams,
        string debugReason = null)
    {
        GameObject source = cueParams.Causer != null ? cueParams.Causer : cueParams.Instigator;
        return TryPlayOverrideAmplitude(
            overrideAmplitude,
            source,
            ResolveDirectionFromCueParams(cueParams),
            debugReason);
    }

    public static CameraShakeHook Create(
        float amplitude,
        float amplitudeMultiplier = 1f,
        float maxAmplitude = 0f,
        float minIntervalSeconds = 0f)
    {
        return new CameraShakeHook
        {
            amplitude = Mathf.Max(0f, amplitude),
            amplitudeMultiplier = Mathf.Max(0f, amplitudeMultiplier),
            maxAmplitude = Mathf.Max(0f, maxAmplitude),
            minIntervalSeconds = Mathf.Max(0f, minIntervalSeconds),
            directionMode = CameraShakeDirectionMode.UseFallback,
            customDirection = Vector3.zero,
            ignoreScreenShakeSetting = false,
        };
    }

    private readonly Vector3 ResolveDirection(Vector3 fallbackDirection)
    {
        Vector3 direction = fallbackDirection;

        if (directionMode == CameraShakeDirectionMode.UseCustom &&
            customDirection.sqrMagnitude > 0.0001f)
        {
            direction = customDirection;
        }

        direction.z = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return Vector3.up;

        return direction.normalized;
    }

    private static Vector3 ResolveDirectionFromCueParams(in GameplayCueParams cueParams)
    {
        GameObject origin = cueParams.Causer != null ? cueParams.Causer : cueParams.Instigator;

        if (cueParams.Target != null && origin != null)
        {
            Vector3 delta = cueParams.Target.transform.position - origin.transform.position;
            delta.z = 0f;
            if (delta.sqrMagnitude > 0.0001f)
                return delta.normalized;
        }

        if (cueParams.HasExplicitPosition && origin != null)
        {
            Vector3 delta = cueParams.Position - origin.transform.position;
            delta.z = 0f;
            if (delta.sqrMagnitude > 0.0001f)
                return delta.normalized;
        }

        Vector3 normal = cueParams.Normal;
        normal.z = 0f;
        if (normal.sqrMagnitude > 0.0001f)
            return normal.normalized;

        return Vector3.up;
    }
}

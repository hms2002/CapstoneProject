using UnityEngine;

/// <summary>
/// 책임 : Gameplay 계층이 구체 time-scale pause 구현 없이 전역 일시정지 토큰을 획득/해제하게 하는 계약이다.
/// </summary>
public interface ITimeScalePauseBackend
{
    bool IsPaused { get; }
    bool IsHeldBy(Object owner);
    bool Acquire(Object owner);
    bool Release(Object owner);
}

/// <summary>
/// 책임 : Gameplay/Core 호출자가 Infrastructure time-scale pause 서비스 타입을 직접 참조하지 않게 한다.
/// </summary>
public interface ICombatSlowMotionBackend
{
    bool IsCombatSlowMotion { get; }
    bool AcquireCombatSlowMotion(Object owner);
}

/// <summary>Updates one presentation owner's scale atomically without releasing other pause owners.</summary>
public interface ITimeScaleRampBackend
{
    bool SetOwnedTimeScale(Object owner, float scale);
}

public static class TimeScalePausePlayback
{
    private static ITimeScalePauseBackend backend;

    public static bool SetOwnedTimeScale(Object owner, float scale)
        => backend is ITimeScaleRampBackend ramp && ramp.SetOwnedTimeScale(owner, scale);

    public static bool IsCombatSlowMotion => backend is ICombatSlowMotionBackend slow && slow.IsCombatSlowMotion;
    public static bool AcquireCombatSlowMotion(Object owner)
        => backend is ICombatSlowMotionBackend slow && slow.AcquireCombatSlowMotion(owner);
    public static float PresentationDeltaTime => IsPaused ? 0f :
        IsCombatSlowMotion ? Time.unscaledDeltaTime : Time.deltaTime;

    public static bool IsPaused => backend != null && backend.IsPaused;

    public static void RegisterBackend(ITimeScalePauseBackend pauseBackend)
    {
        backend = pauseBackend;
    }

    public static bool IsHeldBy(Object owner)
    {
        return backend != null && backend.IsHeldBy(owner);
    }

    public static bool Acquire(Object owner)
    {
        return backend != null && backend.Acquire(owner);
    }

    public static bool Release(Object owner)
    {
        return backend != null && backend.Release(owner);
    }
}

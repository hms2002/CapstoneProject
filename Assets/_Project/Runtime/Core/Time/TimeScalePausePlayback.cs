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
public static class TimeScalePausePlayback
{
    private static ITimeScalePauseBackend backend;

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

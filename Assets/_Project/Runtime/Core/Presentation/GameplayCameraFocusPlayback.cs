using System.Collections;
using UnityEngine;

/// <summary>
/// 책임 : Gameplay 코드가 구체 Cinemachine rig 없이 임시 카메라 포커스와 렌즈 상태를 제어하게 하는 세션 계약이다.
/// </summary>
public interface IGameplayCameraFocusSession
{
    Transform CachedFollow { get; }
    bool HasOrthographicSize { get; }
    float CachedOrthographicSize { get; }
    float CurrentOrthographicSize { get; }
    Vector3 CurrentCenter { get; }

    void SetTarget(Transform target);
    void SnapToTarget(Transform target);
    void SetOrthographicSize(float orthographicSize);
    IEnumerator WaitForSettle(Transform target);
    void Restore(Transform preferredTarget);
}

/// <summary>
/// 책임 : Core/Gameplay의 카메라 포커스 요청을 구체 카메라 구현 세션으로 변환한다.
/// </summary>
public interface IGameplayCameraFocusBackend
{
    IGameplayCameraFocusSession Capture(Component owner);
}

/// <summary>
/// 책임 : Gameplay 코드가 구체 카메라 부트스트랩 구현 없이 현재 gameplay 출력 카메라를 조회하게 하는 계약이다.
/// </summary>
public interface IGameplayCameraViewBackend
{
    Camera GetMainCamera();
}

/// <summary>
/// 책임 : Gameplay 코드가 구체 카메라 rig 없이 임시 지도 줌 상태를 적용하고 원래 gameplay 카메라 상태로 복구하게 하는 세션 계약이다.
/// </summary>
public interface IGameplayCameraMapZoomSession
{
    bool IsValid { get; }
    float Aspect { get; }
    float CurrentOrthographicSize { get; }
    float CachedOrthographicSize { get; }
    Vector2 CurrentCenter { get; }

    void Begin(int minimumPriority);
    void Apply(Vector2 center, float orthographicSize);
    Vector2 ResolveRestoreCenter();
    void Restore();
}

/// <summary>
/// 책임 : Core/Gameplay의 지도 줌 요청을 구체 카메라 구현 세션으로 변환한다.
/// </summary>
public interface IGameplayCameraMapZoomBackend
{
    IGameplayCameraMapZoomSession Capture();
}

/// <summary>
/// 책임 : Gameplay 호출자가 Presentation/Infrastructure 카메라 구현체 타입 없이 임시 포커스 세션을 생성하게 한다.
/// </summary>
public static class GameplayCameraFocusPlayback
{
    private static IGameplayCameraFocusBackend backend;

    public static void RegisterBackend(IGameplayCameraFocusBackend newBackend)
    {
        backend = newBackend;
    }

    public static IGameplayCameraFocusSession Capture(Component owner)
    {
        return backend?.Capture(owner);
    }

    public static void SnapToTarget(Component owner, Transform target)
    {
        IGameplayCameraFocusSession session = Capture(owner);
        session?.SnapToTarget(target);
    }
}

/// <summary>
/// 책임 : Gameplay 호출자가 CameraBootstrap 타입 없이 현재 gameplay 출력 카메라를 조회하게 한다.
/// </summary>
public static class GameplayCameraViewQuery
{
    private static IGameplayCameraViewBackend backend;

    public static void RegisterBackend(IGameplayCameraViewBackend newBackend)
    {
        backend = newBackend;
    }

    public static Camera GetMainCamera()
    {
        return backend?.GetMainCamera();
    }
}

/// <summary>
/// 책임 : Gameplay 호출자가 CameraBootstrap/Cinemachine 타입 없이 지도 전체 줌 세션을 생성하게 한다.
/// </summary>
public static class GameplayCameraMapZoomPlayback
{
    private static IGameplayCameraMapZoomBackend backend;

    public static void RegisterBackend(IGameplayCameraMapZoomBackend newBackend)
    {
        backend = newBackend;
    }

    public static IGameplayCameraMapZoomSession Capture()
    {
        return backend?.Capture();
    }
}

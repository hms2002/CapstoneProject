/// <summary>
/// 책임 : ScenePortal의 구체 씬 전환 실행 구현을 Gameplay 포털 코드에서 숨기는 backend 계약이다.
/// </summary>
public interface IScenePortalTravelBackend
{
    bool TryTravel(ScenePortal portal);
}

/// <summary>
/// 책임 : ScenePortal이 Infrastructure 씬 전환 서비스 타입 없이 포털 이동 실행을 요청하게 한다.
/// </summary>
public static class ScenePortalTravelPlayback
{
    private static IScenePortalTravelBackend backend;

    public static void RegisterBackend(IScenePortalTravelBackend travelBackend)
    {
        backend = travelBackend;
    }

    public static void UnregisterBackend(IScenePortalTravelBackend travelBackend)
    {
        if (ReferenceEquals(backend, travelBackend))
            backend = null;
    }

    public static bool TryTravel(ScenePortal portal)
    {
        return backend != null && backend.TryTravel(portal);
    }
}

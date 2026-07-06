/// <summary>
/// 책임 : 구체 preload 서비스 없이 프레젠테이션 preload window 갱신을 요청하게 하는 backend 계약이다.
/// </summary>
public interface IPresentationPreloadBackend
{
    void RefreshFirstRunIntroWindow(string reason);
}

/// <summary>
/// 책임 : Gameplay 코드가 Infrastructure preload 서비스 타입 없이 preload window 갱신을 요청하게 한다.
/// </summary>
public static class PresentationPreloadPlayback
{
    private static IPresentationPreloadBackend backend;

    public static bool IsAvailable => backend != null;

    public static void RegisterBackend(IPresentationPreloadBackend preloadBackend)
    {
        backend = preloadBackend;
    }

    public static void UnregisterBackend(IPresentationPreloadBackend preloadBackend)
    {
        if (ReferenceEquals(backend, preloadBackend))
            backend = null;
    }

    public static bool RefreshFirstRunIntroWindow(string reason = null)
    {
        if (backend == null)
            return false;

        backend.RefreshFirstRunIntroWindow(reason);
        return true;
    }
}

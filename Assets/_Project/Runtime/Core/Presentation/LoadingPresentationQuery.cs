/// <summary>
/// 책임 : Gameplay 계층이 구체 로딩 오버레이 구현 없이 로딩 프레젠테이션 차단 상태만 조회하게 하는 계약이다.
/// </summary>
public interface ILoadingPresentationStateBackend
{
    bool IsActiveLoadingPresentation { get; }
}

/// <summary>
/// 책임 : 입력/전투 흐름에서 로딩 오버레이의 현재 차단 상태를 구체 UI 타입 없이 조회하게 한다.
/// </summary>
public static class LoadingPresentationQuery
{
    private static ILoadingPresentationStateBackend backend;

    public static bool IsActiveLoadingPresentation =>
        backend != null && backend.IsActiveLoadingPresentation;

    public static void RegisterBackend(ILoadingPresentationStateBackend stateBackend)
    {
        backend = stateBackend;
    }

    public static void UnregisterBackend(ILoadingPresentationStateBackend stateBackend)
    {
        if (ReferenceEquals(backend, stateBackend))
            backend = null;
    }
}

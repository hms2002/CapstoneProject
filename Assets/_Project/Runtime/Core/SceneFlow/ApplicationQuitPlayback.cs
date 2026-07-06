using UnityEngine;

/// <summary>
/// 책임 : Core/UI 호출자가 구체 Editor API 없이 애플리케이션 종료 처리를 위임하게 하는 backend 계약이다.
/// </summary>
public interface IApplicationQuitBackend
{
    bool TryQuitApplication();
}

/// <summary>
/// 책임 : 런타임 종료 요청을 현재 플랫폼/에디터 backend로 전달하고, 없으면 Unity 기본 종료를 수행한다.
/// </summary>
public static class ApplicationQuitPlayback
{
    private static IApplicationQuitBackend backend;

    public static void RegisterBackend(IApplicationQuitBackend quitBackend)
    {
        backend = quitBackend;
    }

    public static void UnregisterBackend(IApplicationQuitBackend quitBackend)
    {
        if (ReferenceEquals(backend, quitBackend))
            backend = null;
    }

    public static void Quit()
    {
        if (backend != null && backend.TryQuitApplication())
            return;

        Application.Quit();
    }
}

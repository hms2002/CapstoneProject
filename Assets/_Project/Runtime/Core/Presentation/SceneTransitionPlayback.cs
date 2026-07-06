/// <summary>
/// 책임 : Gameplay 계층이 구체 씬 전환 구현 없이 현재 전환 상태를 조회하고 씬 이동을 요청하게 하는 계약이다.
/// </summary>
public interface ISceneTransitionHandle
{
    bool IsTransitionActive { get; }
    bool TryLoadScene(string targetSceneName);
    bool TryLoadScene(string targetSceneName, float fadeOutDurationOverride);
    bool TryLoadScene(string targetSceneName, float fadeOutDurationOverride, float fadeInDurationOverride);
}

/// <summary>
/// 책임 : 구체 씬 전환 coordinator의 조회/생성을 Core 호출자에게 숨기는 backend 계약이다.
/// </summary>
public interface ISceneTransitionBackend
{
    ISceneTransitionHandle Instance { get; }
    ISceneTransitionHandle EnsureInstance();
}

/// <summary>
/// 책임 : Gameplay/Core 계층이 Infrastructure 씬 전환 타입을 직접 참조하지 않고 씬 전환을 조회/요청하게 한다.
/// </summary>
public static class SceneTransitionPlayback
{
    private static ISceneTransitionBackend backend;

    public static ISceneTransitionHandle Instance => backend?.Instance;

    public static bool IsTransitionActive =>
        backend?.Instance != null && backend.Instance.IsTransitionActive;

    public static void RegisterBackend(ISceneTransitionBackend transitionBackend)
    {
        backend = transitionBackend;
    }

    public static ISceneTransitionHandle EnsureInstance()
    {
        return backend?.EnsureInstance();
    }
}

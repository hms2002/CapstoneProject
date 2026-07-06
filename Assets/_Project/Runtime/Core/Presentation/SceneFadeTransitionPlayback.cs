using System.Collections;
using UnityEngine;

/// <summary>
/// 책임 : Gameplay 계층이 구체 씬 페이드 구현 없이 전환 상태와 페이드 세션을 제어하게 하는 계약이다.
/// </summary>
public interface ISceneFadeTransitionHandle
{
    bool IsTransitionActive { get; }
    void SetPlayerUnlockBlocked(Object owner, bool blocked);
    bool TryBeginOverlayFadeSession(float initialAlpha = 0f);
    IEnumerator FadeOutAsync(float duration);
    IEnumerator FadeInAsync(float duration);
    void EndOverlayFadeSession();
}

/// <summary>
/// 책임 : 구체 씬 페이드 서비스의 조회/생성을 Core 호출자에게 숨기는 backend 계약이다.
/// </summary>
public interface ISceneFadeTransitionBackend
{
    ISceneFadeTransitionHandle Instance { get; }
    ISceneFadeTransitionHandle EnsureInstance(bool allowRuntimeFallback = false);
}

/// <summary>
/// 책임 : Gameplay/Core 계층이 Infrastructure 씬 페이드 타입을 직접 참조하지 않고 전환 페이드 상태를 조회하게 한다.
/// </summary>
public static class SceneFadeTransitionPlayback
{
    private static ISceneFadeTransitionBackend backend;

    public static ISceneFadeTransitionHandle Instance => backend?.Instance;

    public static void RegisterBackend(ISceneFadeTransitionBackend fadeBackend)
    {
        backend = fadeBackend;
    }

    public static ISceneFadeTransitionHandle EnsureInstance(bool allowRuntimeFallback = false)
    {
        return backend?.EnsureInstance(allowRuntimeFallback);
    }
}

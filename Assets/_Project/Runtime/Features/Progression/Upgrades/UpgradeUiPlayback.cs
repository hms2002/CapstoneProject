using UnityEngine;

/// <summary>
/// 책임 : 업그레이드 UI 구현이 Gameplay 업그레이드 시스템에 제공해야 하는 열기/닫기 backend 계약이다.
/// </summary>
public interface IUpgradeUiBackend
{
    Component BackendComponent { get; }
    void Toggle(bool useFadePresentationOnOpen, float openFadeOutDuration, float openFadeInDuration);
    void Close();
    void Cleanup();
}

/// <summary>
/// 책임 : UpgradeManager가 구체 UI 타입 없이 업그레이드 화면 열기/닫기 명령을 요청하게 한다.
/// </summary>
public static class UpgradeUiPlayback
{
    private static IUpgradeUiBackend backend;

    public static void RegisterBackend(IUpgradeUiBackend newBackend)
    {
        backend = newBackend;
    }

    public static void UnregisterBackend(IUpgradeUiBackend oldBackend)
    {
        if (ReferenceEquals(backend, oldBackend))
            backend = null;
    }

    public static IUpgradeUiBackend ResolveBackend()
    {
        if (IsBackendAlive(backend))
            return backend;

        backend = FindSceneBackend();
        return backend;
    }

    public static void Toggle(bool useFadePresentationOnOpen, float openFadeOutDuration, float openFadeInDuration)
    {
        ResolveBackend()?.Toggle(useFadePresentationOnOpen, openFadeOutDuration, openFadeInDuration);
    }

    public static void Close()
    {
        ResolveBackend()?.Close();
    }

    public static void Cleanup()
    {
        ResolveBackend()?.Cleanup();
    }

    private static bool IsBackendAlive(IUpgradeUiBackend candidate)
    {
        return candidate != null && candidate.BackendComponent != null;
    }

    private static IUpgradeUiBackend FindSceneBackend()
    {
        MonoBehaviour[] behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || !behaviour.gameObject.scene.IsValid())
                continue;

            if (behaviour is IUpgradeUiBackend upgradeUiBackend)
                return upgradeUiBackend;
        }

        return null;
    }
}

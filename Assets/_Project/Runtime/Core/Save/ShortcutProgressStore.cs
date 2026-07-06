using UnityEngine;

/// <summary>
/// 책임 : 영구/런중 숏컷 해금 상태의 구체 저장 구현을 Core/Gameplay 호출자에게 숨기는 backend 계약이다.
/// </summary>
public interface IShortcutProgressStoreBackend
{
    bool IsShortcutUnlocked(string mapID, string doorID);
    void UnlockShortcut(string mapID, string doorID, UnityEngine.Object requester);
}

/// <summary>
/// 책임 : Gameplay 코드가 Infrastructure 숏컷 저장 서비스 타입 없이 숏컷 해금 상태를 조회하고 갱신하게 한다.
/// </summary>
public static class ShortcutProgressStore
{
    private static IShortcutProgressStoreBackend backend;

    public static bool IsAvailable => IsBackendAlive(backend);

    public static void RegisterBackend(IShortcutProgressStoreBackend progressBackend)
    {
        backend = progressBackend;
    }

    public static void UnregisterBackend(IShortcutProgressStoreBackend progressBackend)
    {
        if (ReferenceEquals(backend, progressBackend))
            backend = null;
    }

    public static bool IsShortcutUnlocked(string mapID, string doorID)
    {
        return IsAvailable && backend.IsShortcutUnlocked(mapID, doorID);
    }

    public static void UnlockShortcut(string mapID, string doorID, UnityEngine.Object requester = null)
    {
        if (IsAvailable)
            backend.UnlockShortcut(mapID, doorID, requester);
    }

    private static bool IsBackendAlive(IShortcutProgressStoreBackend progressBackend)
    {
        if (progressBackend == null)
            return false;

        if (progressBackend is Component component)
            return component != null;

        return true;
    }
}

using System;
using UnityEngine;

/// <summary>
/// 책임 : UpgradeManager 싱글턴 소유권, 전역 UI 서비스 부모 지정, DontDestroyOnLoad 유지 처리를 캡슐화한다.
/// </summary>
internal static class UpgradeManagerLifetimeService
{
    public static bool TryClaimInstance(
        UpgradeManager manager,
        Func<UpgradeManager> getCurrent,
        Action<UpgradeManager> setCurrent)
    {
        if (manager == null)
            return false;

        UpgradeManager current = getCurrent != null ? getCurrent() : null;
        if (current != null && current != manager)
        {
            UnityEngine.Object.Destroy(manager.gameObject);
            return false;
        }

        setCurrent?.Invoke(manager);
        GlobalCanvasPlayback.AdoptService(manager.transform);
        MarkPersistent(manager.transform);
        return true;
    }

    public static void ReleaseInstance(
        UpgradeManager manager,
        Func<UpgradeManager> getCurrent,
        Action<UpgradeManager> setCurrent)
    {
        if (manager == null)
            return;

        UpgradeManager current = getCurrent != null ? getCurrent() : null;
        if (current == manager)
            setCurrent?.Invoke(null);
    }

    private static void MarkPersistent(Transform transform)
    {
        Transform persistentRoot = transform != null ? transform.root : null;
        if (persistentRoot == null || persistentRoot.parent != null)
            return;

        UnityEngine.Object.DontDestroyOnLoad(persistentRoot.gameObject);
    }
}

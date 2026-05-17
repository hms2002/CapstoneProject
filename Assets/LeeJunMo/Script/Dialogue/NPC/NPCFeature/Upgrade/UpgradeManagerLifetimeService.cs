using System;
using UnityEngine;

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
        GlobalUIRoot.AdoptService(manager.transform);
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

using UnityEngine;

/// <summary>
/// 책임 : 영구 저장 데이터와 런 세션 임시 데이터를 합쳐 숏컷 해금 상태를 관리하는 Infrastructure 저장 backend이다.
/// </summary>
public sealed class ShortcutProgressService : MonoBehaviour, IShortcutProgressStoreBackend
{
    public static ShortcutProgressService Instance { get; private set; }

    private static bool s_isQuitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        var root = new GameObject(nameof(ShortcutProgressService));
        root.AddComponent<ShortcutProgressService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ShortcutProgressStore.RegisterBackend(this);
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        ShortcutProgressStore.UnregisterBackend(this);

        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public void UnlockShortcut(string mapID, string doorID)
    {
        UnlockShortcut(mapID, doorID, this);
    }

    public void UnlockShortcut(string mapID, string doorID, Object requester)
    {
        if (!TryGetMapData(out MapSaveData mapData))
            return;

        if (RunSessionStore.IsRunActive)
        {
            if (IsShortcutUnlocked(mapID, doorID))
                return;

            RunSessionStore.AddPendingShortcutUnlock(mapID, doorID);
            return;
        }

        StageProgress stageData = mapData.GetStageData(mapID);
        if (!stageData.unlockedShortcuts.Contains(doorID))
        {
            stageData.unlockedShortcuts.Add(doorID);
            GameDataStore.RequestImmediateSave(requester != null ? requester : this);
        }
    }

    public bool IsShortcutUnlocked(string mapID, string doorID)
    {
        if (RunSessionStore.IsRunActive && RunSessionStore.HasPendingShortcutUnlock(mapID, doorID))
            return true;

        if (!TryGetMapData(out MapSaveData mapData))
            return false;

        StageProgress stageData = mapData.GetStageData(mapID);
        return stageData.unlockedShortcuts.Contains(doorID);
    }

    private static bool TryGetMapData(out MapSaveData mapData)
    {
        mapData = null;

        if (GameDataStore.Data == null)
            return false;

        if (GameDataStore.Data.mapData == null)
            GameDataStore.Data.mapData = new MapSaveData();

        mapData = GameDataStore.Data.mapData;
        return true;
    }
}

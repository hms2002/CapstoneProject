using UnityEngine;

public sealed class ShortcutProgressService : MonoBehaviour
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
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public void UnlockShortcut(string mapID, string doorID)
    {
        if (!TryGetMapData(out MapSaveData mapData))
            return;

        if (IsRunActive())
        {
            if (IsShortcutUnlocked(mapID, doorID))
                return;

            GamePlayDataManager.Instance.AddPendingShortcutUnlock(mapID, doorID);
            return;
        }

        StageProgress stageData = mapData.GetStageData(mapID);
        if (!stageData.unlockedShortcuts.Contains(doorID))
        {
            stageData.unlockedShortcuts.Add(doorID);
            GameDataSaveCoordinator.RequestImmediateSave(this);
        }
    }

    public bool IsShortcutUnlocked(string mapID, string doorID)
    {
        if (IsRunActive() && GamePlayDataManager.Instance.HasPendingShortcutUnlock(mapID, doorID))
            return true;

        if (!TryGetMapData(out MapSaveData mapData))
            return false;

        StageProgress stageData = mapData.GetStageData(mapID);
        return stageData.unlockedShortcuts.Contains(doorID);
    }

    private static bool TryGetMapData(out MapSaveData mapData)
    {
        mapData = null;

        if (GameDataManager.Instance == null || GameDataManager.Instance.Data == null)
            return false;

        if (GameDataManager.Instance.Data.mapData == null)
            GameDataManager.Instance.Data.mapData = new MapSaveData();

        mapData = GameDataManager.Instance.Data.mapData;
        return true;
    }

    private static bool IsRunActive()
    {
        return GamePlayDataManager.Instance != null
            && GamePlayDataManager.Instance.Data != null
            && GamePlayDataManager.Instance.Data.isRunActive;
    }
}

using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    private static bool s_isQuitting;

    public GameData Data { get; private set; }

    private GameDataRepository repository;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        var go = new GameObject(nameof(GameDataManager));
        go.AddComponent<GameDataManager>();
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

        repository = new GameDataRepository();
        LoadData();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void LoadData()
    {
        repository ??= new GameDataRepository();
        Data = repository.LoadOrCreate();
        Debug.Log("[GameDataManager] Game data loaded.");
    }

    public GameData EnsureData()
    {
        if (Data == null)
            Data = new GameData();

        return Data;
    }

    public void SaveData()
    {
        EnsureData();

        if (Data.itemData == null)
            Data.itemData = new ItemSaveData();

        if (ItemManager.Instance != null)
        {
            Data.itemData.unlockedWeaponIDs = ItemManager.Instance.GetUnlockedWeaponIDs();
            Data.itemData.unlockedRelicIDs = ItemManager.Instance.GetUnlockedRelicIDs();
        }

        repository ??= new GameDataRepository();
        repository.Save(Data);
        Debug.Log(
            $"[GameDataManager] Save complete. Persistent: {repository.SavePath}, Inspectable: {repository.InspectableSavePath}");
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;

        if (GamePlayDataManager.Instance != null
            && GamePlayDataManager.Instance.Data != null
            && GamePlayDataManager.Instance.Data.isRunActive)
        {
            Debug.Log("[GameDataManager] Skipping save on quit because a run is still active.");
            return;
        }

        SaveData();
    }
}

using UnityEngine;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    private static bool s_isQuitting;

    public GameData Data { get; private set; }
    public int ActiveSlotIndex { get; private set; }

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

        repository = new GameDataRepository(ActiveSlotIndex);
        LoadData();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void LoadData()
    {
        LoadData(ActiveSlotIndex);
    }

    public void LoadData(int slotIndex)
    {
        ActiveSlotIndex = Mathf.Max(0, slotIndex);
        repository = new GameDataRepository(ActiveSlotIndex);
        Data = repository.LoadOrCreate();
        NormalizeLoadedData();
        Debug.Log($"[GameDataManager] Game data loaded for slot {ActiveSlotIndex + 1}.");
    }

    public bool LoadSlot(int slotIndex)
    {
        int normalizedSlotIndex = Mathf.Max(0, slotIndex);
        LoadData(normalizedSlotIndex);
        return true;
    }

    public void ResetLoadedSlotIfActive(int slotIndex)
    {
        int normalizedSlotIndex = Mathf.Max(0, slotIndex);
        if (normalizedSlotIndex != ActiveSlotIndex)
            return;

        repository = new GameDataRepository(ActiveSlotIndex);
        Data = new GameData();
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

        if (Data.bossDialogueData == null)
            Data.bossDialogueData = new BossDialogueSaveData();
        Data.bossDialogueData.bossRecords ??= new System.Collections.Generic.List<BossDialogueRecord>();

        if (Data.runSpecialNpcData == null)
            Data.runSpecialNpcData = new RunSpecialNpcSaveData();
        Data.runSpecialNpcData.constructionRecords ??= new System.Collections.Generic.List<RunSpecialNpcConstructionRecord>();

        if (Data.tutorialData == null)
            Data.tutorialData = new TutorialSaveData();
        Data.tutorialData.Normalize();

        if (ItemManager.Instance != null)
        {
            Data.itemData.unlockedWeaponIDs = ItemManager.Instance.GetUnlockedWeaponIDs();
            Data.itemData.unlockedRelicIDs = ItemManager.Instance.GetUnlockedRelicIDs();
        }

        if (UpgradeManager.Instance != null)
        {
            var allUpgrades = UpgradeManager.Instance.GetAllUpgrades();
            Data.knownTotalUpgradeCount = allUpgrades != null ? Mathf.Max(0, allUpgrades.Count) : 0;
        }

        repository ??= new GameDataRepository(ActiveSlotIndex);
        repository.Save(Data);
        Debug.Log(
            $"[GameDataManager] Save complete. Persistent: {repository.SavePath}, Inspectable: {repository.InspectableSavePath}");
    }

    private void NormalizeLoadedData()
    {
        EnsureData();

        Data.itemData ??= new ItemSaveData();
        Data.affectionData ??= new AffectionSaveData();
        Data.bossDialogueData ??= new BossDialogueSaveData();
        Data.bossDialogueData.bossRecords ??= new System.Collections.Generic.List<BossDialogueRecord>();
        Data.runSpecialNpcData ??= new RunSpecialNpcSaveData();
        Data.runSpecialNpcData.constructionRecords ??= new System.Collections.Generic.List<RunSpecialNpcConstructionRecord>();
        Data.tutorialData ??= new TutorialSaveData();
        Data.tutorialData.Normalize();
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

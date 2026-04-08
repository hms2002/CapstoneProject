using System.Collections.Generic;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance { get; private set; }

    private static bool s_isQuitting;

    [SerializeField] private ItemDatabase database;

    private readonly HashSet<string> unlockedWeaponIDs = new HashSet<string>();
    private readonly HashSet<string> unlockedRelicIDs = new HashSet<string>();

    private bool isInitialized;
    private ItemSaveData pendingSaveData;

    /// <summary>
    /// 책임 :
    /// - 씬 복원/루팅 시스템이 ItemManager를 안전하게 조회할 수 있는 준비 상태를 노출한다.
    /// - 싱글톤 인스턴스만 존재하고 database가 아직 adopt되지 않은 중간 상태를 구분한다.
    /// </summary>
    public bool IsReady => database != null && isInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        var go = new GameObject(nameof(ItemManager));
        go.AddComponent<ItemManager>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            TryInitializeFromGameData();
            return;
        }

        Instance.TryAdoptDatabase(database);
        Destroy(gameObject);
    }

    private void Start()
    {
        TryInitializeFromGameData();
    }

    public void Initialize(ItemSaveData saveData)
    {
        pendingSaveData = saveData;

        if (database == null)
            return;

        database.InitializeCache();
        unlockedWeaponIDs.Clear();
        unlockedRelicIDs.Clear();

        if (database.defaultUnlockedWeapons != null)
        {
            foreach (var weapon in database.defaultUnlockedWeapons)
            {
                if (weapon != null)
                    unlockedWeaponIDs.Add(weapon.weaponId);
            }
        }

        if (database.defaultUnlockedRelics != null)
        {
            foreach (var relic in database.defaultUnlockedRelics)
            {
                if (relic != null)
                    unlockedRelicIDs.Add(relic.relicId);
            }
        }

        if (saveData != null)
        {
            if (saveData.unlockedWeaponIDs != null)
                unlockedWeaponIDs.UnionWith(saveData.unlockedWeaponIDs);

            if (saveData.unlockedRelicIDs != null)
                unlockedRelicIDs.UnionWith(saveData.unlockedRelicIDs);
        }

        isInitialized = true;
        pendingSaveData = null;

        Debug.Log($"[ItemManager] Initialized. unlockedWeapons={unlockedWeaponIDs.Count}, unlockedRelics={unlockedRelicIDs.Count}");
    }

    public void UnlockWeapon(string id)
    {
        if (database == null)
            return;

        if (!unlockedWeaponIDs.Contains(id) && database.GetWeaponByID(id) != null)
        {
            unlockedWeaponIDs.Add(id);
            Debug.Log($"[ItemManager] Weapon unlocked: {id}");
        }
    }

    public void UnlockRelic(string id)
    {
        if (database == null)
            return;

        if (!unlockedRelicIDs.Contains(id) && database.GetRelicByID(id) != null)
        {
            unlockedRelicIDs.Add(id);
            Debug.Log($"[ItemManager] Relic unlocked: {id}");
        }
    }

    public bool IsWeaponUnlocked(string id) => unlockedWeaponIDs.Contains(id);
    public bool IsRelicUnlocked(string id) => unlockedRelicIDs.Contains(id);

    public List<string> GetUnlockedWeaponIDs() => new List<string>(unlockedWeaponIDs);
    public List<string> GetUnlockedRelicIDs() => new List<string>(unlockedRelicIDs);

    public WeaponDefinition GetWeaponData(string id)
    {
        return database != null ? database.GetWeaponByID(id) : null;
    }

    public RelicDefinition GetRelicData(string id)
    {
        return database != null ? database.GetRelicByID(id) : null;
    }

    public ConsumableDefinition GetConsumableData(string id)
    {
        return database != null ? database.GetConsumableByID(id) : null;
    }

    public List<ConsumableDefinition> GetAllConsumables()
    {
        return database != null ? database.GetAllConsumables() : new List<ConsumableDefinition>();
    }

    private void TryInitializeFromGameData()
    {
        if (isInitialized || GameDataManager.Instance == null)
            return;

        Initialize(GameDataManager.Instance.Data != null ? GameDataManager.Instance.Data.itemData : null);
    }

    private void TryAdoptDatabase(ItemDatabase incomingDatabase)
    {
        if (incomingDatabase == null)
            return;

        if (database != null)
        {
            if (database != incomingDatabase)
            {
                Debug.LogWarning("[ItemManager] Different ItemDatabase was supplied by a scene instance. Keeping the existing database.", this);
            }

            return;
        }

        database = incomingDatabase;
        database.InitializeCache();

        if (!isInitialized)
        {
            Initialize(pendingSaveData ?? (GameDataManager.Instance != null ? GameDataManager.Instance.Data?.itemData : null));
        }
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }
}

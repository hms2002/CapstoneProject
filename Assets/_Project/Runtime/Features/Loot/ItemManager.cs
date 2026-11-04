using System.Collections.Generic;
using UnityEngine;

// 책임: 아이템 데이터베이스 로드, 무기/유물 해금 상태, 저장 데이터 반영을 관리한다.
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
        RestoreEarnedAffectionUnlocks();

        CapstoneDiagnostics.EditorOnlyLog.Log($"[ItemManager] Initialized. unlockedWeapons={unlockedWeaponIDs.Count}, unlockedRelics={unlockedRelicIDs.Count}");
    }

    public void UnlockWeapon(string id)
    {
        if (database == null)
            return;

        if (!unlockedWeaponIDs.Contains(id) && database.GetWeaponByID(id) != null)
        {
            unlockedWeaponIDs.Add(id);
            CapstoneDiagnostics.EditorOnlyLog.Log($"[ItemManager] Weapon unlocked: {id}");
        }
    }

    public void UnlockRelic(string id)
    {
        if (database == null)
            return;

        if (!unlockedRelicIDs.Contains(id) && database.GetRelicByID(id) != null)
        {
            unlockedRelicIDs.Add(id);
            CapstoneDiagnostics.EditorOnlyLog.Log($"[ItemManager] Relic unlocked: {id}");
        }
    }

    public bool IsWeaponUnlocked(string id)
    {
        RestoreEarnedAffectionUnlocks();
        return unlockedWeaponIDs.Contains(id);
    }

    public bool IsRelicUnlocked(string id)
    {
        RestoreEarnedAffectionUnlocks();
        return unlockedRelicIDs.Contains(id);
    }

    public List<string> GetUnlockedWeaponIDs()
    {
        RestoreEarnedAffectionUnlocks();
        return new List<string>(unlockedWeaponIDs);
    }

    public List<string> GetUnlockedRelicIDs()
    {
        RestoreEarnedAffectionUnlocks();
        return new List<string>(unlockedRelicIDs);
    }

    // Add missing permanent unlocks after reward content is added to an existing profile.
    // Queries retry when the NPC database is adopted later; no other effects or popup are replayed.
    private void RestoreEarnedAffectionUnlocks()
    {
        var records = GameDataStore.Data?.affectionData?.affectionRecords;
        if (!IsReady || NPCManager.Instance == null || records == null)
            return;

        foreach (AffectionRecord record in records)
        {
            if (record == null)
                continue;

            NPCData npc = NPCManager.Instance.GetNPCData(record.npcId);
            if (npc?.affectionRewards == null)
                continue;

            foreach (AffectionReward reward in npc.affectionRewards)
            {
                if (reward.targetLevel > record.amount || reward.effect is not UnlockItemAffectionEffect unlock)
                    continue;

                if (unlock.weapons != null)
                    foreach (WeaponDefinition weapon in unlock.weapons)
                        if (weapon != null) UnlockWeapon(weapon.weaponId);
                if (unlock.relics != null)
                    foreach (RelicDefinition relic in unlock.relics)
                        if (relic != null) UnlockRelic(relic.relicId);
            }
        }
    }

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
        if (isInitialized || !GameDataStore.IsAvailable)
            return;

        Initialize(GameDataStore.Data != null ? GameDataStore.Data.itemData : null);
    }

    private void TryAdoptDatabase(ItemDatabase incomingDatabase)
    {
        if (incomingDatabase == null)
            return;

        if (database != null)
        {
            if (database != incomingDatabase)
            {
                CapstoneDiagnostics.EditorOnlyLog.LogWarning("[ItemManager] Different ItemDatabase was supplied by a scene instance. Keeping the existing database.", this);
            }

            return;
        }

        database = incomingDatabase;
        database.InitializeCache();

        if (!isInitialized)
        {
            Initialize(pendingSaveData ?? GameDataStore.Data?.itemData);
        }
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }
}

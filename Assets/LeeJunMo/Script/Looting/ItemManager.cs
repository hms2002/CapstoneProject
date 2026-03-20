using UnityEngine;
using System.Collections.Generic;

public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance { get; private set; }

    [SerializeField] private ItemDatabase database;

    private HashSet<string> unlockedWeaponIDs = new HashSet<string>();
    private HashSet<string> unlockedRelicIDs = new HashSet<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Initialize(ItemSaveData saveData)
    {
        if (database == null)
        {
            Debug.LogError("[ItemManager] ItemDatabase가 연결되지 않았습니다!");
            return;
        }

        database.InitializeCache();
        unlockedWeaponIDs.Clear();
        unlockedRelicIDs.Clear();

        // 1. 기본 해금 아이템 세팅
        if (database.defaultUnlockedWeapons != null)
        {
            foreach (var w in database.defaultUnlockedWeapons)
                if (w != null) unlockedWeaponIDs.Add(w.weaponId);
        }

        if (database.defaultUnlockedRelics != null)
        {
            foreach (var r in database.defaultUnlockedRelics)
                if (r != null) unlockedRelicIDs.Add(r.relicId);
        }

        // 2. 세이브 데이터 덮어쓰기
        if (saveData != null)
        {
            if (saveData.unlockedWeaponIDs != null)
                unlockedWeaponIDs.UnionWith(saveData.unlockedWeaponIDs);

            if (saveData.unlockedRelicIDs != null)
                unlockedRelicIDs.UnionWith(saveData.unlockedRelicIDs);
        }

        Debug.Log($"[ItemManager] 초기화 완료. (무기 해금: {unlockedWeaponIDs.Count} / 유물 해금: {unlockedRelicIDs.Count})");
    }

    public void UnlockWeapon(string id)
    {
        if (!unlockedWeaponIDs.Contains(id) && database.GetWeaponByID(id) != null)
        {
            unlockedWeaponIDs.Add(id);
            Debug.Log($"[ItemManager] 무기 해금됨: {id}");
        }
    }

    public void UnlockRelic(string id)
    {
        if (!unlockedRelicIDs.Contains(id) && database.GetRelicByID(id) != null)
        {
            unlockedRelicIDs.Add(id);
            Debug.Log($"[ItemManager] 유물 해금됨: {id}");
        }
    }

    public bool IsWeaponUnlocked(string id) => unlockedWeaponIDs.Contains(id);
    public bool IsRelicUnlocked(string id) => unlockedRelicIDs.Contains(id);

    // 게임을 저장할 때 GameDataManager가 이 함수들을 호출해서 리스트를 가져갑니다.
    public List<string> GetUnlockedWeaponIDs() => new List<string>(unlockedWeaponIDs);
    public List<string> GetUnlockedRelicIDs() => new List<string>(unlockedRelicIDs);


    // =========================================================
    // 🌟 [추가됨] 드롭 시스템(LootManager)을 위한 데이터 반환 헬퍼 함수
    // =========================================================
    
    /// <summary>
    /// 무기 ID를 입력받아 실제 WeaponDefinition(ScriptableObject)을 반환합니다.
    /// </summary>
    public WeaponDefinition GetWeaponData(string id)
    {
        if (database != null)
        {
            return database.GetWeaponByID(id);
        }
        return null;
    }

    /// <summary>
    /// 유물 ID를 입력받아 실제 RelicDefinition(ScriptableObject)을 반환합니다.
    /// </summary>
    public RelicDefinition GetRelicData(string id)
    {
        if (database != null)
        {
            return database.GetRelicByID(id);
        }
        return null;
    }
}
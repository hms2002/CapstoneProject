using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    // 런타임에서 사용할 데이터 원본
    public GameData Data { get; private set; }

    // [필수] 인스펙터에서 ItemDatabase 에셋을 연결해야 합니다.
    [Header("Link")]
    public ItemDatabase itemDatabase;

    private string savePath;

    // [New] 마정석 변경 알림 이벤트 (UI 갱신용)
    public event Action<int> OnMagicStoneChanged;

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
            return;
        }

        savePath = Application.persistentDataPath + "/GameSave.json";

        // 게임 시작 시 데이터 로드
        LoadData();
    }

    public void SaveData()
    {
        string json = JsonUtility.ToJson(Data, true);
        File.WriteAllText(savePath, json);
        Debug.Log($"[GameDataManager] 저장 완료: {savePath}");
    }

    public void LoadData()
    {
        if (File.Exists(savePath))
        {
            try
            {
                string json = File.ReadAllText(savePath);
                Data = JsonUtility.FromJson<GameData>(json);
                Debug.Log("[GameDataManager] 로드 성공");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[GameDataManager] 로드 실패 (초기화함): {e.Message}");
                Data = new GameData();
            }
        }
        else
        {
            Debug.Log("[GameDataManager] 세이브 파일 없음. 새로 생성.");
            Data = new GameData();
        }

        // [핵심] 로드된 데이터를 기반으로 아이템 DB 동기화
        if (itemDatabase != null)
        {
            itemDatabase.Initialize(Data.itemData);
        }
        else
        {
            Debug.LogError("[GameDataManager] ItemDatabase가 연결되지 않았습니다! 인스펙터를 확인하세요.");
        }
    }

    // =========================================================
    // [New] Effect 스크립트에서 호출할 공개 해금 함수들
    // =========================================================

    // 1. 무기 해금 (외부 호출용)
    public void UnlockWeapon(string id)
    {
        // A. 런타임(게임플레이) 데이터베이스에 즉시 반영
        // (상점이나 인벤토리에서 바로 뜨게 함)
        if (itemDatabase != null)
        {
            itemDatabase.UnlockWeapon(id);
        }

        // B. 세이브 데이터(GameData)에 ID 기록
        SaveItemUnlock(id, true);

        // C. 파일로 저장 (해금은 중요한 정보라 바로 저장을 권장)
        SaveData();
    }

    // 2. 유물 해금 (외부 호출용)
    public void UnlockRelic(string id)
    {
        // A. 런타임 반영
        if (itemDatabase != null)
        {
            // ItemDatabase에 UnlockRelic 함수가 있어야 합니다.
            // (아래 참고사항 확인)
            itemDatabase.UnlockRelic(id);
        }

        // B. 세이브 데이터 기록
        SaveItemUnlock(id, false);

        // C. 파일 저장
        SaveData();
    }

    public void UnlockItems(List<WeaponDefinition> weapons, List<RelicDefinition> relics)
    {
        // 1. 무기 해금 처리 (ID 추출하여 DB에 전달)
        if (weapons != null)
        {
            foreach (var w in weapons)
            {
                if (w == null) continue;
                UnlockWeapon(w.weaponId); // 문자열 ID 사용
            }
        }

        // 2. 유물 해금 처리 (ID 추출하여 DB에 전달)
        if (relics != null)
        {
            foreach (var r in relics)
            {
                if (r == null) continue;
                UnlockRelic(r.relicId); // 문자열 ID 사용
            }
        }
    }

    // =========================================================
    // [New] 아이템 해금 데이터 저장 헬퍼 함수
    // =========================================================
    public void SaveItemUnlock(string id, bool isWeapon)
    {
        if (isWeapon)
        {
            if (!Data.itemData.unlockedWeaponIDs.Contains(id))
            {
                Data.itemData.unlockedWeaponIDs.Add(id);
                // 중요할 경우 즉시 저장 (선택 사항)
                // SaveData(); 
            }
        }
        else
        {
            if (!Data.itemData.unlockedRelicIDs.Contains(id))
            {
                Data.itemData.unlockedRelicIDs.Add(id);
                // SaveData();
            }
        }
    }

    // =========================================================
    // 기존 숏컷 관련 함수
    // =========================================================
    public void UnlockShortcut(string mapID, string doorID)
    {
        StageProgress stageData = Data.mapData.GetStageData(mapID);
        if (!stageData.unlockedShortcuts.Contains(doorID))
        {
            stageData.unlockedShortcuts.Add(doorID);
        }
    }

    public bool IsShortcutUnlocked(string mapID, string doorID)
    {
        StageProgress stageData = Data.mapData.GetStageData(mapID);
        return stageData.unlockedShortcuts.Contains(doorID);
    }

    // 마정석 획득
    public void AddMagicStone(int amount)
    {
        Data.magicStone += amount;
        OnMagicStoneChanged?.Invoke(Data.magicStone);
        // Debug.Log($"[재화] 마정석 획득: +{amount} (Total: {Data.magicStone})");

        // 중요하면 바로 저장 (선택사항)
        // SaveData(); 
    }

    // 마정석 사용 (성공 시 true, 부족하면 false 반환)
    public bool SpendMagicStone(int amount)
    {
        if (Data.magicStone >= amount)
        {
            Data.magicStone -= amount;
            OnMagicStoneChanged?.Invoke(Data.magicStone);
            SaveData(); // 사용 후에는 저장을 권장
            return true;
        }
        else
        {
            Debug.Log("[재화] 마정석이 부족합니다.");
            return false;
        }
    }

    // 현재 개수 확인
    public int GetMagicStoneCount() => Data.magicStone;
    private void OnApplicationQuit()
    {
        SaveData();
    }
}
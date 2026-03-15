using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    public GameData Data { get; private set; }

    private string savePath;

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

        LoadData();
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

        // [핵심] 로드 후 ItemManager에게 데이터를 넘겨주어 메모리 세팅 지시
        // (주의: Awake 실행 순서상 ItemManager가 먼저 생겼거나, 다른 곳에서 초기화 호출을 맞춰야 합니다)
        if (ItemManager.Instance != null)
        {
            ItemManager.Instance.Initialize(Data.itemData);
        }
    }

    // [핵심] 중앙 통제형 저장 함수. 이제 여러 곳에서 마구잡이로 부르지 않습니다.
    public void SaveData()
    {
        if (Data == null) return;

        // 1. 디스크에 쓰기 직전, ItemManager 메모리에 있는 최신 해금 리스트를 끌어옵니다.
        if (ItemManager.Instance != null)
        {
            Data.itemData.unlockedWeaponIDs = ItemManager.Instance.GetUnlockedWeaponIDs();
            Data.itemData.unlockedRelicIDs = ItemManager.Instance.GetUnlockedRelicIDs();
        }

        // 2. 파일 쓰기
        string json = JsonUtility.ToJson(Data, true);
        File.WriteAllText(savePath, json);
        Debug.Log($"[GameDataManager] 일괄 저장 완료: {savePath}");
    }

    // =========================================================
    // 숏컷 관련 함수 (기존 유지)
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

    // =========================================================
    // 마정석 관련 함수 (즉시 저장 삭제)
    // =========================================================
    public void AddMagicStone(int amount)
    {
        Data.magicStone += amount;
        OnMagicStoneChanged?.Invoke(Data.magicStone);
        // SaveData() 삭제됨. 나중에 한 번에 저장.
    }

    public bool SpendMagicStone(int amount)
    {
        if (Data.magicStone >= amount)
        {
            Data.magicStone -= amount;
            OnMagicStoneChanged?.Invoke(Data.magicStone);
            // SaveData() 삭제됨. 나중에 한 번에 저장.
            return true;
        }
        else
        {
            Debug.Log("[재화] 마정석이 부족합니다.");
            return false;
        }
    }

    public int GetMagicStoneCount() => Data.magicStone;

    private void OnApplicationQuit()
    {
        // 게임 꺼질 때는 확실하게 한 번 더 저장!
        SaveData();
    }
}
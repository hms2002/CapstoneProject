using UnityEngine;
using System.IO;

public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    // 런타임에서 사용할 데이터 원본
    public GameData Data { get; private set; }

    private string savePath;

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

        // 게임 켜지자마자 가장 먼저 로드 (1순위)
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
    }

    public void UnlockShortcut(string mapID, string doorID)
    {
        StageProgress stageData = Data.mapData.GetStageData(mapID);
        if (!stageData.unlockedShortcuts.Contains(doorID))
        {
            stageData.unlockedShortcuts.Add(doorID);
            // SaveData();
        }
    }

    public bool IsShortcutUnlocked(string mapID, string doorID)
    {
        StageProgress stageData = Data.mapData.GetStageData(mapID);
        return stageData.unlockedShortcuts.Contains(doorID);
    }

    private void OnApplicationQuit()
    {
        SaveData();
    }
}
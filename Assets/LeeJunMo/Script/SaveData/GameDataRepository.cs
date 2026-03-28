using System;
using System.IO;
using UnityEngine;

public sealed class GameDataRepository
{
    public string SavePath { get; }

    public GameDataRepository(string savePath = null)
    {
        SavePath = string.IsNullOrWhiteSpace(savePath) ? GetDefaultSavePath() : savePath;
    }

    public static string GetDefaultSavePath()
    {
        return Path.Combine(Application.persistentDataPath, "GameSave.json");
    }

    public bool Exists()
    {
        return File.Exists(SavePath);
    }

    public GameData LoadOrCreate()
    {
        if (!Exists())
            return new GameData();

        try
        {
            string json = File.ReadAllText(SavePath);
            GameData loaded = JsonUtility.FromJson<GameData>(json);
            return loaded ?? new GameData();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataRepository] 저장 파일 로드 실패: {e.Message}");
            return new GameData();
        }
    }

    public void Save(GameData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
    }

    public void Delete()
    {
        if (!Exists())
            return;

        File.Delete(SavePath);
    }
}

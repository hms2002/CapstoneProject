using System;
using System.IO;
using UnityEngine;

public sealed class GameDataRepository
{
    public string SavePath { get; }
    public string InspectableSavePath { get; }

    public GameDataRepository(string savePath = null)
    {
        SavePath = string.IsNullOrWhiteSpace(savePath) ? GetDefaultSavePath() : savePath;
        InspectableSavePath = GetInspectableSavePath();
    }

    public static string GetDefaultSavePath()
    {
        return Path.Combine(Application.persistentDataPath, "GameSave.json");
    }

    public static string GetInspectableSavePath()
    {
        string dataPath = Application.dataPath;
        if (string.IsNullOrWhiteSpace(dataPath))
            return null;

        string rootPath = Path.GetDirectoryName(dataPath);
        if (string.IsNullOrWhiteSpace(rootPath))
            return null;

        return Path.Combine(rootPath, "GameData.json");
    }

    public bool Exists()
    {
        return File.Exists(InspectableSavePath) || File.Exists(SavePath);
    }

    public GameData LoadOrCreate()
    {
        if (!File.Exists(InspectableSavePath))
        {
            GameData freshData = new GameData();
            Save(freshData);
            Debug.Log($"[GameDataRepository] Inspectable save was missing. Recreated fresh data at: {InspectableSavePath}");
            return freshData;
        }

        try
        {
            string json = File.ReadAllText(InspectableSavePath);
            GameData loaded = JsonUtility.FromJson<GameData>(json);
            GameData data = loaded ?? new GameData();
            Save(data);
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataRepository] Failed to load inspectable save. Recreating fresh data: {e.Message}");

            GameData freshData = new GameData();
            Save(freshData);
            return freshData;
        }
    }

    public void Save(GameData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        TryWriteInspectableCopy(json);
    }

    public void Delete()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);

        TryDeleteInspectableCopy();
    }

    private void TryWriteInspectableCopy(string json)
    {
        if (string.IsNullOrWhiteSpace(InspectableSavePath))
            return;

        if (string.Equals(SavePath, InspectableSavePath, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            string directory = Path.GetDirectoryName(InspectableSavePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(InspectableSavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameDataRepository] Failed to write inspectable GameData copy: {e.Message}");
        }
    }

    private void TryDeleteInspectableCopy()
    {
        if (string.IsNullOrWhiteSpace(InspectableSavePath))
            return;

        if (string.Equals(SavePath, InspectableSavePath, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (File.Exists(InspectableSavePath))
                File.Delete(InspectableSavePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameDataRepository] Failed to delete inspectable GameData copy: {e.Message}");
        }
    }
}

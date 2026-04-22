using System;
using System.IO;
using UnityEngine;

public sealed class GameDataRepository
{
    public int SlotIndex { get; }
    public string SavePath { get; }
    public string InspectableSavePath { get; }

    public GameDataRepository(int slotIndex = 0, string savePath = null)
    {
        SlotIndex = NormalizeSlotIndex(slotIndex);
        SavePath = string.IsNullOrWhiteSpace(savePath) ? GetDefaultSavePath(SlotIndex) : savePath;
        InspectableSavePath = GetInspectableSavePath(SlotIndex);
    }

    public static string GetDefaultSavePath(int slotIndex = 0)
    {
        int normalizedSlotIndex = NormalizeSlotIndex(slotIndex);
        return Path.Combine(Application.persistentDataPath, $"GameSave_Slot{normalizedSlotIndex + 1}.json");
    }

    public static string GetInspectableSavePath(int slotIndex = 0)
    {
        string dataPath = Application.dataPath;
        if (string.IsNullOrWhiteSpace(dataPath))
            return null;

        string rootPath = Path.GetDirectoryName(dataPath);
        if (string.IsNullOrWhiteSpace(rootPath))
            return null;

        int normalizedSlotIndex = NormalizeSlotIndex(slotIndex);
        return Path.Combine(rootPath, $"GameData_Slot{normalizedSlotIndex + 1}.json");
    }

    public bool Exists()
    {
        return TryGetReadablePath(out _);
    }

    public bool TryLoad(out GameData data)
    {
        data = null;

        if (!TryGetReadablePath(out string readablePath))
            return false;

        try
        {
            string json = File.ReadAllText(readablePath);
            GameData loaded = JsonUtility.FromJson<GameData>(json);
            data = loaded ?? new GameData();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameDataRepository] Failed to load save at {readablePath}. {e.Message}");
            return false;
        }
    }

    public GameData LoadOrCreate()
    {
        if (TryLoad(out GameData loadedData))
        {
            Save(loadedData);
            return loadedData;
        }

        GameData freshData = new GameData();
        Save(freshData);
        Debug.Log($"[GameDataRepository] Save was missing. Recreated fresh data for slot {SlotIndex + 1} at: {InspectableSavePath}");
        return freshData;
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

        if (SlotIndex == 0)
        {
            TryDeleteFile(GetLegacyDefaultSavePath());
            TryDeleteFile(GetLegacyInspectableSavePath());
        }
    }

    private bool TryGetReadablePath(out string readablePath)
    {
        if (File.Exists(InspectableSavePath))
        {
            readablePath = InspectableSavePath;
            return true;
        }

        if (File.Exists(SavePath))
        {
            readablePath = SavePath;
            return true;
        }

        if (SlotIndex == 0)
        {
            string legacyInspectablePath = GetLegacyInspectableSavePath();
            if (!string.IsNullOrWhiteSpace(legacyInspectablePath) && File.Exists(legacyInspectablePath))
            {
                readablePath = legacyInspectablePath;
                return true;
            }

            string legacySavePath = GetLegacyDefaultSavePath();
            if (!string.IsNullOrWhiteSpace(legacySavePath) && File.Exists(legacySavePath))
            {
                readablePath = legacySavePath;
                return true;
            }
        }

        readablePath = null;
        return false;
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

    private static int NormalizeSlotIndex(int slotIndex)
    {
        return Mathf.Max(0, slotIndex);
    }

    private static string GetLegacyDefaultSavePath()
    {
        return Path.Combine(Application.persistentDataPath, "GameSave.json");
    }

    private static string GetLegacyInspectableSavePath()
    {
        string dataPath = Application.dataPath;
        if (string.IsNullOrWhiteSpace(dataPath))
            return null;

        string rootPath = Path.GetDirectoryName(dataPath);
        if (string.IsNullOrWhiteSpace(rootPath))
            return null;

        return Path.Combine(rootPath, "GameData.json");
    }

    private static void TryDeleteFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GameDataRepository] Failed to delete save file {path}: {e.Message}");
        }
    }
}

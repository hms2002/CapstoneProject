using System;
using UnityEngine;

/// <summary>Owns a disposable profile copy; test actions cannot write it to a save slot.</summary>
internal sealed class TestSceneProfileStore : IGameDataStoreBackend
{
    public GameData Data { get; }
    public int ActiveSlotIndex { get; }
    public event Action<GameData, int> OnDataLoaded { add { } remove { } }

    public TestSceneProfileStore(GameData source, int slot)
    {
        Data = source != null
            ? JsonUtility.FromJson<GameData>(JsonUtility.ToJson(source))
            : new GameData();
        ActiveSlotIndex = slot;
    }

    public GameData EnsureData() => Data;
    public void SaveData() { }
    public void RequestImmediateSave(UnityEngine.Object requester) { }
    public void RequestDeferredSave(UnityEngine.Object requester) { }
    public void FlushSave(UnityEngine.Object requester) { }
}

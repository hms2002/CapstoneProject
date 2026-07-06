using System;
using UnityEngine;

/// <summary>
/// 책임 : 영구 저장 데이터의 구체 저장소 구현을 Core/Gameplay 호출자에게 숨기는 backend 계약이다.
/// </summary>
public interface IGameDataStoreBackend
{
    GameData Data { get; }
    int ActiveSlotIndex { get; }
    event Action<GameData, int> OnDataLoaded;

    GameData EnsureData();
    void SaveData();
    void RequestImmediateSave(UnityEngine.Object requester);
    void RequestDeferredSave(UnityEngine.Object requester);
    void FlushSave(UnityEngine.Object requester);
}

/// <summary>
/// 책임 : Core/Gameplay 코드가 Infrastructure 저장 매니저 타입 없이 영구 저장 데이터를 조회하고 저장 요청을 보낸다.
/// </summary>
public static class GameDataStore
{
    private static IGameDataStoreBackend backend;

    public static event Action<GameData, int> OnDataLoaded;

    public static bool IsAvailable => IsBackendAlive(backend);
    public static GameData Data => IsAvailable ? backend.Data : null;
    public static int ActiveSlotIndex => IsAvailable ? backend.ActiveSlotIndex : 0;

    public static void RegisterBackend(IGameDataStoreBackend storeBackend)
    {
        if (ReferenceEquals(backend, storeBackend))
            return;

        UnsubscribeBackend();
        backend = storeBackend;
        SubscribeBackend();
    }

    public static void UnregisterBackend(IGameDataStoreBackend storeBackend)
    {
        if (!ReferenceEquals(backend, storeBackend))
            return;

        UnsubscribeBackend();
        backend = null;
    }

    public static GameData EnsureData()
    {
        return IsAvailable ? backend.EnsureData() : null;
    }

    public static void SaveData()
    {
        if (IsAvailable)
            backend.SaveData();
    }

    public static void RequestImmediateSave(UnityEngine.Object requester = null)
    {
        if (IsAvailable)
            backend.RequestImmediateSave(requester);
    }

    public static void RequestDeferredSave(UnityEngine.Object requester = null)
    {
        if (IsAvailable)
            backend.RequestDeferredSave(requester);
    }

    public static void FlushSave(UnityEngine.Object requester = null)
    {
        if (IsAvailable)
            backend.FlushSave(requester);
    }

    private static void SubscribeBackend()
    {
        if (backend != null)
            backend.OnDataLoaded += RelayDataLoaded;
    }

    private static void UnsubscribeBackend()
    {
        if (backend != null)
            backend.OnDataLoaded -= RelayDataLoaded;
    }

    private static void RelayDataLoaded(GameData data, int slotIndex)
    {
        OnDataLoaded?.Invoke(data, slotIndex);
    }

    private static bool IsBackendAlive(IGameDataStoreBackend storeBackend)
    {
        if (storeBackend == null)
            return false;

        if (storeBackend is Component component)
            return component != null;

        return true;
    }
}

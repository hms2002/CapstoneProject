using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - UI 계층에서 상태 HUD source registry에 접근하는 facade를 제공한다.
/// - HUD presenter가 상태 소유자를 직접 추적하지 않고도 Core registry를 통해 현재 표시 목록을 다시 읽게 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StatusHudService : MonoBehaviour
{
    private static StatusHudService instance;
    private readonly Dictionary<(string Owner, string Status), int> entryIndices = new();
    public static StatusHudService Current => instance;
    public static StatusHudService Instance => EnsureInstance();

    public static StatusHudService EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<StatusHudService>();
        if (instance != null)
            return instance;

        GameObject root = new("StatusHudService");
        instance = root.AddComponent<StatusHudService>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        GlobalUIRoot.AdoptService(transform);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public void RegisterSource(IStatusHudSource source)
    {
        StatusHudSourceRegistry.RegisterSource(source);
    }

    public void UnregisterSource(IStatusHudSource source)
    {
        StatusHudSourceRegistry.UnregisterSource(source);
    }

    public void CollectEntries(List<StatusHudEntry> buffer)
    {
        StatusHudSourceRegistry.CollectEntries(buffer);
        if (buffer == null)
            return;

        // Collapse duplicate projections, not independent owners or gameplay stacks.
        entryIndices.Clear();
        int count = 0;
        for (int i = 0; i < buffer.Count; i++)
        {
            StatusHudEntry entry = buffer[i];
            if (!entry.IsVisible)
                continue;
            if (string.IsNullOrEmpty(entry.OwnerKey) || string.IsNullOrEmpty(entry.StatusId))
            {
                buffer[count++] = entry;
                continue;
            }

            var key = (entry.OwnerKey, entry.StatusId);
            if (entryIndices.TryGetValue(key, out int index))
            {
                if (entry.RemainingTime > buffer[index].RemainingTime)
                    buffer[index] = entry;
                continue;
            }

            entryIndices.Add(key, count);
            buffer[count++] = entry;
        }

        if (count < buffer.Count)
            buffer.RemoveRange(count, buffer.Count - count);
    }
}

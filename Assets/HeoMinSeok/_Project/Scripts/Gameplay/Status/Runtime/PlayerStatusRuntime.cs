using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어에게 현재 적용 중인 상태를 등록, 갱신, 해제하는 허브 역할을 맡는다.
/// - 상태의 진짜 소유자들은 handle 기반으로 Apply/Release만 호출하고, HUD나 다른 소비자는 여기서 활성 상태 목록을 읽게 만든다.
/// - 선택적으로 상태 태그를 `TagSystem`에 부여/회수해 존재 플래그와 HUD 수명을 같은 handle 기준으로 묶는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStatusRuntime : MonoBehaviour
{
    [SerializeField] private bool logStatusLifecycle = true;
    [SerializeField] private TagSystem tagSystem;

    private readonly Dictionary<int, ActiveStatusEntry> activeEntries = new();
    private int nextRuntimeId = 1;

    public int ActiveStatusCount => activeEntries.Count;

    private void Awake()
    {
        tagSystem ??= GetComponent<TagSystem>();
    }

    public static PlayerStatusRuntime GetOrAdd(GameObject owner)
    {
        if (owner == null)
            return null;

        PlayerStatusRuntime existing = owner.GetComponent<PlayerStatusRuntime>();
        return existing != null ? existing : owner.AddComponent<PlayerStatusRuntime>();
    }

    public StatusHandle Apply(in StatusApplyRequest request)
    {
        if (request.Definition == null)
        {
            if (logStatusLifecycle)
                Debug.LogWarning("[PlayerStatusRuntime] Ignored Apply because definition was null.", this);
            return default;
        }

        int runtimeId = nextRuntimeId++;
        ActiveStatusEntry entry = new(runtimeId, request);
        activeEntries[runtimeId] = entry;
        ApplyEntryTag(entry);

        if (logStatusLifecycle)
            Debug.Log($"[PlayerStatusRuntime] Applied status '{request.Definition.StatusId}' (id={runtimeId}, ownerKey={entry.OwnerKey})", this);

        return new StatusHandle(this, runtimeId);
    }

    public bool UpdateStatus(StatusHandle handle, in StatusApplyRequest request)
    {
        if (!TryGetEntry(handle, out ActiveStatusEntry entry))
        {
            if (logStatusLifecycle)
                Debug.LogWarning($"[PlayerStatusRuntime] Failed to update status because handle {handle.RuntimeId} was not active.", this);
            return false;
        }

        GameplayTag previousTag = entry.StateTag;
        entry.Apply(request);
        UpdateEntryTag(previousTag, entry.StateTag);

        if (logStatusLifecycle && entry.Definition != null)
            Debug.Log($"[PlayerStatusRuntime] Updated status '{entry.Definition.StatusId}' (id={handle.RuntimeId})", this);

        return true;
    }

    public bool Release(StatusHandle handle)
    {
        if (!handle.IsValid)
        {
            if (logStatusLifecycle)
                Debug.LogWarning("[PlayerStatusRuntime] Ignored Release because handle was invalid.", this);
            return false;
        }

        if (!activeEntries.TryGetValue(handle.RuntimeId, out ActiveStatusEntry entry))
        {
            if (logStatusLifecycle)
                Debug.LogWarning($"[PlayerStatusRuntime] Ignored Release because handle {handle.RuntimeId} was not found.", this);
            return false;
        }

        activeEntries.Remove(handle.RuntimeId);
        RemoveEntryTag(entry);

        if (logStatusLifecycle && entry.Definition != null)
            Debug.Log($"[PlayerStatusRuntime] Released status '{entry.Definition.StatusId}' (id={handle.RuntimeId})", this);

        return true;
    }

    public void CollectStatusHudEntries(List<StatusHudEntry> buffer)
    {
        if (buffer == null || activeEntries.Count == 0)
            return;

        foreach (ActiveStatusEntry entry in activeEntries.Values)
        {
            StatusHudEntry hudEntry = entry.ToHudEntry();
            if (hudEntry.IsVisible)
                buffer.Add(hudEntry);
        }
    }

    public void ClearAll()
    {
        foreach (ActiveStatusEntry entry in activeEntries.Values)
            RemoveEntryTag(entry);

        activeEntries.Clear();
    }

    private bool TryGetEntry(StatusHandle handle, out ActiveStatusEntry entry)
    {
        entry = null;

        if (!handle.IsValid)
            return false;

        return activeEntries.TryGetValue(handle.RuntimeId, out entry);
    }

    private void ApplyEntryTag(ActiveStatusEntry entry)
    {
        if (entry == null || entry.StateTag == null)
            return;

        tagSystem ??= GetComponent<TagSystem>();
        tagSystem?.AddTag(entry.StateTag);
    }

    private void RemoveEntryTag(ActiveStatusEntry entry)
    {
        if (entry == null || entry.StateTag == null)
            return;

        tagSystem ??= GetComponent<TagSystem>();
        tagSystem?.RemoveTag(entry.StateTag);
    }

    private void UpdateEntryTag(GameplayTag previousTag, GameplayTag nextTag)
    {
        if (previousTag == nextTag)
            return;

        tagSystem ??= GetComponent<TagSystem>();
        if (tagSystem == null)
            return;

        if (previousTag != null)
            tagSystem.RemoveTag(previousTag);

        if (nextTag != null)
            tagSystem.AddTag(nextTag);
    }
}

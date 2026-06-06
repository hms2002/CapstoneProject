using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 상태 HUD source의 등록/해제를 관리하고, 현재 활성 source들로부터 HUD 엔트리를 수집하는 중앙 서비스를 제공한다.
/// - HUD가 상태 소유자를 직접 추적하지 않고도 같은 진입점으로 현재 표시 목록을 다시 읽게 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StatusHudService : MonoBehaviour
{
    private static StatusHudService instance;
    private readonly HashSet<IStatusHudSource> sources = new();

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
        if (source != null)
            sources.Add(source);
    }

    public void UnregisterSource(IStatusHudSource source)
    {
        if (source != null)
            sources.Remove(source);
    }

    public void CollectEntries(List<StatusHudEntry> buffer)
    {
        if (buffer == null)
            return;

        buffer.Clear();

        if (sources.Count == 0)
            return;

        foreach (IStatusHudSource source in sources)
            source?.CollectStatusHudEntries(buffer);
    }
}

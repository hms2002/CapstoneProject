using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - PlayerStatusRuntime에 등록된 일반 상태를 공용 HUD 엔트리 목록으로 투영한다.
/// - 무기 전용 source와 별개로 지역 디버프, 유물 버프, 플레이어 상태를 같은 HUD 파이프라인으로 노출하게 만든다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerStatusHudSource : MonoBehaviour, IStatusHudSource
{
    [SerializeField] private PlayerStatusRuntime runtime;
    [SerializeField] private bool logStatusHudCollection = true;

    private int lastCollectedCount = -1;

    public static PlayerStatusHudSource GetOrAdd(GameObject owner)
    {
        if (owner == null)
            return null;

        PlayerStatusHudSource existing = owner.GetComponent<PlayerStatusHudSource>();
        return existing != null ? existing : owner.AddComponent<PlayerStatusHudSource>();
    }

    private void Awake()
    {
        runtime ??= GetComponent<PlayerStatusRuntime>();
    }

    private void OnEnable()
    {
        runtime ??= GetComponent<PlayerStatusRuntime>();
        StatusHudSourceRegistry.RegisterSource(this);

        if (logStatusHudCollection)
            Debug.Log($"[PlayerStatusHudSource] Registered. runtime={(runtime != null ? runtime.name : "null")}", this);
    }

    private void OnDisable()
    {
        StatusHudSourceRegistry.UnregisterSource(this);
    }

    public void CollectStatusHudEntries(List<StatusHudEntry> buffer)
    {
        if (runtime == null)
        {
            if (logStatusHudCollection)
                Debug.LogWarning("[PlayerStatusHudSource] Skipped collection because PlayerStatusRuntime was missing.", this);
            return;
        }

        int beforeCount = buffer != null ? buffer.Count : 0;
        runtime?.CollectStatusHudEntries(buffer);

        if (buffer == null)
            return;

        int collectedCount = buffer.Count - beforeCount;
        if (!logStatusHudCollection || lastCollectedCount == collectedCount)
            return;

        lastCollectedCount = collectedCount;
        Debug.Log($"[PlayerStatusHudSource] Collected {collectedCount} player status HUD entr{(collectedCount == 1 ? "y" : "ies")} (activeRuntimeStatuses={runtime.ActiveStatusCount}).", this);
    }
}

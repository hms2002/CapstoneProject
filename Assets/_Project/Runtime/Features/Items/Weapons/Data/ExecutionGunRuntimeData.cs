using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 처형총 슬롯이 최근 소비한 표식 수, 반격 검격 개방 여부, 개방 지속 시간을 persistent state로 보관한다.
/// - 검과 총이 서로의 상태를 참조하는 왕복 규칙에서 총 쪽 결과를 장착 여부와 무관하게 유지한다.
/// </summary>
[Serializable]
public sealed class ExecutionGunRuntimeData : WeaponRuntimeData, IWeaponRuntimeStatePersistence
{
    [Serializable]
    private struct PersistedState
    {
        public int lastConsumedMarkCount;
        public bool reboundSlashReady;
        public float reboundWindowRemaining;
    }

    private int requiredMarksForExecutionShot = 3;
    private float reboundWindowSeconds = 6f;
    private int lastConsumedMarkCount;
    private bool reboundSlashReady;
    private float reboundWindowRemaining;

    public int RequiredMarksForExecutionShot => Mathf.Max(1, requiredMarksForExecutionShot);
    public float ReboundWindowSeconds => Mathf.Max(0f, reboundWindowSeconds);
    public int LastConsumedMarkCount => lastConsumedMarkCount;
    public bool ReboundSlashReady => reboundSlashReady;
    public float ReboundWindowRemaining => reboundWindowRemaining;
    public string StateType => nameof(ExecutionGunRuntimeData);

    public void ApplyDefaults(ExecutionGunLoadout loadout)
    {
        if (loadout == null)
            return;

        requiredMarksForExecutionShot = loadout.RequiredMarksForExecutionShot;
        reboundWindowSeconds = loadout.ReboundWindowSeconds;
        lastConsumedMarkCount = 0;
        reboundSlashReady = false;
        reboundWindowRemaining = 0f;
    }

    public void OpenReboundSlashWindow(int consumedMarkCount)
    {
        lastConsumedMarkCount = Mathf.Max(0, consumedMarkCount);
        reboundSlashReady = consumedMarkCount > 0;
        reboundWindowRemaining = reboundSlashReady ? ReboundWindowSeconds : 0f;
    }

    public void CloseReboundSlashWindow()
    {
        reboundSlashReady = false;
        reboundWindowRemaining = 0f;
    }

    public void SetReboundWindowRemaining(float remaining)
    {
        reboundWindowRemaining = Mathf.Max(0f, remaining);
    }

    public string CaptureStateJson()
    {
        return JsonUtility.ToJson(new PersistedState
        {
            lastConsumedMarkCount = lastConsumedMarkCount,
            reboundSlashReady = reboundSlashReady,
            reboundWindowRemaining = reboundWindowRemaining
        });
    }

    public void RestoreStateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        PersistedState state = JsonUtility.FromJson<PersistedState>(json);
        lastConsumedMarkCount = Mathf.Max(0, state.lastConsumedMarkCount);
        reboundSlashReady = state.reboundSlashReady;
        reboundWindowRemaining = Mathf.Max(0f, state.reboundWindowRemaining);
    }
}

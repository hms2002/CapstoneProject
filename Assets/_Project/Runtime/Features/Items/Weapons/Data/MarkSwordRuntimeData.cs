using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 표식검 슬롯이 소유하는 표식 스택과 스택 감쇠 타이머를 persistent state로 보관한다.
/// - 총과의 상호작용 결과를 장착 여부와 무관하게 유지해 쌍무기 조합 규칙의 진실한 상태 저장소 역할을 한다.
/// </summary>
[Serializable]
public sealed class MarkSwordRuntimeData : WeaponRuntimeData, IWeaponRuntimeStatePersistence
{
    [Serializable]
    private struct PersistedState
    {
        public int markStacks;
        public float markDecayRemaining;
    }

    private int maxMarkStacks = 3;
    private float markDecaySeconds = 5f;
    private int markStacks;
    private float markDecayRemaining;

    public int MaxMarkStacks => Mathf.Max(1, maxMarkStacks);
    public float MarkDecaySeconds => Mathf.Max(0f, markDecaySeconds);
    public int MarkStacks => markStacks;
    public float MarkDecayRemaining => markDecayRemaining;
    public string StateType => nameof(MarkSwordRuntimeData);

    public void ApplyDefaults(MarkSwordLoadout loadout)
    {
        if (loadout == null)
            return;

        maxMarkStacks = loadout.MaxMarkStacks;
        markDecaySeconds = loadout.MarkDecaySeconds;
        markStacks = 0;
        markDecayRemaining = 0f;
    }

    public void AddMarkStack()
    {
        markStacks = Mathf.Clamp(markStacks + 1, 0, MaxMarkStacks);
        markDecayRemaining = markStacks > 0 ? MarkDecaySeconds : 0f;
    }

    public void ConsumeOneMarkStack()
    {
        if (markStacks <= 0)
            return;

        markStacks = Mathf.Max(0, markStacks - 1);
        markDecayRemaining = markStacks > 0 ? MarkDecaySeconds : 0f;
    }

    public void ClearMarks()
    {
        markStacks = 0;
        markDecayRemaining = 0f;
    }

    public void SetMarkDecayRemaining(float remaining)
    {
        markDecayRemaining = Mathf.Max(0f, remaining);
    }

    public string CaptureStateJson()
    {
        return JsonUtility.ToJson(new PersistedState
        {
            markStacks = markStacks,
            markDecayRemaining = markDecayRemaining
        });
    }

    public void RestoreStateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        PersistedState state = JsonUtility.FromJson<PersistedState>(json);
        markStacks = Mathf.Clamp(state.markStacks, 0, MaxMarkStacks);
        markDecayRemaining = Mathf.Max(0f, state.markDecayRemaining);
    }
}

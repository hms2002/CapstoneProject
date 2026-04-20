using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 월영도 슬롯이 소유하는 냉기 스택과 감쇠 타이머를 persistent runtime state로 보관한다.
/// - 태양도가 읽는 "월영도 냉기 상태"를 장착 여부와 무관하게 유지해 쌍무기 상호참조의 진실한 저장소 역할을 한다.
/// </summary>
[Serializable]
public sealed class MoonBladeRuntimeData : WeaponRuntimeData, IWeaponRuntimeStatePersistence
{
    [Serializable]
    private struct PersistedState
    {
        public int coldStacks;
        public float coldDecayRemaining;
    }

    private int maxColdStacks = 3;
    private float coldDecaySeconds = 5f;
    private int coldStacks;
    private float coldDecayRemaining;

    public int MaxColdStacks => Mathf.Max(1, maxColdStacks);
    public float ColdDecaySeconds => Mathf.Max(0f, coldDecaySeconds);
    public int ColdStacks => coldStacks;
    public float ColdDecayRemaining => coldDecayRemaining;
    public string StateType => nameof(MoonBladeRuntimeData);

    public void ApplyDefaults(MoonBladeLoadout loadout)
    {
        if (loadout == null)
            return;

        maxColdStacks = loadout.MaxColdStacks;
        coldDecaySeconds = loadout.ColdDecaySeconds;
        coldStacks = 0;
        coldDecayRemaining = 0f;
    }

    public void AddColdStack()
    {
        coldStacks = Mathf.Clamp(coldStacks + 1, 0, MaxColdStacks);
        coldDecayRemaining = coldStacks > 0 ? ColdDecaySeconds : 0f;
    }

    public void ConsumeOneColdStack()
    {
        if (coldStacks <= 0)
            return;

        coldStacks = Mathf.Max(0, coldStacks - 1);
        coldDecayRemaining = coldStacks > 0 ? ColdDecaySeconds : 0f;
    }

    public void ClearCold()
    {
        coldStacks = 0;
        coldDecayRemaining = 0f;
    }

    public void SetColdDecayRemaining(float remaining)
    {
        coldDecayRemaining = Mathf.Max(0f, remaining);
    }

    public string CaptureStateJson()
    {
        return JsonUtility.ToJson(new PersistedState
        {
            coldStacks = coldStacks,
            coldDecayRemaining = coldDecayRemaining
        });
    }

    public void RestoreStateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        PersistedState state = JsonUtility.FromJson<PersistedState>(json);
        coldStacks = Mathf.Clamp(state.coldStacks, 0, MaxColdStacks);
        coldDecayRemaining = Mathf.Max(0f, state.coldDecayRemaining);
    }
}

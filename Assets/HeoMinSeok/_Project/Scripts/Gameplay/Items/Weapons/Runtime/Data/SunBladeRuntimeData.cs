using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 태양도 슬롯이 소유하는 열기 스택과 감쇠 타이머를 persistent runtime state로 보관한다.
/// - 월영도가 읽는 "태양도 열기 상태"를 장착 여부와 무관하게 유지해 쌍무기 상호참조의 진실한 저장소 역할을 한다.
/// </summary>
[Serializable]
public sealed class SunBladeRuntimeData : WeaponRuntimeData, IWeaponRuntimeStatePersistence
{
    [Serializable]
    private struct PersistedState
    {
        public int heatStacks;
        public float heatDecayRemaining;
    }

    private int maxHeatStacks = 3;
    private float heatDecaySeconds = 5f;
    private int heatStacks;
    private float heatDecayRemaining;

    public int MaxHeatStacks => Mathf.Max(1, maxHeatStacks);
    public float HeatDecaySeconds => Mathf.Max(0f, heatDecaySeconds);
    public int HeatStacks => heatStacks;
    public float HeatDecayRemaining => heatDecayRemaining;
    public string StateType => nameof(SunBladeRuntimeData);

    public void ApplyDefaults(SunBladeLoadout loadout)
    {
        if (loadout == null)
            return;

        maxHeatStacks = loadout.MaxHeatStacks;
        heatDecaySeconds = loadout.HeatDecaySeconds;
        heatStacks = 0;
        heatDecayRemaining = 0f;
    }

    public void AddHeatStack()
    {
        heatStacks = Mathf.Clamp(heatStacks + 1, 0, MaxHeatStacks);
        heatDecayRemaining = heatStacks > 0 ? HeatDecaySeconds : 0f;
    }

    public void ConsumeOneHeatStack()
    {
        if (heatStacks <= 0)
            return;

        heatStacks = Mathf.Max(0, heatStacks - 1);
        heatDecayRemaining = heatStacks > 0 ? HeatDecaySeconds : 0f;
    }

    public void ClearHeat()
    {
        heatStacks = 0;
        heatDecayRemaining = 0f;
    }

    public void SetHeatDecayRemaining(float remaining)
    {
        heatDecayRemaining = Mathf.Max(0f, remaining);
    }

    public string CaptureStateJson()
    {
        return JsonUtility.ToJson(new PersistedState
        {
            heatStacks = heatStacks,
            heatDecayRemaining = heatDecayRemaining
        });
    }

    public void RestoreStateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        PersistedState state = JsonUtility.FromJson<PersistedState>(json);
        heatStacks = Mathf.Clamp(state.heatStacks, 0, MaxHeatStacks);
        heatDecayRemaining = Mathf.Max(0f, state.heatDecayRemaining);
    }
}

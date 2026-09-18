using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 책임:
/// - 플레이어가 장착한 유물의 일반 적/보스 처치 보상 배율을 token 기준으로 보관한다.
/// - 소수 보너스를 누적해 작은 EXP 보상에서 반올림으로 레벨별 배율이 왜곡되는 것을 막는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RelicKillRewardMultiplierRuntime : MonoBehaviour
{
    private sealed class Registration
    {
        public KillRewardRelicKind kind;
        public float bonusRate;
        public float fractionalRemainder;
    }

    private readonly Dictionary<Object, Registration> registrations = new();

    public void Register(Object token, KillRewardRelicKind kind, float bonusRate)
    {
        if (token == null)
            return;

        float previousRemainder = registrations.TryGetValue(token, out Registration previous)
            ? previous.fractionalRemainder
            : 0f;

        registrations[token] = new Registration
        {
            kind = kind,
            bonusRate = Mathf.Max(0f, bonusRate),
            fractionalRemainder = Mathf.Clamp(previousRemainder, 0f, 0.999999f)
        };
    }

    public void Unregister(Object token)
    {
        if (token != null)
            registrations.Remove(token);
    }

    public int ApplyGoldReward(int baseAmount) => Apply(baseAmount, KillRewardRelicKind.Gold);

    public int ApplyExperienceReward(int baseAmount) => Apply(baseAmount, KillRewardRelicKind.Experience);

    public bool TryGetFractionalRemainder(Object token, out float remainder)
    {
        remainder = 0f;
        if (token == null || !registrations.TryGetValue(token, out Registration registration))
            return false;

        remainder = registration.fractionalRemainder;
        return true;
    }

    public void RestoreFractionalRemainder(Object token, float remainder)
    {
        if (token == null || !registrations.TryGetValue(token, out Registration registration))
            return;

        registration.fractionalRemainder = Mathf.Clamp(remainder, 0f, 0.999999f);
    }

    private int Apply(int baseAmount, KillRewardRelicKind kind)
    {
        int safeBaseAmount = Mathf.Max(0, baseAmount);
        if (safeBaseAmount <= 0)
            return 0;

        int bonus = 0;
        foreach (Registration registration in registrations.Values)
        {
            if (registration.kind != kind || registration.bonusRate <= 0f)
                continue;

            float exactBonus = safeBaseAmount * registration.bonusRate + registration.fractionalRemainder;
            int wholeBonus = Mathf.FloorToInt(exactBonus + 0.000001f);
            registration.fractionalRemainder = Mathf.Clamp(exactBonus - wholeBonus, 0f, 0.999999f);
            bonus += Mathf.Max(0, wholeBonus);
        }

        return safeBaseAmount + bonus;
    }
}

public enum KillRewardRelicKind
{
    Gold = 0,
    Experience = 1
}

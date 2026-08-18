using System;
using UnityEngine;

/// <summary>
/// 실제 체력이 감소한 전투 피해를 런타임 기능에 전달하는 공통 읽기 전용 신호다.
/// </summary>
public static class CombatActivityEvents
{
    public static event Action<GameObject, GameObject, float> DamageApplied;

    internal static void RaiseDamageApplied(GameObject source, GameObject target, float amount)
    {
        if (target == null || amount <= 0f)
            return;

        DamageApplied?.Invoke(source, target, amount);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        DamageApplied = null;
    }
}

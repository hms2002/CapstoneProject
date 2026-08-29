using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

public readonly struct CombatOutgoingDamageContext
{
    public CombatOutgoingDamageContext(
        AbilitySystem sourceSystem,
        AbilitySpec sourceSpec,
        GameObject target,
        float baseDamage)
    {
        SourceSystem = sourceSystem;
        SourceSpec = sourceSpec;
        Target = target;
        BaseDamage = baseDamage;
    }

    public AbilitySystem SourceSystem { get; }
    public AbilitySpec SourceSpec { get; }
    public GameObject Target { get; }
    public float BaseDamage { get; }
}

/// <summary>
/// 플레이어 보상처럼 제한된 런타임 기능이 최종 outgoing damage를 조정하는 확장 지점이다.
/// </summary>
public static class CombatOutgoingDamageModifiers
{
    private static readonly List<Func<CombatOutgoingDamageContext, float>> Modifiers = new();

    public static IDisposable Register(Func<CombatOutgoingDamageContext, float> modifier)
    {
        if (modifier == null)
            return EmptyHandle.Instance;

        Modifiers.Add(modifier);
        return new Registration(modifier);
    }

    internal static float Apply(CombatOutgoingDamageContext context)
    {
        float result = Mathf.Max(0f, context.BaseDamage);
        for (int i = 0; i < Modifiers.Count; i++)
        {
            Func<CombatOutgoingDamageContext, float> modifier = Modifiers[i];
            if (modifier == null)
                continue;

            result = Mathf.Max(0f, modifier(new CombatOutgoingDamageContext(
                context.SourceSystem,
                context.SourceSpec,
                context.Target,
                result)));
        }

        return result;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Modifiers.Clear();
    }

    private sealed class Registration : IDisposable
    {
        private Func<CombatOutgoingDamageContext, float> modifier;

        public Registration(Func<CombatOutgoingDamageContext, float> modifier)
        {
            this.modifier = modifier;
        }

        public void Dispose()
        {
            if (modifier == null)
                return;

            Modifiers.Remove(modifier);
            modifier = null;
        }
    }

    private sealed class EmptyHandle : IDisposable
    {
        public static readonly EmptyHandle Instance = new();
        public void Dispose() { }
    }
}

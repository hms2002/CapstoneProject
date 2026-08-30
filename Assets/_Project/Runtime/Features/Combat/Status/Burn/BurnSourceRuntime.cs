using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>화상 규칙의 공격자 측 런타임 값을 소유합니다. 유물은 추후 token API만 사용해 확장할 수 있습니다.</summary>
[DisallowMultipleComponent]
public sealed class BurnSourceRuntime : MonoBehaviour
{
    public readonly struct Modifier
    {
        public readonly float TickIntervalMultiplier;
        public readonly float DamageRatioAdd;
        public readonly int ApplicationAdd;
        public readonly int FirstApplicationAdd;
        public readonly bool AllowCritical;
        public readonly int StackDamageThreshold;
        public readonly float StackDamageRatioPerStep;
        public readonly float StackDamageRatioMax;

        public Modifier(
            float tickIntervalMultiplier,
            float damageRatioAdd,
            int applicationAdd,
            int firstApplicationAdd,
            bool allowCritical,
            int stackDamageThreshold = 0,
            float stackDamageRatioPerStep = 0f,
            float stackDamageRatioMax = 0f)
        {
            TickIntervalMultiplier = tickIntervalMultiplier;
            DamageRatioAdd = damageRatioAdd;
            ApplicationAdd = applicationAdd;
            FirstApplicationAdd = firstApplicationAdd;
            AllowCritical = allowCritical;
            StackDamageThreshold = stackDamageThreshold;
            StackDamageRatioPerStep = stackDamageRatioPerStep;
            StackDamageRatioMax = stackDamageRatioMax;
        }
    }

    private readonly Dictionary<object, Modifier> modifiers = new();

    public float TickInterval
    {
        get
        {
            float multiplier = 1f;
            foreach (Modifier modifier in modifiers.Values)
                multiplier *= modifier.TickIntervalMultiplier <= 0f ? 1f : modifier.TickIntervalMultiplier;
            return Mathf.Max(0.05f, multiplier);
        }
    }

    public float DamageRatio
    {
        get
        {
            float value = 0.5f;
            foreach (Modifier modifier in modifiers.Values)
                value += modifier.DamageRatioAdd;
            return Mathf.Max(0f, value);
        }
    }

    public bool AllowCritical
    {
        get
        {
            foreach (Modifier modifier in modifiers.Values)
                if (modifier.AllowCritical) return true;
            return false;
        }
    }

    public int ResolveApplicationStacks(int baseStacks, bool isFirstApplication)
    {
        int value = baseStacks;
        foreach (Modifier modifier in modifiers.Values)
        {
            value += modifier.ApplicationAdd;
            if (isFirstApplication)
                value += modifier.FirstApplicationAdd;
        }
        return Mathf.Max(0, value);
    }

    public float ResolveStackDamageMultiplier(int currentStacks)
    {
        float multiplier = 1f;
        foreach (Modifier modifier in modifiers.Values)
        {
            if (modifier.StackDamageThreshold <= 0 || modifier.StackDamageRatioPerStep <= 0f)
                continue;

            int steps = Mathf.Max(0, currentStacks) / modifier.StackDamageThreshold;
            float bonus = steps * modifier.StackDamageRatioPerStep;
            if (modifier.StackDamageRatioMax > 0f)
                bonus = Mathf.Min(bonus, modifier.StackDamageRatioMax);
            multiplier *= 1f + bonus;
        }

        return Mathf.Max(0f, multiplier);
    }

    public void SetModifier(object token, Modifier modifier)
    {
        if (token != null) modifiers[token] = modifier;
    }

    public void RemoveModifier(object token)
    {
        if (token != null) modifiers.Remove(token);
    }

    public static BurnSourceRuntime Resolve(AbilitySystem system)
    {
        if (system == null) return null;
        BurnSourceRuntime runtime = system.GetComponent<BurnSourceRuntime>();
        return runtime != null ? runtime : system.gameObject.AddComponent<BurnSourceRuntime>();
    }
}

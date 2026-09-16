using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    public readonly struct ElementBuildUpModifierContext
    {
        public ElementBuildUpModifierContext(
            GameObject attacker,
            GameObject target,
            GameplayTag elementType,
            float baseAmount)
        {
            Attacker = attacker;
            Target = target;
            ElementType = elementType;
            BaseAmount = baseAmount;
        }

        public GameObject Attacker { get; }
        public GameObject Target { get; }
        public GameplayTag ElementType { get; }
        public float BaseAmount { get; }
    }

    public static class ElementBuildUpModifiers
    {
        private static readonly List<Func<ElementBuildUpModifierContext, float>> Modifiers = new();

        public static IDisposable Register(Func<ElementBuildUpModifierContext, float> modifier)
        {
            if (modifier == null)
                return EmptyHandle.Instance;

            Modifiers.Add(modifier);
            return new Registration(modifier);
        }

        internal static float Apply(ElementBuildUpModifierContext context)
        {
            float result = Mathf.Max(0f, context.BaseAmount);
            for (int i = 0; i < Modifiers.Count; i++)
            {
                Func<ElementBuildUpModifierContext, float> modifier = Modifiers[i];
                if (modifier == null)
                    continue;

                result = Mathf.Max(0f, modifier(new ElementBuildUpModifierContext(
                    context.Attacker,
                    context.Target,
                    context.ElementType,
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
            private Func<ElementBuildUpModifierContext, float> modifier;

            public Registration(Func<ElementBuildUpModifierContext, float> modifier)
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

    public static class ElementBuildUpResolver
    {
        public static List<ElementDamageResult> ResolveForApplication(
            GameObject attacker,
            GameObject target,
            List<ElementDamageResult> buffer)
        {
            return Evaluate(attacker, target, buffer);
        }

        public static List<ElementDamageResult> Evaluate(
            GameObject attacker,
            GameObject target,
            List<ElementDamageResult> buffer = null)
        {
            if (buffer == null)
                buffer = new List<ElementDamageResult>();

            buffer.Clear();

            if (attacker == null)
                return buffer;

            var source = attacker.GetComponent<ElementOffenseSource>();
            if (source == null || !source.ApplyToAllDamage)
                return buffer;

            var profile = source.Profile;
            if (profile == null || profile.formulas == null || profile.formulas.Length == 0)
                return buffer;

            // 핵심 수정:
            // AttributeStatProvider는 MonoBehaviour가 아니므로 직접 GetComponent 하지 않는다.
            // Unity 쪽 브리지인 AttributeStatSource가 구현하는 IStatProvider를 찾는다.
            var statProvider = attacker.GetComponent<IStatProvider>();
            if (statProvider == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[ElementBuildUpResolver] '{attacker.name}' 에 IStatProvider 가 없습니다. " +
                    "(예: AttributeStatSource) 자동 속성 누적 계산을 건너뜁니다.",
                    attacker);
#endif
                return buffer;
            }

            float targetMultiplier = 1f;
            if (target != null && profile.groggyTag != null)
            {
                var tagSystem = target.GetComponent<TagSystem>();
                if (tagSystem != null && tagSystem.HasTag(profile.groggyTag))
                    targetMultiplier = Mathf.Max(0f, profile.groggyMultiplier);
            }

            for (int i = 0; i < profile.formulas.Length; i++)
            {
                var entry = profile.formulas[i];
                if (entry == null || !entry.enabled || entry.elementType == null)
                    continue;

                float stat = statProvider.Get(entry.sourceStatId);

                stat = Mathf.Max(0f, stat);
                if (stat <= 0f)
                    continue;

                float amount =
                    profile.baseValue +
                    (stat * profile.maxCap) / (stat + Mathf.Max(0.0001f, profile.curveConstant));

                amount = amount * Mathf.Max(0f, entry.multiplier) + entry.flatBonus;
                amount *= targetMultiplier;
                amount = ElementBuildUpModifiers.Apply(
                    new ElementBuildUpModifierContext(attacker, target, entry.elementType, amount));

                if (amount <= 0f)
                    continue;

                buffer.Add(new ElementDamageResult
                {
                    elementType = entry.elementType,
                    damage = amount
                });
            }

            return buffer;
        }
    }
}

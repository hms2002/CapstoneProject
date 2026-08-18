using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// AbilitySystem의 쿨다운/차지 관련 로직 전담.
    /// - 일반 쿨다운 시작/조회/감소
    /// - charge 스킬 재충전 tick
    /// - GE 기반 쿨다운 / fallback 쿨다운 지원
    /// </summary>
    public sealed class AbilityCooldownController
    {
        private readonly AbilitySystem owner;
        private readonly GameplayEffectRunner effectRunner;
        private readonly AttributeSet attributeSet;
        private readonly GameplayEffect defaultCooldownEffect;
        private readonly AttributeDefinition cooldownDurationMultiplierAttribute;
        private readonly AttributeDefinition cooldownFlatReduceSecondsOnHitAttribute;
        private readonly float minCooldownSeconds;
        private readonly List<ScopedCooldownMultiplier> scopedDurationMultipliers =
            new List<ScopedCooldownMultiplier>();

        private const string KEY_CHARGES = "__Charges";
        private const string KEY_RECHARGE = "__RechargeRemaining";

        public AbilityCooldownController(
            AbilitySystem owner,
            GameplayEffectRunner effectRunner,
            AttributeSet attributeSet,
            GameplayEffect defaultCooldownEffect,
            AttributeDefinition cooldownDurationMultiplierAttribute,
            AttributeDefinition cooldownFlatReduceSecondsOnHitAttribute,
            float minCooldownSeconds)
        {
            this.owner = owner;
            this.effectRunner = effectRunner;
            this.attributeSet = attributeSet;
            this.defaultCooldownEffect = defaultCooldownEffect;
            this.cooldownDurationMultiplierAttribute = cooldownDurationMultiplierAttribute;
            this.cooldownFlatReduceSecondsOnHitAttribute = cooldownFlatReduceSecondsOnHitAttribute;
            this.minCooldownSeconds = minCooldownSeconds;
        }

        public void StartCooldown(AbilitySpec spec)
        {
            var def = spec?.Definition;
            if (def == null || def.useCharges)
                return;

            float finalCooldown = GetFinalCooldownSeconds(def);
            if (finalCooldown <= 0f)
                return;

            if (defaultCooldownEffect != null && effectRunner != null && owner != null)
            {
                var cdSpec = owner.MakeSpec(defaultCooldownEffect, causer: owner.gameObject, sourceObject: def);
                cdSpec.SetDuration(finalCooldown);
                effectRunner.ApplyEffectSpec(cdSpec, owner.gameObject);
                return;
            }

            spec.CooldownRemaining = finalCooldown;
        }

        public IDisposable AddScopedDurationMultiplier(
            Func<AbilityDefinition, bool> appliesTo,
            float multiplier)
        {
            if (appliesTo == null || multiplier <= 0f)
                return null;

            var entry = new ScopedCooldownMultiplier(appliesTo, multiplier);
            scopedDurationMultipliers.Add(entry);
            return new ScopedCooldownMultiplierHandle(scopedDurationMultipliers, entry);
        }

        public bool IsOnCooldown(AbilitySpec spec)
        {
            if (spec == null)
                return false;

            var def = spec.Definition;

            if (def != null && def.useCharges)
                return spec.GetInt(KEY_CHARGES, 0) <= 0;

            if (defaultCooldownEffect != null && effectRunner != null && owner != null)
                return effectRunner.HasActiveEffect(defaultCooldownEffect, owner.gameObject, def);

            return spec.CooldownRemaining > 0f;
        }

        public float GetCooldownRemaining(AbilityDefinition ability)
        {
            if (owner == null)
                return 0f;

            var spec = owner.FindSpec(ability);
            if (spec == null)
                return 0f;

            var def = spec.Definition;

            if (def != null && def.useCharges)
                return spec.GetFloat(KEY_RECHARGE, 0f);

            if (defaultCooldownEffect != null && effectRunner != null)
                return effectRunner.GetRemainingTime(defaultCooldownEffect, owner.gameObject, def);

            return Mathf.Max(0f, spec.CooldownRemaining);
        }

        public void TickCooldowns(System.Collections.Generic.IReadOnlyList<AbilitySpec> runtimeSpecs)
        {
            if (runtimeSpecs == null || runtimeSpecs.Count == 0)
                return;

            for (int i = 0; i < runtimeSpecs.Count; i++)
            {
                var spec = runtimeSpecs[i];
                var def = spec.Definition;

                if (def != null && def.useCharges)
                {
                    TickChargeCooldown(spec, def);
                    continue;
                }

                if (defaultCooldownEffect != null)
                    continue;

                if (spec.CooldownRemaining > 0f)
                    spec.CooldownRemaining -= Time.deltaTime;
            }
        }

        public bool ReduceCooldownRemaining(AbilityDefinition def, float reduceSeconds)
        {
            if (owner == null || def == null || reduceSeconds <= 0f || def.useCharges)
                return false;

            if (defaultCooldownEffect != null && effectRunner != null)
            {
                int affected = effectRunner.ReduceRemainingTimeBySourceObject(
                    owner.gameObject,
                    defaultCooldownEffect,
                    def,
                    reduceSeconds);

                return affected > 0;
            }

            var spec = owner.FindSpec(def);
            if (spec == null)
                return false;

            spec.CooldownRemaining = Mathf.Max(0f, spec.CooldownRemaining - reduceSeconds);
            return true;
        }

        public bool ReduceCooldownRemainingOnHit(AbilityDefinition def)
        {
            if (def == null || def.useCharges)
                return false;

            float reduce = 0f;
            if (attributeSet != null && cooldownFlatReduceSecondsOnHitAttribute != null)
            {
                var ro = attributeSet.GetReadOnly(cooldownFlatReduceSecondsOnHitAttribute);
                if (ro != null)
                    reduce = ro.CurrentValue;
            }

            if (reduce <= 0f)
                return false;

            return ReduceCooldownRemaining(def, reduce);
        }

        public float GetFinalCooldownSeconds(AbilityDefinition def)
        {
            if (def == null)
                return 0f;

            float baseCd = Mathf.Max(0f, def.cooldown);
            if (baseCd <= 0f)
                return 0f;

            float mult = 1f;

            if (attributeSet != null && cooldownDurationMultiplierAttribute != null)
            {
                var ro = attributeSet.GetReadOnly(cooldownDurationMultiplierAttribute);
                if (ro != null)
                    mult = ro.CurrentValue;
            }

            if (mult <= 0f)
                mult = 1f;

            for (int i = 0; i < scopedDurationMultipliers.Count; i++)
            {
                ScopedCooldownMultiplier scoped = scopedDurationMultipliers[i];
                if (scoped.AppliesTo(def))
                    mult *= scoped.Multiplier;
            }

            return Mathf.Max(minCooldownSeconds, baseCd * mult);
        }

        private sealed class ScopedCooldownMultiplier
        {
            private readonly Func<AbilityDefinition, bool> appliesTo;

            public ScopedCooldownMultiplier(Func<AbilityDefinition, bool> appliesTo, float multiplier)
            {
                this.appliesTo = appliesTo;
                Multiplier = multiplier;
            }

            public float Multiplier { get; }

            public bool AppliesTo(AbilityDefinition definition)
            {
                return appliesTo != null && appliesTo(definition);
            }
        }

        private sealed class ScopedCooldownMultiplierHandle : IDisposable
        {
            private List<ScopedCooldownMultiplier> owner;
            private ScopedCooldownMultiplier entry;

            public ScopedCooldownMultiplierHandle(
                List<ScopedCooldownMultiplier> owner,
                ScopedCooldownMultiplier entry)
            {
                this.owner = owner;
                this.entry = entry;
            }

            public void Dispose()
            {
                if (owner == null || entry == null)
                    return;

                owner.Remove(entry);
                owner = null;
                entry = null;
            }
        }

        public void ConsumeChargeOnCommit(AbilitySpec spec, AbilityDefinition def)
        {
            if (spec == null || def == null || !def.useCharges)
                return;

            int c = spec.GetInt(KEY_CHARGES, 0);
            if (c > 0)
                spec.SetInt(KEY_CHARGES, c - 1);

            if (spec.GetInt(KEY_CHARGES, 0) < def.maxCharges &&
                spec.GetFloat(KEY_RECHARGE, 0f) <= 0f)
            {
                spec.SetFloat(KEY_RECHARGE, Mathf.Max(0.01f, GetFinalCooldownSeconds(def)));
            }
        }

        private void TickChargeCooldown(AbilitySpec spec, AbilityDefinition def)
        {
            int charges = spec.GetInt(KEY_CHARGES, 0);
            int max = Mathf.Max(1, def.maxCharges);

            if (charges >= max)
            {
                spec.SetFloat(KEY_RECHARGE, 0f);
                return;
            }

            float r = spec.GetFloat(KEY_RECHARGE, 0f);
            if (r <= 0f)
                r = Mathf.Max(0.01f, GetFinalCooldownSeconds(def));

            r -= Time.deltaTime;

            if (r <= 0f)
            {
                charges++;
                spec.SetInt(KEY_CHARGES, charges);
                r = (charges < max) ? Mathf.Max(0.01f, GetFinalCooldownSeconds(def)) : 0f;
            }

            spec.SetFloat(KEY_RECHARGE, r);
        }
        /// <summary>
        /// 책임 : 특정 ability의 남은 cooldown을 정확한 값으로 설정한다.
        /// 씬 이동 후 cooldown 복원의 공식 setter 역할을 담당한다.
        /// 
        /// 처리 규칙:
        /// - 차지형 스킬은 재충전 남은 시간(KEY_RECHARGE)을 복원한다.
        /// - GE 기반 쿨다운은 cooldown effect를 제거 후 남은 시간으로 다시 건다.
        /// - fallback 쿨다운은 spec.CooldownRemaining에 직접 반영한다.
        /// </summary>
        public bool TrySetCooldownRemaining(AbilityDefinition def, float seconds)
        {
            if (owner == null || def == null)
                return false;

            float clamped = Mathf.Max(0f, seconds);
            var spec = owner.FindSpec(def);
            if (spec == null)
                return false;

            // 1) 차지형 스킬
            if (def.useCharges)
            {
                int max = Mathf.Max(1, def.maxCharges);

                // 현재 스냅샷 구조에서는 charges 개수 자체는 별도 복원하지 않으므로
                // 기존 spec의 charges 값을 유지하고, 다음 충전까지 남은 시간만 복원한다.
                int charges = spec.GetInt(KEY_CHARGES, max);
                charges = Mathf.Clamp(charges, 0, max);
                spec.SetInt(KEY_CHARGES, charges);

                if (charges >= max)
                {
                    spec.SetFloat(KEY_RECHARGE, 0f);
                }
                else
                {
                    spec.SetFloat(KEY_RECHARGE, clamped);
                }

                return true;
            }

            // 2) GE 기반 쿨다운
            if (defaultCooldownEffect != null && effectRunner != null)
            {
                effectRunner.EndEffectsBySourceObject(
                    owner.gameObject,
                    defaultCooldownEffect,
                    def);

                if (clamped <= 0f)
                    return true;

                var cdSpec = owner.MakeSpec(
                    defaultCooldownEffect,
                    causer: owner.gameObject,
                    sourceObject: def);

                cdSpec.SetDuration(clamped);
                effectRunner.ApplyEffectSpec(cdSpec, owner.gameObject);
                return true;
            }

            // 3) fallback 쿨다운
            spec.CooldownRemaining = clamped;
            return true;
        }
        /// <summary>
        /// 책임 : 특정 ability의 남은 cooldown과 충전 수를 함께 복원한다.
        /// 일반 스킬은 cooldown만, 차지형 스킬은 charges와 recharge를 함께 맞춰
        /// 씬 이동 전 사용 가능 상태를 최대한 그대로 재현한다.
        /// </summary>
        public bool TryRestoreCooldownState(
            AbilityDefinition def,
            float cooldownRemaining,
            int chargesRemaining)
        {
            if (owner == null || def == null)
                return false;

            float clampedCooldown = Mathf.Max(0f, cooldownRemaining);
            var spec = owner.FindSpec(def);
            if (spec == null)
                return false;

            // 1) 차지형 스킬
            if (def.useCharges)
            {
                int max = Mathf.Max(1, def.maxCharges);
                int clampedCharges = Mathf.Clamp(chargesRemaining, 0, max);

                spec.SetInt(KEY_CHARGES, clampedCharges);

                if (clampedCharges >= max)
                {
                    spec.SetFloat(KEY_RECHARGE, 0f);
                }
                else
                {
                    spec.SetFloat(KEY_RECHARGE, clampedCooldown);
                }

                return true;
            }

            // 2) GE 기반 쿨다운
            if (defaultCooldownEffect != null && effectRunner != null)
            {
                effectRunner.EndEffectsBySourceObject(
                    owner.gameObject,
                    defaultCooldownEffect,
                    def);

                if (clampedCooldown <= 0f)
                    return true;

                var cdSpec = owner.MakeSpec(
                    defaultCooldownEffect,
                    causer: owner.gameObject,
                    sourceObject: def);

                cdSpec.SetDuration(clampedCooldown);
                effectRunner.ApplyEffectSpec(cdSpec, owner.gameObject);
                return true;
            }

            // 3) fallback 쿨다운
            spec.CooldownRemaining = clampedCooldown;
            return true;
        }
    }
}

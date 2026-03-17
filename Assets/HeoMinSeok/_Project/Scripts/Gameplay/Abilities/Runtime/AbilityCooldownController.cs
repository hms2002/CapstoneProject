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

            return Mathf.Max(minCooldownSeconds, baseCd * mult);
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
                spec.SetFloat(KEY_RECHARGE, Mathf.Max(0.01f, def.cooldown));
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
    }
}
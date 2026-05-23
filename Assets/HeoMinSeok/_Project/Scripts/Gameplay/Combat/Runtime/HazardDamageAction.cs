using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 함정/환경 피해 전용 적용기.
    /// - 전투용 HitConfirmed / KillConfirmed는 다루지 않음
    /// - 실제 HP가 감소했을 때만 target AbilitySystem의 DamagedTag를 발행
    /// </summary>
    public static class HazardDamageAction
    {
        public static void ApplyDamage(
            AbilitySystem targetSystem,
            GameObject target,
            GE_Damage_Spec damageEffect,
            float finalHpDamage,
            GameObject causer,
            Object sourceObject = null,
            bool ignoreInvulnerability = false,
            bool ignoreEvasion = false,
            bool logDebug = false)
        {
            if (targetSystem == null || target == null || damageEffect == null)
            {
                LogDebug(logDebug, $"blocked: missing refs. targetSystem={targetSystem}, target={target}, damageEffect={damageEffect}", target);
                return;
            }

            if (!ignoreInvulnerability && CombatInvulnerabilityUtil.IsDamageSuppressed(target, damageEffect))
            {
                LogDebug(logDebug, $"blocked: invulnerability suppressed. target={target.name}", target);
                return;
            }

            if (!ignoreEvasion && CombatEvasionUtil.TryRollEvasion(target))
            {
                LogDebug(logDebug, $"blocked: evaded. target={target.name}, damage={finalHpDamage:0.###}", target);
                DamagePopupService.ShowText("EVADE", target.transform.position);
                return;
            }

            if (targetSystem.EffectRunner == null)
            {
                LogDebug(logDebug, $"blocked: missing EffectRunner. target={target.name}", target);
                return;
            }

            if (finalHpDamage <= 0f)
            {
                LogDebug(logDebug, $"blocked: non-positive damage. target={target.name}, damage={finalHpDamage:0.###}", target);
                return;
            }

            var hpCheck = CaptureHpCheck(target, damageEffect);
            var shieldCheck = CaptureShieldCheck(target, damageEffect);
            LogDebug(
                logDebug,
                $"before apply. target={target.name}, effect={damageEffect.name}, effectType={damageEffect.GetType().Name}, isSpec={damageEffect is ISpecGameplayEffect}, duration={damageEffect.duration:0.###}, requested={finalHpDamage:0.###}, hp={FormatCheckValue(hpCheck)}, shield={FormatCheckValue(shieldCheck)}",
                target);

            var spec = targetSystem.MakeSpec(
                damageEffect,
                causer: causer,
                sourceObject: sourceObject
            );

            if (damageEffect.damageKey != null)
                spec.SetSetByCallerMagnitude(damageEffect.damageKey, finalHpDamage);

            targetSystem.EffectRunner.ApplyEffectSpec(spec, target);

            TryShowPopup(target, hpCheck);
            EmitDamagedTaken(targetSystem, target, causer, hpCheck);
            LogAppliedDamage(logDebug, target, hpCheck, shieldCheck, finalHpDamage, ignoreInvulnerability, ignoreEvasion);
        }

        /// <summary>
        /// 책임:
        /// - 환경 피해 적용 실패 지점을 선택적으로 출력해 함정/장판 authoring 문제를 빠르게 추적한다.
        /// - 기본값은 비활성으로 두어 일반 장판 tick 로그가 전투 중에 쌓이지 않게 한다.
        /// </summary>
        private static void LogDebug(bool enabled, string message, Object context)
        {
            if (!enabled)
                return;

            Debug.Log($"[HazardDamageAction] {message}", context);
        }

        private static void LogAppliedDamage(
            bool enabled,
            GameObject target,
            HpCheckData hpCheck,
            HpCheckData shieldCheck,
            float requestedDamage,
            bool ignoreInvulnerability,
            bool ignoreEvasion)
        {
            if (!enabled || target == null)
                return;

            if (!hpCheck.IsValid)
            {
                Debug.Log(
                    $"[HazardDamageAction] applied request. target={target.name}, requested={requestedDamage:0.###}, hpCheck=invalid, shield={FormatCheckValueAfter(shieldCheck)}, ignoreInvulnerability={ignoreInvulnerability}, ignoreEvasion={ignoreEvasion}",
                    target);
                return;
            }

            float postHp = hpCheck.TargetAttrs.GetAttributeValue(hpCheck.HpAttr);
            float appliedDamage = Mathf.Max(0f, hpCheck.PreHp - postHp);
            Debug.Log(
                $"[HazardDamageAction] applied. target={target.name}, requested={requestedDamage:0.###}, applied={appliedDamage:0.###}, hp={hpCheck.PreHp:0.###}->{postHp:0.###}, shield={FormatCheckValueAfter(shieldCheck)}, ignoreInvulnerability={ignoreInvulnerability}, ignoreEvasion={ignoreEvasion}",
                target);
        }

        private static HpCheckData CaptureHpCheck(GameObject target, GE_Damage_Spec geDmg)
        {
            if (target == null || geDmg == null || geDmg.healthAttribute == null)
                return default;

            var hpAttr = geDmg.healthAttribute;
            var targetAttrs = target.GetComponent<AttributeSet>();
            if (targetAttrs == null)
                return new HpCheckData(preHp: -1f, hpAttr, null);

            float preHp = targetAttrs.GetAttributeValue(hpAttr);
            return new HpCheckData(preHp, hpAttr, targetAttrs);
        }

        private static HpCheckData CaptureShieldCheck(GameObject target, GE_Damage_Spec geDmg)
        {
            if (target == null || geDmg == null || geDmg.absorbShieldAttribute == null)
                return default;

            var shieldAttr = geDmg.absorbShieldAttribute;
            var targetAttrs = target.GetComponent<AttributeSet>();
            if (targetAttrs == null)
                return new HpCheckData(preHp: -1f, shieldAttr, null);

            float preShield = targetAttrs.GetAttributeValue(shieldAttr);
            return new HpCheckData(preShield, shieldAttr, targetAttrs);
        }

        private static string FormatCheckValue(HpCheckData check)
        {
            return check.IsValid ? $"{check.PreHp:0.###}" : "invalid";
        }

        private static string FormatCheckValueAfter(HpCheckData check)
        {
            if (!check.IsValid)
                return "invalid";

            float postValue = check.TargetAttrs.GetAttributeValue(check.HpAttr);
            return $"{check.PreHp:0.###}->{postValue:0.###}";
        }

        private static void EmitDamagedTaken(
            AbilitySystem targetSystem,
            GameObject target,
            GameObject causer,
            HpCheckData hpCheck)
        {
            if (targetSystem == null || target == null)
                return;

            if (targetSystem.DamagedTag == null)
                return;

            if (!hpCheck.IsValid)
                return;

            float postHp = hpCheck.TargetAttrs.GetAttributeValue(hpCheck.HpAttr);
            if (postHp >= hpCheck.PreHp)
                return;

            targetSystem.SendGameplayEvent(targetSystem.DamagedTag, new AbilityEventData
            {
                AbilitySystem = targetSystem,
                Spec = null,
                Instigator = causer,
                Target = target,
                WorldPosition = target.transform.position,
                Causer = causer
            });
        }

        /// <summary>
        /// 책임 :
        /// - 함정/장판 피해처럼 AbilitySpec이 없는 피해도 실제 HP 감소량 기준으로 팝업을 표시한다.
        /// - 기존 Attribute 감소 listener fallback과 중복 표시되지 않도록 같은 피해를 suppress 등록한다.
        /// </summary>
        private static void TryShowPopup(GameObject target, HpCheckData hpCheck)
        {
            if (target == null || !hpCheck.IsValid)
                return;

            float postHp = hpCheck.TargetAttrs.GetAttributeValue(hpCheck.HpAttr);
            float appliedDamage = Mathf.Max(0f, hpCheck.PreHp - postHp);
            if (appliedDamage <= 0f)
                return;

            DamagePopupService.Show(DamagePopupRequest.Damage(appliedDamage, target.transform.position));
            DamagePopupDuplicateSuppressor.Register(target, appliedDamage);
        }

        private readonly struct HpCheckData
        {
            public readonly float PreHp;
            public readonly AttributeDefinition HpAttr;
            public readonly AttributeSet TargetAttrs;

            public HpCheckData(float preHp, AttributeDefinition hpAttr, AttributeSet targetAttrs)
            {
                PreHp = preHp;
                HpAttr = hpAttr;
                TargetAttrs = targetAttrs;
            }

            public bool IsValid => TargetAttrs != null && HpAttr != null && PreHp >= 0f;
        }
    }
}

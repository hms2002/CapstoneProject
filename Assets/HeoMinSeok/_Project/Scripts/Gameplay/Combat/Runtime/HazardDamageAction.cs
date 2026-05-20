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
            bool ignoreInvulnerability = false)
        {
            if (targetSystem == null || target == null || damageEffect == null)
                return;

            if (!ignoreInvulnerability && CombatInvulnerabilityUtil.IsDamageSuppressed(target, damageEffect))
                return;

            if (CombatEvasionUtil.TryRollEvasion(target))
            {
                DamagePopupService.ShowText("EVADE", target.transform.position);
                return;
            }

            if (targetSystem.EffectRunner == null)
                return;

            if (finalHpDamage <= 0f)
                return;

            var hpCheck = CaptureHpCheck(target, damageEffect);

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

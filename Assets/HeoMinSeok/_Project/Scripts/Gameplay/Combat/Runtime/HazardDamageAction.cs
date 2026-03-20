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
            Object sourceObject = null)
        {
            if (targetSystem == null || target == null || damageEffect == null)
                return;

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
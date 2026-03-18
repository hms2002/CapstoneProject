using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    public static class ElementBuildUpResolver
    {
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

            var statProvider = attacker.GetComponent<AttributeStatProvider>();
            if (statProvider == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning(
                    $"[ElementBuildUpResolver] '{attacker.name}' 에 AttributeStatProvider 가 없습니다. " +
                    "자동 속성 누적 계산을 건너뜁니다.",
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
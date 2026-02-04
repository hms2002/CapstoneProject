using System;
using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Stat Modifiers (Generic)")]
public class RelicLogic_StatModifiers : RelicLogic
{
    [Serializable]
    public struct Entry
    {
        public AttributeDefinition attribute;
        public ModifierType type;  // Flat / Percent

        [Tooltip("레벨 1 기준 값. valueByLevel이 비어있으면 value * level 로 선형 강화합니다.")]
        public float value;        // Flat: +5, Percent: +0.2 (즉 +20%)

        [Tooltip("레벨별 값 테이블(레벨1=0번째). 비어있으면 value * level 로 계산.")]
        public List<float> valueByLevel;

        [Tooltip("0 이하면 영구. (현재 AttributeModifier Duration은 초 단위)")]
        public float duration;
    }

    [Header("Modifiers to Apply")]
    public List<Entry> entries = new List<Entry>();

    private static float EvalValue(Entry e, int level)
    {
        if (level < 1) level = 1;

        // 1) explicit table
        if (e.valueByLevel != null && e.valueByLevel.Count > 0)
        {
            int idx = Mathf.Clamp(level - 1, 0, e.valueByLevel.Count - 1);
            return e.valueByLevel[idx];
        }

        // 2) fallback: simple linear scaling
        return e.value * level;
    }

    public override void OnEquipped(RelicContext ctx)
    {
        if (ctx.attributeSet == null) return;
        if (ctx.token == null) return;

        int level = ctx.level > 0 ? ctx.level : 1;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.attribute == null) continue;

            float v = EvalValue(e, level);

            var mod = new AttributeModifier(
                e.type,
                v,
                ctx.token,
                duration: Mathf.Max(0f, e.duration)
            );

            ctx.attributeSet.AddModifier(e.attribute, mod);
        }
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.attributeSet == null) return;
        if (ctx.token == null) return;

        ctx.attributeSet.RemoveModifiersFromSource(ctx.token);
    }
}

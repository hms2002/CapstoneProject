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
        public float value;        // Flat: +5, Percent: +0.2 (즉 +20%)
        [Tooltip("0 이하면 영구. (현재 AttributeModifier Duration은 초 단위)")]
        public float duration;
    }

    [Header("Modifiers to Apply")]
    public List<Entry> entries = new List<Entry>();

    public override void OnEquipped(RelicContext ctx)
    {
        if (ctx.attributeSet == null) return;
        if (ctx.token == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.attribute == null) continue;

            var mod = new AttributeModifier(
                e.type,
                e.value,
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

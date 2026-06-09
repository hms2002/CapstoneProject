using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Stat Modifiers (Generic)")]
public class RelicLogic_StatModifiers : RelicLogic
{
    [Serializable]
    public struct Entry
    {
        public AttributeDefinition attribute;

        [Tooltip("비워두면 AttributeDefinition의 표시 이름을 사용합니다.")]
        public string displayNameOverride;

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
        return RelicTooltipFormatter.EvaluateLeveledValue(e.value, e.valueByLevel, level);
    }

    /// <summary>
    /// 책임 :
    /// - 엔트리별 표시 이름 override를 우선 사용하고, 없으면 AttributeDefinition의 기본 이름으로 fallback 한다.
    /// - 시스템용 속성 식별자와 유물 툴팁 문구를 느슨하게 분리한다.
    /// </summary>
    private static string ResolveDisplayName(Entry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.displayNameOverride))
            return entry.displayNameOverride;

        if (entry.attribute == null)
            return string.Empty;

        return !string.IsNullOrEmpty(entry.attribute.attributeName)
            ? entry.attribute.attributeName
            : entry.attribute.name;
    }

    public override void OnEquipped(RelicContext ctx)
    {
        ApplyModifiers(ctx);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.attributeSet == null) return;
        if (ctx.token == null) return;

        ctx.attributeSet.RemoveModifiersFromSource(ctx.token);
    }
    public override void OnRestoreAttached(RelicContext ctx)
    {
        ApplyModifiers(ctx);
    }

    public override void AppendPreviewModifiers(
        RelicContext ctx,
        AttributeDefinition attribute,
        List<AttributeModifier> results)
    {
        if (attribute == null || results == null)
            return;

        int level = ctx.level > 0 ? ctx.level : 1;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.attribute != attribute)
                continue;

            results.Add(new AttributeModifier(
                e.type,
                EvalValue(e, level),
                ctx.token,
                duration: Mathf.Max(0f, e.duration)));
        }
    }

    /// <summary>
    /// 책임 :
    /// - 유물 레벨에 맞는 상시 AttributeModifier를 현재 token 기준으로 부여한다.
    /// - 일반 장착과 씬 복원 장착이 같은 부여 규칙을 공유하도록 한다.
    /// </summary>
    private void ApplyModifiers(RelicContext ctx)
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

            ctx.attributeSet.TryAddModifier(e.attribute, mod);
        }
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        var sb = new StringBuilder();

        if (entries == null || entries.Count == 0)
        {
            sb.AppendLine("(스탯 변경 없음)");
        }
        else
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.attribute == null)
                    continue;

                string displayName = ResolveDisplayName(entry);
                string value = RelicTooltipFormatter.FormatSignedValueToken(
                    EvalValue(entry, previewLevel),
                    RelicTooltipFormatter.ShouldDisplayAsPercent(entry.attribute, displayName, entry.type));

                sb.Append($"● [[{displayName}]] {value}");
                if (entry.duration > 0f)
                    sb.Append($" ({RelicTooltipFormatter.FormatSeconds(entry.duration)})");
                sb.AppendLine();
            }
        }

        return new RelicTooltipData
        {
            effectText = sb.ToString().TrimEnd()
        };
    }
}

using System.Text;
using UnityEngine;
using UnityGAS;

public class RelicDetailView : MonoBehaviour, IItemDetailView
{
    [SerializeField] private SectionListView sections;

    public bool CanShow(object def) => def is RelicDefinition;

    public void Show(object def, ItemDetailContext ctx, ItemDetailPanelServices services)
    {
        gameObject.SetActive(true);
        sections?.Clear();

        var r = (RelicDefinition)def;

        // Level info (if the player already owns it)
        int level = 1;

        if (ctx != null)
        {
            // 1) 슬롯 레벨(상자/가방/장착/월드루트) 우선
            if (ctx.relicLevelOverride > 0) level = ctx.relicLevelOverride;
            else if (ctx.sourceContainer is IRelicLevelProvider p && ctx.sourceIndex >= 0)
            {
                if (p.TryGetRelicLevel(ctx.sourceIndex, out var lvl)) level = lvl;
            }
            // 2) 그래도 없으면 “플레이어가 이미 보유한 레벨”로 fallback
            else if (ctx.owner != null)
            {
                var inv = ctx.owner.GetComponent<RelicInventory>();
                if (inv != null && inv.TryGetRelicLevelById(r.relicId, out var ownedLevel))
                    level = ownedLevel;
            }
        }

        // Description
        if (!string.IsNullOrEmpty(r.description))
        {
            var desc = services.formatText != null ? services.formatText(r.description) : r.description;
            sections.Add("설명", desc, services.showGlossary);
        }

        // Upgrade / Level
        {
            var sb = new StringBuilder();
            sb.AppendLine($"현재 강화: +{level} / +{Mathf.Max(1, r.maxLevel)}");
            sb.AppendLine($"획득 시 강화량: +{Mathf.Max(1, r.dropLevel)}");
            sections.Add("강화", sb.ToString().TrimEnd(), services.showGlossary);
        }

        // Effects
        string effect = BuildEffectText(r, ctx, level);
        effect = services.formatText != null ? services.formatText(effect) : effect;
        sections.Add("효과", effect, services.showGlossary);
    }

    public void Hide()
    {
        sections?.Clear();
        gameObject.SetActive(false);
    }

    private static float EvalValue(float baseValue, System.Collections.Generic.List<float> table, int level)
    {
        if (level < 1) level = 1;
        if (table != null && table.Count > 0)
        {
            int idx = Mathf.Clamp(level - 1, 0, table.Count - 1);
            return table[idx];
        }
        return baseValue * level;
    }

    private string BuildEffectText(RelicDefinition r, ItemDetailContext ctx, int level)
    {
        var sb = new StringBuilder();

        if (r.logic == null)
        {
            sb.AppendLine("(로직 없음)");
            return sb.ToString().TrimEnd();
        }

        int maxLevel = Mathf.Max(1, r.maxLevel);
        int nextLevel = Mathf.Clamp(level + 1, 1, maxLevel);
        bool hasNext = nextLevel != level;

        // 1) Stat Modifiers
        if (r.logic is RelicLogic_StatModifiers mods)
        {
            if (mods.entries == null || mods.entries.Count == 0)
            {
                sb.AppendLine("(스탯 변경 없음)");
                return sb.ToString();
            }

            for (int i = 0; i < mods.entries.Count; i++)
            {
                var e = mods.entries[i];
                if (e.attribute == null) continue;

                float curV = EvalValue(e.value, e.valueByLevel, level);
                float nextV = hasNext ? EvalValue(e.value, e.valueByLevel, nextLevel) : curV;

                string name = e.attribute.attributeName;
                string type = e.type.ToString();

                string curStr = e.type == ModifierType.Percent ? $"{curV * 100f:0.##}%" : $"{curV:0.##}";
                string nextStr = e.type == ModifierType.Percent ? $"{nextV * 100f:0.##}%" : $"{nextV:0.##}";

                sb.Append($"- [[{name}]]: {type} {curStr}");
                if (hasNext && nextV != curV) sb.Append($"  →  <color=#90CAF9>{nextStr}</color>");
                if (e.duration > 0f) sb.Append($" ({e.duration:0.##}s)");

                // show current value if available
                if (ctx != null && ctx.attributeSet != null)
                {
                    float cur = ctx.attributeSet.GetAttributeValue(e.attribute);
                    sb.Append($"  | 현재: <color=#FFD54F>{cur:0.##}</color>");
                }

                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        // 2) Lightning proc
        if (r.logic is RelicLogic_LightningOnHitConfirmed_Managed lightning)
        {
            float curDmg = EvalValue(lightning.baseDamage, lightning.baseDamageByLevel, level);
            float nextDmg = hasNext ? EvalValue(lightning.baseDamage, lightning.baseDamageByLevel, nextLevel) : curDmg;

            sb.AppendLine("- 발동: HitConfirmed");
            sb.Append($"- 번개 피해: {curDmg:0.##}");
            if (hasNext && nextDmg != curDmg) sb.Append($"  →  <color=#90CAF9>{nextDmg:0.##}</color>");
            sb.AppendLine();

            sb.AppendLine($"- 반경: {lightning.radius:0.##}");
            if (lightning.cooldownSeconds > 0f) sb.AppendLine($"- 쿨다운: {lightning.cooldownSeconds:0.##}s");

            return sb.ToString().TrimEnd();
        }

        // 3) Current HP threshold move speed
        if (r.logic is RelicLogic_MoveSpeedByCurrentHealth_Managed moveByHp)
        {
            if (moveByHp.rules == null || moveByHp.rules.Count == 0)
            {
                sb.AppendLine("(체력 구간 규칙 없음)");
                return sb.ToString().TrimEnd();
            }

            for (int i = 0; i < moveByHp.rules.Count; i++)
            {
                var rule = moveByHp.rules[i];
                float curV = EvalValue(rule.percentBonus, rule.percentBonusByLevel, level);
                float nextV = hasNext ? EvalValue(rule.percentBonus, rule.percentBonusByLevel, nextLevel) : curV;

                string rangeText;
                bool hasLower = rule.minHealthInclusive > 0f;
                bool hasUpper = !float.IsInfinity(rule.maxHealthInclusive) && rule.maxHealthInclusive < 999999f;

                if (Mathf.Approximately(rule.minHealthInclusive, rule.maxHealthInclusive))
                    rangeText = $"현재 체력이 {rule.minHealthInclusive:0.##}";
                else if (hasLower && hasUpper)
                    rangeText = $"현재 체력이 {rule.minHealthInclusive:0.##}~{rule.maxHealthInclusive:0.##}";
                else if (hasLower)
                    rangeText = $"현재 체력이 {rule.minHealthInclusive:0.##} 이상";
                else
                    rangeText = $"현재 체력이 {rule.maxHealthInclusive:0.##} 이하";

                string curStr = curV >= 0f ? $"+{curV * 100f:0.##}%" : $"{curV * 100f:0.##}%";
                string nextStr = nextV >= 0f ? $"+{nextV * 100f:0.##}%" : $"{nextV * 100f:0.##}%";

                sb.Append($"- {rangeText}: [[이동속도]] {curStr}");
                if (hasNext && nextV != curV) sb.Append($"  →  <color=#90CAF9>{nextStr}</color>");
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }

        if (r.logic is RelicLogic_EvasionFromBonusMoveSpeed_Managed evadeByMove)
        {
            string stepStr = $"{evadeByMove.bonusMoveStep * 100f:0.##}%";
            string evasionStr = $"{evadeByMove.evasionPerStep * 100f:0.##}%";

            sb.AppendLine($"- [[추가 이동속도]] {stepStr}마다 [[공격을 회피할 확률]] {evasionStr} 추가");

            return sb.ToString().TrimEnd();
        }

        if (r.logic is RelicLogic_MoveSpeedStackOnCriticalHit_Managed moveOnCrit)
        {
            string gainStr = $"{moveOnCrit.percentPerCritical * 100f:0.##}%";
            string capStr = $"{moveOnCrit.maxPercentBonus * 100f:0.##}%";

            sb.AppendLine($"- [[치명타]] 시 [[이동속도]] {gainStr}씩 증가");
            sb.AppendLine($"- 최대 {capStr}까지 증가 가능");
            sb.AppendLine("- 피해를 받으면 위 보너스가 초기화됨");

            return sb.ToString().TrimEnd();
        }

        if (r.logic is RelicLogic_Stenographer_Managed)
        {
            sb.AppendLine("- [[잔영의 날개]] 전용 유물");
            sb.AppendLine("- [[스킬 1]] 변동");
            sb.AppendLine("- 사용 시 [[이동속도]] +150%");
            sb.AppendLine("- 3초 후 추가 [[이동속도]] +150%  (총 +300%)");
            sb.AppendLine("- 3초 후 추가 [[이동속도]] +200%  (총 +500%)");

            return sb.ToString().TrimEnd();
        }

        if (r.logic is RelicLogic_OneDropOfSwiftness_Managed)
        {
            sb.AppendLine("- [[잔영의 날개]] 전용 유물");
            sb.AppendLine("- [[스킬 2]]로 적 처치 시 [[스킬 1]]의 실행을 취소하지 않음");

            return sb.ToString().TrimEnd();
        }

        // Fallback: show linked param if exists
        if (r.param != null)
        {
            sb.AppendLine($"Param: {r.param.name}");
            sb.AppendLine();
        }

        sb.AppendLine("(이 유물 로직 타입에 대한 상세 표시를 추가할 수 있어요)");
        return sb.ToString().TrimEnd();
    }
}

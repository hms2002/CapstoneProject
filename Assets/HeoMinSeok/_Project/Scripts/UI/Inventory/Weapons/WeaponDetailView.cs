using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityGAS;
using UnityGAS.Sample;

public class WeaponDetailView : MonoBehaviour, IItemDetailView
{
    [SerializeField] private SectionListView sections;

    public bool CanShow(object def) => def is WeaponDefinition;

    public void Show(object def, ItemDetailContext ctx, ItemDetailPanelServices services)
    {
        gameObject.SetActive(true);
        sections?.Clear();

        WeaponDefinition weapon = (WeaponDefinition)def;

        AddWeaponSummarySection(weapon, services);
        AddAbilitySection("일반공격", weapon.attack, DamageAttackKind.Normal, ctx, services, weapon.attackInputHint, InputActionId.PrimaryAttack);
        AddAbilitySection("스킬 1", weapon.skill1, DamageAttackKind.Skill, ctx, services, weapon.skill1InputHint, InputActionId.Skill1);
        AddAbilitySection("스킬 2", weapon.skill2, DamageAttackKind.Skill, ctx, services, weapon.skill2InputHint, InputActionId.Skill2);
    }

    public void Hide()
    {
        sections?.Clear();
        gameObject.SetActive(false);
    }

    private void AddAbilitySection(
        string header,
        AbilityDefinition ability,
        DamageAttackKind kind,
        ItemDetailContext ctx,
        ItemDetailPanelServices services,
        string inputHint,
        InputActionId inputAction)
    {
        if (sections == null || ability == null)
            return;

        string body = BuildAbilityBody(ability, kind, ctx, inputHint, inputAction);
        if (services?.formatText != null)
            body = services.formatText(body);

        sections.Add(header, body, services?.showGlossary);
    }

    private void AddWeaponSummarySection(WeaponDefinition weapon, ItemDetailPanelServices services)
    {
        if (sections == null || weapon == null)
            return;

        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(weapon.storyText))
            sb.AppendLine(weapon.storyText);

        if (weapon.statModifiers != null && weapon.statModifiers.Count > 0)
        {
            if (sb.Length > 0)
                sb.AppendLine();

            sb.AppendLine("<b>능력치</b>");

            for (int i = 0; i < weapon.statModifiers.Count; i++)
            {
                WeaponDefinition.WeaponStatModifier entry = weapon.statModifiers[i];
                if (entry.attribute == null)
                    continue;

                string label = !string.IsNullOrEmpty(entry.labelOverride)
                    ? entry.labelOverride
                    : (!string.IsNullOrEmpty(entry.attribute.attributeName) ? entry.attribute.attributeName : entry.attribute.name);

                string valueText = entry.type == ModifierType.Percent
                    ? $"+{entry.value * 100f:0.#}%"
                    : $"+{entry.value:0.##}";

                sb.AppendLine($"- {label} <color=#FFD54F>{valueText}</color>");
            }
        }

        string body = sb.ToString().TrimEnd();
        if (services?.formatText != null)
            body = services.formatText(body);

        if (!string.IsNullOrEmpty(body))
            sections.Add("요약", body, services?.showGlossary);
    }

    private string BuildAbilityBody(
        AbilityDefinition ability,
        DamageAttackKind kind,
        ItemDetailContext ctx,
        string inputHint,
        InputActionId inputAction)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"<b>{ability.abilityName}</b>");
        if (!string.IsNullOrEmpty(ability.description))
            sb.AppendLine(ability.description);

        string resolvedInputHint = ResolveInputHintLabel(inputHint, inputAction);
        if (!string.IsNullOrEmpty(resolvedInputHint))
            sb.AppendLine($"입력: <color=#A7E1FF>{resolvedInputHint}</color>");

        if (ability.cooldown > 0f)
            sb.AppendLine($"쿨다운: <color=#FFD54F>{ability.cooldown:0.##}s</color>");

        if (ability.abilityTags != null && ability.abilityTags.Count > 0)
            sb.AppendLine($"태그: {JoinTags(ability.abilityTags)}");

        if (ability.sourceObject != null)
        {
            sb.AppendLine();
            sb.AppendLine("<b>상세</b>");
            AppendSourceObjectDetails(sb, ability.sourceObject, kind, ctx);
        }

        return sb.ToString().TrimEnd();
    }

    private static string ResolveInputHintLabel(string fallbackInputHint, InputActionId inputAction)
    {
        string bindingLabel = InputBindingService.EnsureInstance().GetBindingDisplayLabel(inputAction);
        if (!string.IsNullOrWhiteSpace(bindingLabel) && bindingLabel != "-")
            return bindingLabel;

        return fallbackInputHint;
    }

    private void AppendSourceObjectDetails(StringBuilder sb, Object sourceObj, DamageAttackKind kind, ItemDetailContext ctx)
    {
        if (sourceObj == null)
        {
            sb.AppendLine("(추가 정보 없음)");
            return;
        }

        if (sourceObj is IDetailProvider provider)
        {
            ItemDetailBlock block = provider.BuildDetailBlock(ctx);

            if (!string.IsNullOrEmpty(block.title))
                sb.AppendLine($"<color=#A7E1FF>{block.title}</color>");

            if (!string.IsNullOrEmpty(block.body))
                sb.AppendLine(block.body);
            else
                sb.AppendLine("(추가 정보 없음)");

            return;
        }

        sb.AppendLine(sourceObj.name);
        sb.AppendLine("(디테일 제공 인터페이스 미구현)");
    }

    private static string JoinTags(List<GameplayTag> tags)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < tags.Count; i++)
        {
            if (tags[i] == null)
                continue;

            if (sb.Length > 0)
                sb.Append(", ");

            sb.Append(tags[i].ToString());
        }

        return sb.ToString();
    }
}

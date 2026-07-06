using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 하나의 능력이 상세 패널에 여러 변형 설명을 노출할 때 필요한 projection 데이터를 담는다.
/// - UI 렌더러가 ability data 구현을 몰라도 제목, 아이콘, 본문, 쿨다운, 입력 힌트를 같은 방식으로 표시하게 한다.
/// </summary>
public readonly struct AbilityTooltipVariant
{
    public AbilityTooltipVariant(
        string id,
        string title,
        Sprite icon,
        string body,
        float? cooldownSeconds = null,
        string inputHint = null,
        string extraMeta = null)
    {
        Id = id;
        Title = title;
        Icon = icon;
        Body = body;
        CooldownSeconds = cooldownSeconds;
        InputHint = inputHint;
        ExtraMeta = extraMeta;
    }

    public string Id { get; }
    public string Title { get; }
    public Sprite Icon { get; }
    public string Body { get; }
    public float? CooldownSeconds { get; }
    public string InputHint { get; }
    public string ExtraMeta { get; }
}

/// <summary>
/// 책임 :
/// - 능력 데이터가 상세 패널용 변형 tooltip 목록을 직접 구성할 수 있는 계약을 정의한다.
/// - weapon/relic gameplay 데이터가 UI 구현 타입에 의존하지 않고 표시용 variant만 제공하게 한다.
/// </summary>
public interface IAbilityTooltipVariantProvider
{
    int GetAbilityTooltipVariantCount(AbilityDefinition ability, ItemDetailContext ctx);
    AbilityTooltipVariant BuildAbilityTooltipVariant(AbilityDefinition ability, int variantIndex, ItemDetailContext ctx);
}

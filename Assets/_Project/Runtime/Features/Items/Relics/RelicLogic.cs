using UnityEngine;
using System.Collections.Generic;
using UnityGAS;

/// <summary>
/// 책임 : 유물 장착/해제 생명주기에 반응하는 정적 로직의 공통 베이스다.
/// 일반 장착 경로와 복원 장착 경로를 분리해, 씬 복원 시 중복 효과 적용을 막는다.
/// </summary>
public abstract class RelicLogic : ScriptableObject
{
    [Header("Tooltip")]
    [SerializeField, TextArea(3, 8)]
    [Tooltip("효과 문장 본문 템플릿. 포맷 규칙은 고정하고, {token} 자리표시자만 로직이 치환합니다. 비워두면 코드 기본 문장을 사용합니다.")]
    private string effectTemplate;

    /// <summary>
    /// 책임 :
    /// - 각 유물 로직이 인스펙터에 보여줄 기본 효과 템플릿 본문을 제공한다.
    /// - effectTemplate가 비어 있을 때만 authoring 초기값으로 사용하고, 이후 수정은 SO 데이터가 우선한다.
    /// </summary>
    protected virtual string DefaultEffectTemplate => string.Empty;

    public abstract void OnEquipped(RelicContext ctx);
    public abstract void OnUnequipped(RelicContext ctx);

    /// <summary>
    /// 책임 : 씬 복원 시 유물의 runtime hook만 다시 연결한다.
    /// modifier/effect/tag/ability를 새로 부여하지 않는 것이 원칙이다.
    /// </summary>
    public virtual void OnRestoreAttached(RelicContext ctx) { }

    /// <summary>
    /// 책임 : 복원용 runtime hook을 해제할 필요가 있을 때 사용한다.
    /// 기본 구현은 비워 두고, 필요한 유물만 override 한다.
    /// </summary>
    public virtual void OnRestoreDetached(RelicContext ctx) { }

    /// <summary>
    /// 책임 : 특정 Attribute에 대해 이 유물이 부여할 modifier를 실제 장착 없이 미리 계산한다.
    /// 장착/해제 전 체력 보정 가능 여부를 검증하는 시스템에서 사용한다.
    /// </summary>
    public virtual void AppendPreviewModifiers(
        RelicContext ctx,
        AttributeDefinition attribute,
        List<AttributeModifier> results)
    {
    }

    /// <summary>
    /// 책임 :
    /// - 현재 프리뷰 레벨 기준의 유물 효과 텍스트를 완성해 상세 UI에 전달한다.
    /// - 값 계산과 문자열 포맷 책임은 RelicLogic이 갖고, view는 결과를 그대로 출력만 한다.
    /// </summary>
    public virtual RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return new RelicTooltipData
        {
            effectText = "(이 유물 로직 타입에 대한 상세 표시를 추가할 수 있어요)"
        };
    }

    protected virtual void OnValidate()
    {
        if (!string.IsNullOrWhiteSpace(effectTemplate))
            return;

        string defaultTemplate = DefaultEffectTemplate;
        if (!string.IsNullOrWhiteSpace(defaultTemplate))
            effectTemplate = defaultTemplate;
    }

    /// <summary>
    /// 책임 :
    /// - 유물 로직 기본 문장과 SO에 노출한 템플릿 중 실제로 사용할 본문 템플릿을 고른다.
    /// - 포맷 규칙은 유지하고, 템플릿 본문만 데이터에서 조정할 수 있게 한다.
    /// </summary>
    protected string ResolveEffectTemplate(string fallbackTemplate)
    {
        return string.IsNullOrWhiteSpace(effectTemplate) ? fallbackTemplate : effectTemplate;
    }

    /// <summary>
    /// 책임 :
    /// - 로직이 계산한 토큰 값을 본문 템플릿에 치환해 최종 효과 문장을 완성한다.
    /// - 상세 뷰가 토큰 구조를 알지 않도록 텍스트 완성 책임을 RelicLogic 안에 유지한다.
    /// </summary>
    protected RelicTooltipData BuildTemplatedTooltip(string fallbackTemplate, IDictionary<string, string> tokens)
    {
        return new RelicTooltipData
        {
            effectText = RelicTooltipFormatter.ReplaceTokens(ResolveEffectTemplate(fallbackTemplate), tokens)
        };
    }
}

using UnityEngine;

/// <summary>
/// 책임 :
/// - 유물 상세의 현재/프리뷰 레벨 상태를 관리하고, 해당 레벨 기준 툴팁 생성을 RelicLogic에 요청한다.
/// - Q/E 입력에 따른 레벨 프리뷰 갱신과 헤더 레벨 문자열 전달만 맡고, 공용 패널 레이아웃은 직접 다루지 않는다.
/// </summary>
public class RelicDetailView : MonoBehaviour, IItemDetailView
{
    [SerializeField] private SectionListView sections;
    [SerializeField] private string previewLevelUpColorHex = "90CAF9";
    [SerializeField] private string previewLevelDownColorHex = "FF5050";

    private RelicDefinition currentRelic;
    private ItemDetailContext currentContext;
    private ItemDetailPanelServices currentServices;
    private int actualLevel = 1;
    private int previewLevel = 1;

    public bool CanShow(object def) => def is RelicDefinition;
    public bool CanPreviewPreviousLevel => currentRelic != null && previewLevel > 1;
    public bool CanPreviewNextLevel => currentRelic != null && previewLevel < Mathf.Max(1, currentRelic.maxLevel);

    public void Show(object def, ItemDetailContext ctx, ItemDetailPanelServices services)
    {
        currentRelic = (RelicDefinition)def;
        currentContext = ctx;
        currentServices = services;
        actualLevel = ResolveActualLevel(currentRelic, ctx);
        previewLevel = actualLevel;

        gameObject.SetActive(true);
        RefreshSections();
    }

    public void Hide()
    {
        sections?.Clear();
        ResetPreviewState();
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!gameObject.activeSelf || currentRelic == null)
            return;

        InputBindingService input = InputBindingService.EnsureInstance();
        int nextPreviewLevel = previewLevel;
        int maxLevel = Mathf.Max(1, currentRelic.maxLevel);

        if (input.WasPressedThisFrame(InputActionId.Skill1))
            nextPreviewLevel = Mathf.Max(1, nextPreviewLevel - 1);

        if (input.WasPressedThisFrame(InputActionId.Skill2))
            nextPreviewLevel = Mathf.Min(maxLevel, nextPreviewLevel + 1);

        if (nextPreviewLevel == previewLevel)
            return;

        previewLevel = nextPreviewLevel;
        RefreshSections();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 유물/레벨 프리뷰 상태를 기준으로 레벨 섹션과 효과 섹션을 다시 구성한다.
    /// - 상세 뷰는 프리뷰 레벨 상태만 관리하고, 실제 효과 문장 생성은 RelicLogic에 위임한다.
    /// </summary>
    private void RefreshSections()
    {
        if (sections == null || currentRelic == null)
            return;

        sections.Clear();
        currentServices?.setHeaderLevelText?.Invoke(BuildLevelHeaderText());

        RelicTooltipData tooltip = currentRelic.logic != null
            ? currentRelic.logic.BuildTooltip(currentRelic, previewLevel, currentContext)
            : null;

        string effect = tooltip != null && !string.IsNullOrEmpty(tooltip.effectText)
            ? tooltip.effectText
            : "(로직 없음)";

        if (currentServices?.formatText != null)
            effect = currentServices.formatText(effect);

        sections.Add("효과", effect, currentServices?.showGlossary);
        Canvas.ForceUpdateCanvases();
    }

    /// <summary>
    /// 책임 :
    /// - 유물의 실제 레벨과 프리뷰 레벨을 구분해 레벨 섹션 문자열을 만든다.
    /// - 프리뷰 중이면 현재 레벨이 아니라는 걸 알 수 있게 레벨 숫자만 별도 색으로 강조한다.
    /// </summary>
    private string BuildLevelHeaderText()
    {
        int maxLevel = currentRelic != null ? Mathf.Max(1, currentRelic.maxLevel) : 1;
        string levelNumber = previewLevel == actualLevel
            ? previewLevel.ToString()
            : $"<color=#{ResolvePreviewLevelColorHex()}>{previewLevel}</color>";

        return $"Lv {levelNumber} / {maxLevel}";
    }

    /// <summary>
    /// 책임 :
    /// - 유물 레벨 프리뷰 방향에 따라 표시 색을 결정한다.
    /// - 실제 레벨보다 높으면 파랑, 낮으면 빨강으로 표시해 현재 상태와의 차이를 읽기 쉽게 만든다.
    /// </summary>
    private string ResolvePreviewLevelColorHex()
    {
        return previewLevel < actualLevel
            ? previewLevelDownColorHex
            : previewLevelUpColorHex;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 표시 대상 유물의 실제 레벨을 sourceContainer/override/플레이어 인벤토리 기준으로 계산한다.
    /// - 상세 뷰 프리뷰의 시작 기준점이 되는 레벨을 한 곳에서 일관되게 결정한다.
    /// </summary>
    private static int ResolveActualLevel(RelicDefinition relic, ItemDetailContext ctx)
    {
        int level = 1;

        if (ctx != null)
        {
            if (ctx.relicLevelOverride > 0)
                level = ctx.relicLevelOverride;
            else if (ctx.sourceContainer is IRelicLevelProvider provider && ctx.sourceIndex >= 0)
            {
                if (provider.TryGetRelicLevel(ctx.sourceIndex, out int slotLevel))
                    level = slotLevel;
            }
            else if (ctx.owner != null)
            {
                var inventory = ctx.owner.GetComponent<RelicInventory>();
                if (inventory != null && inventory.TryGetRelicLevelById(relic.relicId, out int ownedLevel))
                    level = ownedLevel;
            }
        }

        return relic != null ? relic.ClampLevel(level) : Mathf.Max(1, level);
    }

    /// <summary>
    /// 책임 :
    /// - 상세 뷰가 숨겨지거나 다른 아이템으로 전환될 때 레벨 프리뷰 상태를 초기화한다.
    /// - 다음 표시에서 이전 유물의 프리뷰 레벨이 남지 않도록 한다.
    /// </summary>
    private void ResetPreviewState()
    {
        currentServices?.setHeaderLevelText?.Invoke(string.Empty);
        currentRelic = null;
        currentContext = null;
        currentServices = null;
        actualLevel = 1;
        previewLevel = 1;
    }
}

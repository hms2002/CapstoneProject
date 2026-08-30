using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Sun Fragment (Managed)")]
public sealed class RelicLogic_SunFragment_Managed : RelicLogic
{
    [SerializeField] private GameplayEffect damageEffect;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private List<int> maxFragmentsByLevel = new() { 1, 2, 3 };
    [SerializeField] private List<int> burnStacksByLevel = new() { 4, 6, 8 };
    [SerializeField, Min(0.05f)] private float spawnInterval = 2f;
    [SerializeField, Min(0.1f)] private float orbitRadius = 1.2f;
    [SerializeField] private Vector2 orbitCenterLocalOffset = new(0f, 0.55f);
    [SerializeField] private float angularSpeedDegPerSec = 180f;
    [SerializeField, Min(0.05f)] private float fragmentSize = 0.3f;
    [SerializeField, Min(0.01f)] private float contactRadius = 0.18f;

    public override void OnEquipped(RelicContext ctx) => Enable(ctx);

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null)
            return;
        ctx.owner.GetComponent<SunFragmentOrbitController>()?.DisableForToken(ctx.token);
    }

    public override void OnRestoreAttached(RelicContext ctx) => Enable(ctx);

    public override void OnRestoreDetached(RelicContext ctx) => OnUnequipped(ctx);

    private void Enable(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.abilitySystem == null || ctx.token == null || damageEffect == null)
            return;

        var controller = ctx.owner.GetComponent<SunFragmentOrbitController>();
        if (controller == null)
            controller = ctx.owner.AddComponent<SunFragmentOrbitController>();

        int level = Mathf.Max(1, ctx.level);
        controller.EnableForToken(ctx.token, new SunFragmentOrbitController.Config
        {
            system = ctx.abilitySystem,
            damageEffect = damageEffect,
            targetLayers = targetLayers,
            maxFragments = Evaluate(maxFragmentsByLevel, level, 1),
            burnStacks = Evaluate(burnStacksByLevel, level, 4),
            spawnInterval = spawnInterval,
            orbitRadius = orbitRadius,
            orbitCenterLocalOffset = orbitCenterLocalOffset,
            angularSpeedDegPerSec = angularSpeedDegPerSec,
            fragmentSize = fragmentSize,
            contactRadius = contactRadius
        });
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        int level = Mathf.Max(1, previewLevel);
        return new RelicTooltipData
        {
            effectText = $"● {spawnInterval:0.##}초마다 [[태양의 파편]] 생성 (최대 {Evaluate(maxFragmentsByLevel, level, 1)}개)\n" +
                         $"● 접촉 시 [[화염 피해]] 100% 및 [[화상]] {Evaluate(burnStacksByLevel, level, 4)} 부여 후 소멸"
        };
    }

    private static int Evaluate(List<int> values, int level, int fallback)
    {
        if (values == null || values.Count == 0)
            return fallback;
        return values[Mathf.Clamp(level - 1, 0, values.Count - 1)];
    }
}

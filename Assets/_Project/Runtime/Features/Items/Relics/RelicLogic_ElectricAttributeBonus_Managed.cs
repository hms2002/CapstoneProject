using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임: 감전된 적 수, 최대 체력, 실행된 이동 스킬에 따라 유물 소유자의 전기 속성을 보정한다.
/// 조건부 값은 현재 장면에서 다시 계산하고, 이동 스킬의 임시 중첩은 장면 복원 시 이어받지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Electric Attribute Bonus (Managed)")]
public sealed class RelicLogic_ElectricAttributeBonus_Managed : RelicLogic
{
    private const string MovementAbilityTagResourcePath = "Tags/Ability.Movement";
    private const int MaximumMovementStacks = 4;
    public enum BonusMode
    {
        NearbyElectrocutedCount = 0,
        BonusMoveSpeed = 1,
        AfterDash = 2,
        MaxHealth = 3
    }

    [Header("Condition")]
    public BonusMode mode;
    public AttributeDefinition electricAddAttribute;
    public GameplayTag electrocutedTag;
    [Tooltip("기존 데이터 호환용 필드입니다. 이동 스킬은 Ability.Movement 분류 태그로 판단합니다.")]
    public AbilityDefinition triggerDashAbility;

    [Header("Conditional Bonus")]
    [Tooltip("기존 데이터 호환용 필드입니다. 감전된 적 수는 현재 씬 전체에서 계산합니다.")]
    [Min(0f)] public float nearbyRadius = 6.5f;
    [Tooltip("기존 이동속도 모드의 제한입니다. 감전된 적 수와 최대 체력에는 적용하지 않습니다.")]
    [Min(1)] public int maxUnits = 3;
    [Min(0.0001f)] public float bonusMoveStep = 0.1f;
    public List<float> bonusPerUnitByLevel = new() { 1f, 2f, 3f };

    [Header("Max Health")]
    public AttributeDefinition maxHealthAttribute;
    [Min(0.0001f)] public float healthStep = 1f;

    [Header("After Movement Ability")]
    [Tooltip("레벨별 중첩 하나의 고정 전기 속성 증가량입니다.")]
    public List<float> flatBonusByLevel = new() { 1f, 1f, 2f };
    [Min(0.01f)] public float durationSeconds = 3f;

    public override void OnEquipped(RelicContext ctx) => Register(ctx);
    public override void OnRestoreAttached(RelicContext ctx) => Register(ctx);
    public override void OnRestoreDetached(RelicContext ctx) => OnUnequipped(ctx);

    public Object GetTemporaryBonusSource(RelicContext ctx)
    {
        if (mode != BonusMode.AfterDash || ctx.owner == null)
            return null;
        var manager = ctx.owner.GetComponent<RelicProcManager>();
        if (manager == null)
            return null;
        foreach (var proc in manager.RegisteredProcs)
            if (proc is ElectricAttributeBonusProc bonus && proc.Token == ctx.token)
                return bonus.TemporaryModifierSource;
        return null;
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner != null && ctx.token != null)
            ctx.owner.GetComponent<RelicProcManager>()?.UnregisterAll(ctx.token);
    }

    public override void AppendPreviewModifiers(RelicContext ctx, AttributeDefinition attribute, List<AttributeModifier> results)
    {
        if (attribute == null || attribute != electricAddAttribute || results == null || mode == BonusMode.AfterDash)
            return;

        int units = 0;
        if (mode == BonusMode.NearbyElectrocutedCount && ctx.owner != null)
        {
            units = Enemy.CountLivingWithTagInScene(ctx.owner.scene, electrocutedTag);
        }
        else if (mode == BonusMode.BonusMoveSpeed)
        {
            StatTypeBindings bindings = ctx.abilitySystem != null && ctx.abilitySystem.DamageProfile != null
                ? ctx.abilitySystem.DamageProfile.GetStatBindings() : null;
            if (bindings == null || !bindings.TryGetComposite(StatId.MoveSpeedFinal, out var composite) || composite == null)
                return;

            var provider = new AttributeStatProvider(ctx.attributeSet, bindings);
            float baseSpeed = provider.Get(composite.baseId, ctx.ReadPreviewAttribute);
            float multiplier = baseSpeed > 0f
                ? Mathf.Max(0f, provider.Get(StatId.MoveSpeedFinal, ctx.ReadPreviewAttribute) / baseSpeed) : 1f;
            units = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, multiplier - 1f) /
                Mathf.Max(0.0001f, bonusMoveStep) + 0.00001f), 0, Mathf.Max(1, maxUnits));
        }
        else if (mode == BonusMode.MaxHealth && maxHealthAttribute != null)
        {
            units = CalculateHealthUnits(ctx.ReadPreviewAttribute(maxHealthAttribute), healthStep);
        }

        float bonus = units * Evaluate(bonusPerUnitByLevel, ctx.level);
        if (bonus > 0f)
            results.Add(new AttributeModifier(ModifierType.Flat, bonus, ctx.token));
    }

    private void Register(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null)
            return;

        RelicProcManager manager = ctx.owner.GetComponent<RelicProcManager>();
        manager?.UnregisterAll(ctx.token);

        if (ctx.attributeSet == null || electricAddAttribute == null)
            return;
        if (mode == BonusMode.NearbyElectrocutedCount && electrocutedTag == null)
            return;
        if (mode == BonusMode.AfterDash && ctx.abilitySystem == null)
            return;
        if (mode == BonusMode.MaxHealth && maxHealthAttribute == null)
            return;

        if (manager == null)
            manager = ctx.owner.AddComponent<RelicProcManager>();

        manager.Register(new ElectricAttributeBonusProc(ctx, this));
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        string template = mode switch
        {
            BonusMode.NearbyElectrocutedCount => GameText.Get(
                "relic.electric.scene.template",
                "● [[감전]]된 적 1명당 [[전기 속성]] {value}"),
            BonusMode.BonusMoveSpeed => GameText.Get(
                "relic.electric.move.template",
                "● [[추가 이동속도]] {step}마다 [[전기 속성]] {value}\n● 최대 {cap}단계까지 적용"),
            BonusMode.AfterDash => GameText.Get(
                "relic.electric.dash.template",
                "● 이동 스킬 사용 시 {duration} 동안 [[전기 속성]] {value}\n● 최대 4중첩, 다시 사용하면 모든 중첩의 지속시간 갱신"),
            BonusMode.MaxHealth => GameText.Get(
                "relic.electric.health.template",
                "● [[최대 체력]] {health_step}칸마다 [[전기 속성]] {value}"),
            _ => string.Empty
        };

        float value = Evaluate(mode == BonusMode.AfterDash ? flatBonusByLevel : bonusPerUnitByLevel, previewLevel);
        return new RelicTooltipData
        {
            effectText = RelicTooltipFormatter.ReplaceTokens(template, new Dictionary<string, string>
            {
                ["value"] = RelicTooltipFormatter.FormatSignedValueToken(value, false),
                ["cap"] = RelicTooltipFormatter.FormatUnsignedValueToken(Mathf.Max(1, maxUnits), false),
                ["step"] = RelicTooltipFormatter.FormatUnsignedValueToken(Mathf.Max(0.0001f, bonusMoveStep), true),
                ["health_step"] = RelicTooltipFormatter.FormatUnsignedValueToken(Mathf.Max(0.0001f, healthStep), false),
                ["duration"] = RelicTooltipFormatter.FormatSeconds(Mathf.Max(0.01f, durationSeconds))
            })
        };
    }

    private static float Evaluate(List<float> values, int level)
    {
        if (values == null || values.Count == 0)
            return 0f;
        return Mathf.Max(0f, values[Mathf.Clamp(level - 1, 0, values.Count - 1)]);
    }

    private static int CalculateHealthUnits(float maxHealth, float step)
    {
        return Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f, maxHealth) / Mathf.Max(0.0001f, step)));
    }

    private sealed class ElectricAttributeBonusProc : IRelicProc
    {
        private const float SceneRefreshInterval = 0.1f;
        private readonly RelicContext ctx;
        private readonly BonusMode mode;
        private readonly AttributeDefinition electricAddAttribute;
        private readonly GameplayTag electrocutedTag;
        private readonly GameplayTag movementAbilityTag;
        private readonly int maxUnits;
        private readonly float moveStep;
        private readonly float bonusPerUnit;
        private readonly float dashBonus;
        private readonly float dashDuration;
        private readonly AttributeDefinition maxHealthAttribute;
        private readonly float healthStep;
        private readonly RelicRuntimeToken dashBuffSource;
        private AttributeStatProvider statProvider;
        private StatId moveSpeedBaseId;
        private AttributeDefinition watchBase;
        private AttributeDefinition watchAdd;
        private AttributeDefinition watchMul;
        private AttributeModifier currentModifier;
        private float sceneRefreshRemaining;
        private int movementStackCount;
        private bool disposed;

        public Object Token => ctx.token;
        public Object TemporaryModifierSource => dashBuffSource;

        public ElectricAttributeBonusProc(RelicContext ctx, RelicLogic_ElectricAttributeBonus_Managed logic)
        {
            this.ctx = ctx;
            mode = logic.mode;
            electricAddAttribute = logic.electricAddAttribute;
            electrocutedTag = logic.electrocutedTag;
            maxUnits = Mathf.Max(1, logic.maxUnits);
            moveStep = Mathf.Max(0.0001f, logic.bonusMoveStep);
            bonusPerUnit = Evaluate(logic.bonusPerUnitByLevel, ctx.level);
            dashBonus = Evaluate(logic.flatBonusByLevel, ctx.level);
            dashDuration = Mathf.Max(0.01f, logic.durationSeconds);
            maxHealthAttribute = logic.maxHealthAttribute;
            healthStep = Mathf.Max(0.0001f, logic.healthStep);

            switch (mode)
            {
                case BonusMode.NearbyElectrocutedCount:
                    RefreshSceneBonus();
                    break;
                case BonusMode.BonusMoveSpeed:
                    ConfigureMoveSpeedWatch();
                    ctx.attributeSet.OnAttributeChanged += OnAttributeChanged;
                    RefreshMoveSpeedBonus();
                    break;
                case BonusMode.AfterDash:
                    // A selection preview reapplies permanent relic-token modifiers only.
                    dashBuffSource = ScriptableObject.CreateInstance<RelicRuntimeToken>();
                    dashBuffSource.hideFlags = HideFlags.HideAndDontSave;
                    movementAbilityTag = Resources.Load<GameplayTag>(MovementAbilityTagResourcePath);
                    ctx.abilitySystem.AbilityExecutionStarted += OnAbilityExecutionStarted;
                    break;
                case BonusMode.MaxHealth:
                    ctx.attributeSet.OnAttributeChanged += OnAttributeChanged;
                    RefreshHealthBonus();
                    break;
            }
        }

        public void Handle(GameplayTag tag, AbilityEventData data) { }

        public void Tick(float deltaTime)
        {
            if (disposed)
                return;
            if (mode == BonusMode.AfterDash)
            {
                if (currentModifier != null && currentModifier.TimeRemaining <= 0f)
                {
                    movementStackCount = 0;
                    RemoveCurrentModifier();
                }
                return;
            }
            if (mode != BonusMode.NearbyElectrocutedCount)
                return;

            sceneRefreshRemaining -= deltaTime;
            if (sceneRefreshRemaining <= 0f)
                RefreshSceneBonus();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if ((mode == BonusMode.BonusMoveSpeed || mode == BonusMode.MaxHealth) && ctx.attributeSet != null)
                ctx.attributeSet.OnAttributeChanged -= OnAttributeChanged;
            if (mode == BonusMode.AfterDash && ctx.abilitySystem != null)
                ctx.abilitySystem.AbilityExecutionStarted -= OnAbilityExecutionStarted;
            movementStackCount = 0;
            RemoveCurrentModifier();
            if (dashBuffSource != null)
                Object.Destroy(dashBuffSource);
        }

        private void RefreshSceneBonus()
        {
            sceneRefreshRemaining = SceneRefreshInterval;
            int count = ctx.owner != null
                ? Enemy.CountLivingWithTagInScene(ctx.owner.scene, electrocutedTag)
                : 0;
            ApplyBonus(count * bonusPerUnit);
        }

        private void ConfigureMoveSpeedWatch()
        {
            StatTypeBindings bindings = ctx.abilitySystem != null && ctx.abilitySystem.DamageProfile != null
                ? ctx.abilitySystem.DamageProfile.GetStatBindings() : null;
            if (bindings == null || !bindings.TryGetComposite(StatId.MoveSpeedFinal, out var composite) || composite == null)
                return;

            statProvider = new AttributeStatProvider(ctx.attributeSet, bindings);
            moveSpeedBaseId = composite.baseId;
            watchBase = ResolveAttribute(bindings, composite.baseId);
            watchAdd = ResolveAttribute(bindings, composite.addId);
            watchMul = ResolveAttribute(bindings, composite.mulId);
        }

        private static AttributeDefinition ResolveAttribute(StatTypeBindings bindings, StatId id)
        {
            return bindings.TryGetBinding(id, out var binding) && binding != null ? binding.attribute : null;
        }

        private void OnAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
        {
            if (mode == BonusMode.MaxHealth)
            {
                if (attribute == maxHealthAttribute)
                    RefreshHealthBonus();
                return;
            }

            if (attribute != null && (attribute == watchBase || attribute == watchAdd || attribute == watchMul))
                RefreshMoveSpeedBonus();
        }

        private void RefreshHealthBonus()
        {
            int steps = CalculateHealthUnits(ctx.attributeSet.GetCurrentValue(maxHealthAttribute), healthStep);
            ApplyBonus(steps * bonusPerUnit);
        }

        private void RefreshMoveSpeedBonus()
        {
            if (statProvider == null)
                return;

            float baseSpeed = statProvider.Get(moveSpeedBaseId);
            float multiplier = baseSpeed > 0f ? Mathf.Max(0f, statProvider.Get(StatId.MoveSpeedFinal) / baseSpeed) : 1f;
            int steps = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, multiplier - 1f) / moveStep + 0.00001f), 0, maxUnits);
            ApplyBonus(steps * bonusPerUnit);
        }

        private void OnAbilityExecutionStarted(AbilitySpec spec)
        {
            if (disposed || ctx.owner == null || !ctx.owner.activeInHierarchy || spec?.Definition == null ||
                spec.Definition.logic == null || !spec.Definition.HasAbilityTag(movementAbilityTag))
                return;

            if (currentModifier == null || currentModifier.TimeRemaining <= 0f)
                movementStackCount = 0;
            movementStackCount = Mathf.Min(MaximumMovementStacks, movementStackCount + 1);
            // One timed total modifier owns all stacks; even a capped use refreshes it.
            RemoveCurrentModifier();
            ApplyBonus(movementStackCount * dashBonus, dashDuration);
        }

        private void ApplyBonus(float value, float duration = 0f)
        {
            if (currentModifier != null && Mathf.Approximately(currentModifier.Value, value))
                return;

            RemoveCurrentModifier();
            if (ctx.attributeSet == null || value <= 0f)
                return;

            currentModifier = new AttributeModifier(
                ModifierType.Flat, value, mode == BonusMode.AfterDash ? dashBuffSource : Token, duration);
            if (!ctx.attributeSet.TryAddModifier(electricAddAttribute, currentModifier))
                currentModifier = null;
        }

        private void RemoveCurrentModifier()
        {
            if (currentModifier != null && ctx.attributeSet != null)
                ctx.attributeSet.TryRemoveModifier(electricAddAttribute, currentModifier);
            currentModifier = null;
        }
    }
}

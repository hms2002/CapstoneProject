using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityGAS;
using Object = UnityEngine.Object;

[CreateAssetMenu(menuName = "Game/Relic Logic/Timed Stat On Gameplay Event (Managed)")]
public sealed class RelicLogic_TimedStatOnGameplayEvent_Managed : RelicLogic
{
    [System.Serializable]
    public struct PassiveEntry
    {
        public AttributeDefinition attribute;
        public string displayNameOverride;
        public ModifierType modifierType;
        public float value;
    }

    public enum EventActorFilter
    {
        None,
        InstigatorIsOwner,
        TargetIsOwner
    }

    protected override string DefaultEffectTemplate => "{trigger}: {duration} 동안 [[{stat}]] {value}";

    [Header("Trigger")]
    public GameplayTag triggerTag;
    public EventActorFilter actorFilter = EventActorFilter.None;
    public string triggerLabel = "조건 충족 시";

    [Header("Passive Modifiers")]
    public List<PassiveEntry> passiveEntries = new();

    [Header("Buff")]
    public AttributeDefinition attribute;
    public string displayNameOverride;
    public ModifierType modifierType = ModifierType.Flat;
    public float value;
    [Min(0.01f)] public float durationSeconds = 4f;
    public bool refreshDuration = true;

    [Header("Status HUD")]
    public StatusHudDefinition statusDefinition;

    public override void OnEquipped(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.attributeSet != null && ctx.token != null)
            ctx.attributeSet.RemoveModifiersFromSource(ctx.token);

        if (ctx.owner == null || ctx.token == null)
            return;

        RelicProcManager manager = ctx.owner.GetComponent<RelicProcManager>();
        manager?.UnregisterAll(ctx.token);
    }

    public override void OnRestoreAttached(RelicContext ctx)
    {
        RegisterProc(ctx);
    }

    private void RegisterProc(RelicContext ctx)
    {
        if (ctx.owner == null || ctx.token == null)
            return;

        if (ctx.attributeSet == null || triggerTag == null || attribute == null)
            return;

        ApplyPassiveModifiers(ctx);

        RelicProcManager manager = ctx.owner.GetComponent<RelicProcManager>();
        if (manager == null)
            manager = ctx.owner.AddComponent<RelicProcManager>();

        manager.Register(new TimedStatOnGameplayEventProc(
            ctx,
            triggerTag,
            actorFilter,
            attribute,
            modifierType,
            value,
            durationSeconds,
            refreshDuration,
            statusDefinition));
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        string displayName = ResolveDisplayName();
        RelicTooltipData timedTooltip = BuildTemplatedTooltip(
            DefaultEffectTemplate,
            new Dictionary<string, string>
            {
                ["trigger"] = string.IsNullOrWhiteSpace(triggerLabel) ? "조건 충족 시" : triggerLabel,
                ["duration"] = RelicTooltipFormatter.FormatSeconds(durationSeconds),
                ["stat"] = displayName,
                ["value"] = RelicTooltipFormatter.FormatSignedValueToken(
                    value,
                    RelicTooltipFormatter.ShouldDisplayAsPercent(attribute, displayName, modifierType))
            });

        string passiveText = BuildPassiveTooltipText();
        if (string.IsNullOrWhiteSpace(passiveText))
            return timedTooltip;

        return new RelicTooltipData
        {
            effectText = $"{passiveText}\n{timedTooltip.effectText}"
        };
    }

    private string ResolveDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayNameOverride))
            return displayNameOverride;

        if (attribute == null)
            return string.Empty;

        return !string.IsNullOrEmpty(attribute.attributeName)
            ? attribute.attributeName
            : attribute.name;
    }

    private void ApplyPassiveModifiers(RelicContext ctx)
    {
        if (ctx.attributeSet == null || ctx.token == null || passiveEntries == null)
            return;

        for (int i = 0; i < passiveEntries.Count; i++)
        {
            PassiveEntry entry = passiveEntries[i];
            if (entry.attribute == null)
                continue;

            var modifier = new AttributeModifier(
                entry.modifierType,
                entry.value,
                ctx.token,
                duration: 0f);

            ctx.attributeSet.TryAddModifier(entry.attribute, modifier);
        }
    }

    private string BuildPassiveTooltipText()
    {
        if (passiveEntries == null || passiveEntries.Count == 0)
            return string.Empty;

        var lines = new List<string>();
        for (int i = 0; i < passiveEntries.Count; i++)
        {
            PassiveEntry entry = passiveEntries[i];
            if (entry.attribute == null)
                continue;

            string displayName = ResolvePassiveDisplayName(entry);
            string valueText = RelicTooltipFormatter.FormatSignedValueToken(
                entry.value,
                RelicTooltipFormatter.ShouldDisplayAsPercent(entry.attribute, displayName, entry.modifierType));
            lines.Add($"[[{displayName}]] {valueText}");
        }

        return string.Join("\n", lines);
    }

    private static string ResolvePassiveDisplayName(PassiveEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.displayNameOverride))
            return entry.displayNameOverride;

        if (entry.attribute == null)
            return string.Empty;

        return !string.IsNullOrEmpty(entry.attribute.attributeName)
            ? entry.attribute.attributeName
            : entry.attribute.name;
    }

    private sealed class TimedStatOnGameplayEventProc : IRelicProc
    {
        public Object Token { get; }

        private readonly RelicContext ctx;
        private readonly GameplayTag triggerTag;
        private readonly EventActorFilter actorFilter;
        private readonly AttributeDefinition attribute;
        private readonly ModifierType modifierType;
        private readonly float value;
        private readonly float durationSeconds;
        private readonly bool refreshDuration;
        private readonly StatusHudDefinition statusDefinition;
        private readonly RelicRuntimeToken buffSource;
        private readonly List<float> activeExpiryTimes = new();
        private readonly PlayerStatusRuntime playerStatusRuntime;
        private readonly string ownerKey;
        private StatusHandle statusHandle;

        public TimedStatOnGameplayEventProc(
            RelicContext ctx,
            GameplayTag triggerTag,
            EventActorFilter actorFilter,
            AttributeDefinition attribute,
            ModifierType modifierType,
            float value,
            float durationSeconds,
            bool refreshDuration,
            StatusHudDefinition statusDefinition)
        {
            this.ctx = ctx;
            Token = ctx.token;
            this.triggerTag = triggerTag;
            this.actorFilter = actorFilter;
            this.attribute = attribute;
            this.modifierType = modifierType;
            this.value = value;
            this.durationSeconds = Mathf.Max(0.01f, durationSeconds);
            this.refreshDuration = refreshDuration;
            this.statusDefinition = statusDefinition;

            buffSource = ScriptableObject.CreateInstance<RelicRuntimeToken>();
            buffSource.hideFlags = HideFlags.HideAndDontSave;

            playerStatusRuntime = ctx.owner != null ? PlayerStatusRuntime.GetOrAdd(ctx.owner) : null;
            string relicId = ctx.relicDef != null ? ctx.relicDef.relicId : "relic";
            int tokenId = Token != null ? RuntimeHelpers.GetHashCode(Token) : 0;
            ownerKey = $"relic.timed_stat_event.{relicId}.{tokenId}";
        }

        public void Handle(GameplayTag tag, AbilityEventData data)
        {
            if (ctx.attributeSet == null || attribute == null || triggerTag == null)
                return;

            if (tag != triggerTag)
                return;

            if (!PassesActorFilter(data))
                return;

            ApplyOrRefreshBuff();
        }

        public void Tick(float deltaTime)
        {
            if (activeExpiryTimes.Count == 0)
                return;

            float now = Time.time;
            bool removedAny = false;
            for (int i = activeExpiryTimes.Count - 1; i >= 0; i--)
            {
                if (activeExpiryTimes[i] > now)
                    continue;

                activeExpiryTimes.RemoveAt(i);
                removedAny = true;
            }

            if (removedAny || statusHandle.IsValid)
                PublishStatusHud();
        }

        public void Dispose()
        {
            ReleaseStatusHud();

            if (ctx.attributeSet != null)
                ctx.attributeSet.RemoveModifiersFromSource(buffSource);

            if (buffSource != null)
                Object.Destroy(buffSource);
        }

        private bool PassesActorFilter(AbilityEventData data)
        {
            if (actorFilter == EventActorFilter.None)
                return true;

            if (ctx.owner == null)
                return false;

            return actorFilter switch
            {
                EventActorFilter.InstigatorIsOwner => data.Instigator == ctx.owner,
                EventActorFilter.TargetIsOwner => data.Target == ctx.owner,
                _ => true
            };
        }

        private void ApplyOrRefreshBuff()
        {
            if (refreshDuration)
            {
                ctx.attributeSet.RemoveModifiersFromSource(buffSource);
                activeExpiryTimes.Clear();
            }

            var modifier = new AttributeModifier(
                modifierType,
                value,
                buffSource,
                duration: durationSeconds);

            ctx.attributeSet.TryAddModifier(attribute, modifier);
            activeExpiryTimes.Add(Time.time + durationSeconds);
            PublishStatusHud();
        }

        private void PublishStatusHud()
        {
            if (statusDefinition == null || playerStatusRuntime == null)
                return;

            int activeCount = activeExpiryTimes.Count;
            if (activeCount <= 0)
            {
                ReleaseStatusHud();
                return;
            }

            float remainingTime = 0f;
            float now = Time.time;
            for (int i = 0; i < activeExpiryTimes.Count; i++)
            {
                float remaining = Mathf.Max(0f, activeExpiryTimes[i] - now);
                if (remaining > remainingTime)
                    remainingTime = remaining;
            }

            StatusApplyRequest request = new(
                statusDefinition,
                ownerKey,
                stackCount: activeCount,
                remainingTime: remainingTime,
                maxTime: durationSeconds,
                isVisible: true,
                showStacksOverride: !refreshDuration && activeCount > 1,
                showDurationOverride: true);

            if (statusHandle.IsValid)
            {
                playerStatusRuntime.UpdateStatus(statusHandle, request);
                return;
            }

            statusHandle = playerStatusRuntime.Apply(request);
        }

        private void ReleaseStatusHud()
        {
            if (!statusHandle.IsValid)
                return;

            statusHandle.Release();
            statusHandle = default;
        }
    }
}

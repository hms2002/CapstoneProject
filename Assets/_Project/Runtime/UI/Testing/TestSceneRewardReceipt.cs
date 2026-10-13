using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// Scene-owned undo receipt for the two instant reward types. A later change to an
/// affected resource invalidates the receipt rather than overwriting that change.
/// </summary>
internal sealed class TestSceneRewardReceipt
{
    private readonly PlayerInteractor2D player;
    private readonly Dictionary<AttributeDefinition, float> attributeBefore = new();
    private readonly Dictionary<AttributeDefinition, float> attributeAfter = new();
    private readonly Dictionary<AttributeDefinition, float> relicAttributeBefore = new();
    private readonly Dictionary<AttributeDefinition, float> relicAttributeAfter = new();
    private readonly List<RelicChange> relicChanges = new();
    private RelicDefinition[] relicBefore;
    private int[] levelBefore;
    private bool supported = true;

    private readonly struct RelicChange
    {
        public readonly int Slot;
        public readonly RelicDefinition Definition;
        public readonly int Before;
        public readonly int After;

        public RelicChange(int slot, RelicDefinition definition, int before, int after)
        {
            Slot = slot;
            Definition = definition;
            Before = before;
            After = after;
        }
    }

    public TestSceneRewardReceipt(PlayerInteractor2D player, LevelRewardDefinitionSO definition)
    {
        this.player = player;
        AttributeSet attributes = player.GetComponent<AttributeSet>();
        foreach (LevelRewardEffectSO effect in definition.Effects)
        {
            if (effect == null || effect.Lifetime != LevelRewardEffectLifetime.InstantOnce)
                continue;

            if (effect is SoulHeartGrantLevelRewardEffectSO grant && grant.GrantedAttribute != null)
            {
                attributeBefore[grant.GrantedAttribute] = attributes.GetCurrentValue(grant.GrantedAttribute);
            }
            else if (effect is RandomRelicUpgradeLevelRewardEffectSO)
            {
                RelicInventory inventory = player.GetComponent<RelicInventory>();
                if (inventory == null) { supported = false; continue; }
                relicBefore = new RelicDefinition[inventory.Capacity];
                levelBefore = new int[inventory.Capacity];
                for (int slot = 0; slot < inventory.Capacity; slot++)
                {
                    relicBefore[slot] = inventory.GetRelicInSlot(slot);
                    levelBefore[slot] = inventory.GetRelicLevelInSlot(slot);
                }
                foreach (AttributeDefinition attribute in attributes.EnumerateDefinitions())
                    relicAttributeBefore[attribute] = attributes.GetCurrentValue(attribute);
            }
            else
            {
                supported = false;
            }
        }
    }

    public void Complete()
    {
        AttributeSet attributes = player.GetComponent<AttributeSet>();
        foreach (AttributeDefinition attribute in attributeBefore.Keys)
            attributeAfter[attribute] = attributes.GetCurrentValue(attribute);

        if (relicBefore == null) return;
        foreach (KeyValuePair<AttributeDefinition, float> entry in relicAttributeBefore)
        {
            float after = attributes.GetCurrentValue(entry.Key);
            if (after != entry.Value) relicAttributeAfter[entry.Key] = after;
        }
        RelicInventory inventory = player.GetComponent<RelicInventory>();
        for (int slot = 0; slot < relicBefore.Length; slot++)
        {
            int after = inventory.GetRelicLevelInSlot(slot);
            if (inventory.GetRelicInSlot(slot) != relicBefore[slot])
                supported = false;
            else if (after != levelBefore[slot])
                relicChanges.Add(new RelicChange(slot, relicBefore[slot], levelBefore[slot], after));
        }
    }

    public bool CanRevert(out string failureReason)
    {
        failureReason = null;
        if (!supported || player == null || PlayerRuntimeRegistry.CurrentPlayer != player)
        {
            failureReason = "이 즉시 효과의 안전한 되돌리기 기록이 없습니다. TestScene을 다시 실행하세요.";
            return false;
        }

        AttributeSet attributes = player.GetComponent<AttributeSet>();
        foreach (KeyValuePair<AttributeDefinition, float> entry in attributeAfter)
        {
            if (attributes == null || attributes.GetCurrentValue(entry.Key) != entry.Value)
            {
                failureReason = "지급 후 해당 자원이 변경되어 안전하게 제거할 수 없습니다. TestScene을 다시 실행하세요.";
                return false;
            }
        }
        foreach (KeyValuePair<AttributeDefinition, float> entry in relicAttributeAfter)
        {
            if (attributes == null || attributes.GetCurrentValue(entry.Key) != entry.Value)
            {
                failureReason = "강화 효과가 바꾼 속성이 이후 변경되었습니다. TestScene을 다시 실행하세요.";
                return false;
            }
        }

        RelicInventory inventory = player.GetComponent<RelicInventory>();
        foreach (RelicChange change in relicChanges)
        {
            if (inventory == null || inventory.GetRelicInSlot(change.Slot) != change.Definition ||
                inventory.GetRelicLevelInSlot(change.Slot) != change.After ||
                inventory.PreviewSetRelicSlotWithLevel(change.Slot, change.Definition, change.Before) !=
                    RelicInventory.AcquireResult.Success)
            {
                failureReason = "지급 후 강화 대상 유물이 변경되었거나 해제할 수 없습니다. TestScene을 다시 실행하세요.";
                return false;
            }
        }

        return true;
    }

    public bool TryRevert(Object source, out string failureReason)
    {
        if (!CanRevert(out failureReason)) return false;
        AttributeSet attributes = player.GetComponent<AttributeSet>();
        RelicInventory inventory = player.GetComponent<RelicInventory>();
        for (int i = 0; i < relicChanges.Count; i++)
        {
            RelicChange change = relicChanges[i];
            if (!inventory.TrySetRelicSlotWithLevel(change.Slot, change.Definition, change.Before))
            {
                for (int reverted = i - 1; reverted >= 0; reverted--)
                {
                    RelicChange prior = relicChanges[reverted];
                    inventory.TrySetRelicSlotWithLevel(prior.Slot, prior.Definition, prior.After);
                }
                failureReason = "유물 강화를 되돌리지 못했습니다.";
                return false;
            }
        }
        foreach (KeyValuePair<AttributeDefinition, float> entry in attributeBefore)
        {
            if (!attributes.TrySetCurrentValue(entry.Key, entry.Value, source))
            {
                failureReason = "즉시 지급한 자원을 되돌리지 못했습니다.";
                return false;
            }
        }
        return true;
    }
}

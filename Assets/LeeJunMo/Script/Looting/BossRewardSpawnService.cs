using System.Collections.Generic;
using UnityEngine;

internal readonly struct BossRewardActivationRequest
{
    public BossRewardContext Context { get; }
    public BossSpecialRewardPresetSO SpecialRewardPreset { get; }
    public TreasureChest TreasureChest { get; }
    public Vector3 DropOrigin { get; }
    public Object LogContext { get; }

    public BossRewardActivationRequest(
        BossRewardContext context,
        BossSpecialRewardPresetSO specialRewardPreset,
        TreasureChest treasureChest,
        Vector3 dropOrigin,
        Object logContext)
    {
        Context = context;
        SpecialRewardPreset = specialRewardPreset;
        TreasureChest = treasureChest;
        DropOrigin = dropOrigin;
        LogContext = logContext;
    }
}

internal static class BossRewardSpawnService
{
    private const float PhysicalDropScatterRadius = 0.65f;

    public static bool ActivateTreasureChest(BossRewardActivationRequest request)
    {
        return TryRunRewardStep(
            () => ActivateTreasureChestCore(
                request.Context,
                request.SpecialRewardPreset,
                request.TreasureChest,
                request.DropOrigin),
            "ActivateTreasureChest",
            request.LogContext);
    }

    public static bool SpawnPhysicalDrops(BossRewardContext context, Vector3 dropOrigin, Object logContext)
    {
        return TryRunRewardStep(
            () =>
            {
                SpawnPhysicalDrops(dropOrigin, ResolveRewardModifiers(context));
                return true;
            },
            "SpawnPhysicalDrops",
            logContext);
    }

    private static bool ActivateTreasureChestCore(
        BossRewardContext context,
        BossSpecialRewardPresetSO specialRewardPreset,
        TreasureChest chest,
        Vector3 dropOrigin)
    {
        if (chest == null)
            return false;

        chest.gameObject.SetActive(true);

        var finalLoots = new List<ScriptableObject>();
        BossRewardModifierAggregate modifiers = ResolveRewardModifiers(context);

        if (LootManager.Instance != null)
        {
            List<ScriptableObject> baseLoots = LootManager.Instance.GenerateBossChestLoot(modifiers.ChestModifierDelta);
            if (baseLoots != null)
                finalLoots.AddRange(baseLoots);
        }

        AddRolledBonusLoots(finalLoots, specialRewardPreset != null ? specialRewardPreset.SpecialLoots : null);
        AddRolledBonusLoots(finalLoots, modifiers.BonusLoots);
        chest.InitializeWithLoot(finalLoots);
        SpawnPhysicalDrops(dropOrigin, modifiers);
        return true;
    }

    private static BossRewardModifierAggregate ResolveRewardModifiers(BossRewardContext context)
    {
        return context != null ? context.RewardModifiers : default;
    }

    private static void AddRolledBonusLoots(List<ScriptableObject> finalLoots, IReadOnlyList<BossSpecificLoot> entries)
    {
        if (finalLoots == null || entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            BossSpecificLoot entry = entries[i];
            if (entry.item != null && Random.Range(0, 100) < entry.dropChance)
                finalLoots.Add(entry.item);
        }
    }

    private static void SpawnPhysicalDrops(Vector3 origin, BossRewardModifierAggregate modifiers)
    {
        if (LootManager.Instance == null)
            return;

        int magicStoneCount = Mathf.Max(0, LootManager.Instance.GetBossMagicStoneCount() + modifiers.MagicStoneBonus);
        int fieldHealCount = Mathf.Max(0, LootManager.Instance.GetBossFieldHealBaseCount() + modifiers.FieldHealPickupBonus);

        for (int i = 0; i < magicStoneCount; i++)
            LootManager.Instance.SpawnMagicStonePickup(ResolveDropPosition(origin), 1);

        for (int i = 0; i < fieldHealCount; i++)
            LootManager.Instance.SpawnFieldHealPickup(ResolveDropPosition(origin));
    }

    private static Vector3 ResolveDropPosition(Vector3 origin)
    {
        Vector2 offset = Random.insideUnitCircle * PhysicalDropScatterRadius;
        return origin + new Vector3(offset.x, offset.y, 0f);
    }

    private static bool TryRunRewardStep(System.Func<bool> action, string stepName, Object logContext)
    {
        if (action == null)
            return false;

        try
        {
            return action.Invoke();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(new System.Exception($"[BossBattleEnd] {stepName} failed.", exception), logContext);
            return false;
        }
    }
}

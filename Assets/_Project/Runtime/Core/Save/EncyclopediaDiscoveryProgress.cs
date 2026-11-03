using System;
using System.Collections.Generic;
using UnityEngine;

public enum EncyclopediaDiscoveryKind { Weapon, Relic, Monster }

/// <summary>Actual acquisitions/encounters, independent of loot-table unlocks. Additive v1 save data.</summary>
[Serializable]
public sealed class EncyclopediaDiscoverySaveData
{
    public int version = 1;
    public List<string> weaponIds = new();
    public List<string> relicIds = new();
    public List<string> monsterIds = new();

    public bool Contains(EncyclopediaDiscoveryKind kind, string id) =>
        !string.IsNullOrWhiteSpace(id) && GetIds(kind)?.Contains(id.Trim()) == true;

    public bool Add(EncyclopediaDiscoveryKind kind, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim();
        List<string> ids = kind switch
        {
            EncyclopediaDiscoveryKind.Weapon => weaponIds ??= new(),
            EncyclopediaDiscoveryKind.Relic => relicIds ??= new(),
            _ => monsterIds ??= new()
        };
        if (ids.Contains(id)) return false;
        ids.Add(id);
        return true;
    }

    public void Merge(EncyclopediaDiscoverySaveData source)
    {
        if (source == null || ReferenceEquals(this, source)) return;
        foreach (EncyclopediaDiscoveryKind kind in Enum.GetValues(typeof(EncyclopediaDiscoveryKind)))
            if (source.GetIds(kind) is List<string> ids)
                foreach (string id in ids) Add(kind, id);
    }

    public void Normalize()
    {
        var previous = new EncyclopediaDiscoverySaveData
        { weaponIds = weaponIds, relicIds = relicIds, monsterIds = monsterIds };
        weaponIds = new(); relicIds = new(); monsterIds = new();
        Merge(previous);
        if (version < 1) version = 1;
    }

    private List<string> GetIds(EncyclopediaDiscoveryKind kind) => kind switch
    {
        EncyclopediaDiscoveryKind.Weapon => weaponIds,
        EncyclopediaDiscoveryKind.Relic => relicIds,
        _ => monsterIds
    };
}

/// <summary>Profile + active-run projection. Existing run-end policy owns committing pending discovery.</summary>
public static class EncyclopediaDiscoveryProgress
{
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEvents() => Changed = null;

    public static bool Has(EncyclopediaDiscoveryKind kind, string id)
    {
        if (GameDataStore.Data?.encyclopediaDiscoveryData?.Contains(kind, id) == true) return true;
        if (RunSessionStore.IsRunActive &&
            RunSessionStore.Data?.pendingEncyclopediaDiscoveryData?.Contains(kind, id) == true) return true;
        // Old saves have reliable encounter evidence for killed species, but no item acquisition history.
        return kind == EncyclopediaDiscoveryKind.Monster &&
            ((GameDataStore.Data?.monsterEncyclopediaData?.GetKillCount(id) ?? 0) > 0 ||
             RunSessionStore.GetPendingMonsterEncyclopediaKillCount(id) > 0);
    }

    public static bool Record(EncyclopediaDiscoveryKind kind, string id, string sceneName)
    {
        GameData profile = GameDataStore.Data;
        if (profile == null || string.IsNullOrWhiteSpace(id) ||
            (sceneName?.IndexOf("Test", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 || Has(kind, id)) return false;
        bool added;
        if (RunSessionStore.IsRunActive && RunSessionStore.Data is GamePlayData run)
        {
            run.pendingEncyclopediaDiscoveryData ??= new();
            added = run.pendingEncyclopediaDiscoveryData.Add(kind, id);
        }
        else
        {
            if (kind == EncyclopediaDiscoveryKind.Monster) return false;
            profile.encyclopediaDiscoveryData ??= new();
            added = profile.encyclopediaDiscoveryData.Add(kind, id);
            if (added) GameDataStore.RequestDeferredSave();
        }
        if (added) Changed?.Invoke();
        return added;
    }

    public static void Commit(GamePlayData run, GameData profile)
    {
        if (run != null && profile != null)
        {
            profile.encyclopediaDiscoveryData ??= new();
            profile.encyclopediaDiscoveryData.Merge(run.pendingEncyclopediaDiscoveryData);
        }
        Clear(run);
    }

    public static void Clear(GamePlayData run)
    {
        if (run == null) return;
        run.pendingEncyclopediaDiscoveryData?.weaponIds?.Clear();
        run.pendingEncyclopediaDiscoveryData?.relicIds?.Clear();
        run.pendingEncyclopediaDiscoveryData?.monsterIds?.Clear();
    }
}

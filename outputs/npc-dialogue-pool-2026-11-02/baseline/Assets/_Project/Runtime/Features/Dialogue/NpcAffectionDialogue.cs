using System;
using UnityEngine;

[Serializable]
public sealed class NpcAffectionDialogueEntry
{
    [Min(0)] public int affectionLevel;
    public string startPath;
}

/// <summary>Reads run eligibility and selects authored ordinary-NPC Ink paths. Owns no progress.</summary>
public static class NpcAffectionDialogue
{
    public static int GetAffection(NPCData npc)
    {
        if (npc == null) return 0;
        int saved = 0;
        var records = GameDataStore.Data?.affectionData?.affectionRecords;
        if (records != null)
            foreach (var record in records)
                if (record.npcId == npc.id) saved = record.amount;
        var pending = RunSessionStore.Data?.pendingRunAffectionChanges;
        if (pending != null)
            foreach (var change in pending)
                if (change.npcId == npc.id) saved += change.delta;
        if (AffectionManager.Instance == null) return saved;
        int current = AffectionManager.Instance.GetAffection(npc.id);
        return current != 0 || saved == 0 ? current : saved;
    }

    public static int GetMaximumAffection(NPCData npc)
    {
        int maximum = 0;
        if (npc?.affectionRewards != null)
            foreach (var reward in npc.affectionRewards)
                if (reward.effect != null) maximum = Mathf.Max(maximum, reward.targetLevel);
        return maximum;
    }

    public static bool CanOffer(NPCData npc)
    {
        if (npc == null || !npc.UsesRunAffectionDialogue || !RunSessionStore.IsRunActive ||
            RunSessionStore.Data.affectionGainNpcIds?.Contains(npc.id) == true) return false;
        int level = GetAffection(npc);
        if (level >= GetMaximumAffection(npc)) return false;
        foreach (var entry in npc.affectionDialogues)
            if (entry != null && entry.affectionLevel == level && !string.IsNullOrWhiteSpace(entry.startPath)) return true;
        return false;
    }

    public static bool TrySelect(NPCData npc, bool backpackFull, out string path, out bool isAffection)
    {
        path = null;
        isAffection = CanOffer(npc);
        if (npc == null || !npc.UsesRunAffectionDialogue) return false;
        if (isAffection)
        {
            int level = GetAffection(npc), count = 0;
            foreach (var entry in npc.affectionDialogues)
                if (entry != null && entry.affectionLevel == level && !string.IsNullOrWhiteSpace(entry.startPath)) count++;
            int selected = UnityEngine.Random.Range(0, count);
            foreach (var entry in npc.affectionDialogues)
                if (entry != null && entry.affectionLevel == level && !string.IsNullOrWhiteSpace(entry.startPath) && selected-- == 0)
                { path = entry.startPath; return true; }
        }
        var paths = backpackFull && npc.fullBackpackGreetingPaths != null && npc.fullBackpackGreetingPaths.Length > 0
            ? npc.fullBackpackGreetingPaths : npc.greetingPaths;
        if (paths == null || paths.Length == 0) return false;
        path = paths[UnityEngine.Random.Range(0, paths.Length)];
        return !string.IsNullOrWhiteSpace(path);
    }

    public static bool IsBackpackFull(PlayerBackpackInventory backpack)
    {
        if (backpack == null || backpack.Capacity == 0) return false;
        for (int i = 0; i < backpack.Capacity; i++)
            if (backpack.Get(i) == null) return false;
        return true;
    }
}

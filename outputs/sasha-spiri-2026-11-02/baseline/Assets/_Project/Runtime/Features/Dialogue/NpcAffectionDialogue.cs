using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class NpcAffectionDialogueEntry
{
    [Min(0), Tooltip("이 대화가 해금되는 최소 호감도입니다. 이후 단계에서도 계속 후보에 포함됩니다.")]
    public int affectionLevel;
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
        return npc != null && GetAffection(npc) < GetMaximumAffection(npc) && CanStartConversation(npc);
    }

    // Choice conversations keep their run allowance at the cap; the bubble/gain gate above does not.
    public static bool CanStartConversation(NPCData npc)
    {
        if (npc == null || !npc.UsesRunAffectionDialogue || !RunSessionStore.IsRunActive ||
            RunSessionStore.Data.affectionGainNpcIds?.Contains(npc.id) == true) return false;
        int level = GetAffection(npc);
        foreach (var entry in npc.affectionDialogues)
            if (entry != null && entry.affectionLevel <= level && !string.IsNullOrWhiteSpace(entry.startPath)) return true;
        return false;
    }

    public static bool TrySelect(NPCData npc, bool backpackFull, out string path, out bool isAffection)
    {
        path = null;
        isAffection = CanStartConversation(npc);
        if (npc == null || !npc.UsesRunAffectionDialogue) return false;
        if (isAffection)
        {
            int level = GetAffection(npc);
            var unlockedPaths = new List<string>();
            foreach (var entry in npc.affectionDialogues)
                if (entry != null && entry.affectionLevel <= level && !string.IsNullOrWhiteSpace(entry.startPath) &&
                    !unlockedPaths.Contains(entry.startPath)) unlockedPaths.Add(entry.startPath);
            path = unlockedPaths[UnityEngine.Random.Range(0, unlockedPaths.Count)];
            return true;
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

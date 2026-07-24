using System.Collections.Generic;

// =========================================================
// [New] 아이템 해금 저장 데이터
// =========================================================
[System.Serializable]
public class ItemSaveData
{
    // 해금된 무기 ID 목록
    public List<string> unlockedWeaponIDs = new List<string>();

    // 해금된 유물 ID 목록
    public List<string> unlockedRelicIDs = new List<string>();
}

// =========================================================
// [기존] 숏컷/맵 데이터
// =========================================================
[System.Serializable]
public class StageProgress
{
    public string mapID;
    public List<string> unlockedShortcuts = new List<string>();

    public StageProgress(string id) { mapID = id; }
}

[System.Serializable]
public class MapSaveData
{
    public List<StageProgress> stageProgressList = new List<StageProgress>();

    public StageProgress GetStageData(string mapID)
    {
        var data = stageProgressList.Find(x => x.mapID == mapID);
        if (data == null)
        {
            data = new StageProgress(mapID);
            stageProgressList.Add(data);
        }
        return data;
    }
}

// =========================================================
// [기존] 호감도 데이터
// =========================================================
[System.Serializable]
public class AffectionRecord
{
    public int npcId;
    public int amount;
    public AffectionRecord(int id, int val) { npcId = id; amount = val; }
}

[System.Serializable]
public class AffectionSaveData
{
    public List<AffectionRecord> affectionRecords = new List<AffectionRecord>();
}

// =========================================================
// Boss encounter dialogue state
// =========================================================
[System.Serializable]
public class BossDialogueRecord
{
    public int npcId;
    public int encounterCount;
    public int victoryCount;
    public int defeatCount;
    public float lastEncounterTotalPlaySeconds;

    public BossDialogueRecord(int id)
    {
        npcId = id;
    }
}

[System.Serializable]
public class BossDialogueSaveData
{
    public List<BossDialogueRecord> bossRecords = new List<BossDialogueRecord>();

    public BossDialogueRecord GetOrCreateRecord(int npcId)
    {
        bossRecords ??= new List<BossDialogueRecord>();

        BossDialogueRecord record = bossRecords.Find(x => x != null && x.npcId == npcId);
        if (record != null)
            return record;

        record = new BossDialogueRecord(npcId);
        bossRecords.Add(record);
        return record;
    }
}

// =========================================================
// Run-internal special NPC state
// =========================================================
[System.Serializable]
public class RunSpecialNpcConstructionRecord
{
    public string constructionId;
    public int startedClearCount;
    public bool completed;

    public RunSpecialNpcConstructionRecord(string id, int clearCount)
    {
        constructionId = id;
        startedClearCount = clearCount;
    }
}

[System.Serializable]
public class RunSpecialNpcSaveData
{
    public List<RunSpecialNpcConstructionRecord> constructionRecords = new List<RunSpecialNpcConstructionRecord>();

    public RunSpecialNpcConstructionRecord FindConstructionRecord(string constructionId)
    {
        if (string.IsNullOrWhiteSpace(constructionId))
            return null;

        constructionRecords ??= new List<RunSpecialNpcConstructionRecord>();
        return constructionRecords.Find(x => x != null && x.constructionId == constructionId);
    }

    public RunSpecialNpcConstructionRecord GetOrCreateConstructionRecord(string constructionId, int startedClearCount)
    {
        constructionRecords ??= new List<RunSpecialNpcConstructionRecord>();

        RunSpecialNpcConstructionRecord record = FindConstructionRecord(constructionId);
        if (record != null)
            return record;

        record = new RunSpecialNpcConstructionRecord(constructionId, startedClearCount);
        constructionRecords.Add(record);
        return record;
    }
}

// =========================================================
// Tutorial guide completion state
// =========================================================
[System.Serializable]
public class TutorialSaveData
{
    public List<string> completedTutorialIds = new List<string>();

    public bool IsCompleted(string tutorialId)
    {
        if (string.IsNullOrWhiteSpace(tutorialId))
            return false;

        completedTutorialIds ??= new List<string>();
        return completedTutorialIds.Contains(tutorialId);
    }

    public bool MarkCompleted(string tutorialId)
    {
        if (string.IsNullOrWhiteSpace(tutorialId))
            return false;

        completedTutorialIds ??= new List<string>();
        if (completedTutorialIds.Contains(tutorialId))
            return false;

        completedTutorialIds.Add(tutorialId);
        return true;
    }

    public bool ClearCompleted(string tutorialId)
    {
        if (string.IsNullOrWhiteSpace(tutorialId) || completedTutorialIds == null)
            return false;

        return completedTutorialIds.Remove(tutorialId);
    }

    public void Normalize()
    {
        completedTutorialIds ??= new List<string>();
        completedTutorialIds.RemoveAll(string.IsNullOrWhiteSpace);
    }
}

// =========================================================
// [Existing] Upgrade data
// =========================================================
[System.Serializable]
public class UpgradeSaveData
{
    public List<int> purchasedIDs = new List<int>();
    public List<int> unlockedIDs = new List<int>();
    // Legacy cache. Run modifiers are rebuilt from purchasedIDs at runtime.
    public RunModifierSaveData runModifierData = new RunModifierSaveData();

    public UpgradeSaveData()
    {
        unlockedIDs.Add(0);
    }
}

[System.Serializable]
public class RunModifierSaveData
{
    public int extraWeaponGraveCount;
    public int extraRelicGraveCount;
    public int extraWeaponDropCount;
    public int extraRelicDropCount;

    public int weaponGraveMinBonus;
    public int weaponGraveMaxBonus;
    public int relicGraveMinBonus;
    public int relicGraveMaxBonus;

    public int weaponDropMinBonus;
    public int weaponDropMaxBonus;
    public int relicDropMinBonus;
    public int relicDropMaxBonus;

    public int chestWeaponMinBonus;
    public int chestWeaponMaxBonus;
    public int chestRelicMinBonus;
    public int chestRelicMaxBonus;

    public float extraRareChance;
    public float extraEpicChance;
}

// =========================================================
// [통합] 최종 게임 데이터 클래스
// =========================================================
[System.Serializable]
public class GameData
{
    public bool hasInitializedProfile;
    public int magicStone;
    public float totalPlaySeconds;
    public int clearCount;
    public int knownTotalUpgradeCount;

    public UpgradeSaveData upgradeData = new UpgradeSaveData();
    public MapSaveData mapData = new MapSaveData();
    public AffectionSaveData affectionData = new AffectionSaveData();
    public BossDialogueSaveData bossDialogueData = new BossDialogueSaveData();
    public RunSpecialNpcSaveData runSpecialNpcData = new RunSpecialNpcSaveData();
    public TutorialSaveData tutorialData = new TutorialSaveData();

    // [New] 아이템 해금 데이터 포함
    public ItemSaveData itemData = new ItemSaveData();
}

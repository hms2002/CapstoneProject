using System.Collections.Generic;

// =========================================================
// [New] 아이템 해금 저장 데이터
// =========================================================
/// <summary>
/// 책임 : 영구 저장되는 아이템 해금 상태를 보관하는 직렬화 DTO다.
/// </summary>
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
/// <summary>
/// 책임 : 스테이지별 해금된 숏컷 목록을 보관하는 직렬화 DTO다.
/// </summary>
[System.Serializable]
public class StageProgress
{
    public string mapID;
    public List<string> unlockedShortcuts = new List<string>();

    public StageProgress(string id) { mapID = id; }
}

/// <summary>
/// 책임 : 모든 맵의 숏컷 진행 상태를 보관하고 스테이지별 데이터를 조회/생성한다.
/// </summary>
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
/// <summary>
/// 책임 : NPC 하나의 누적 호감도 값을 보관하는 직렬화 DTO다.
/// </summary>
[System.Serializable]
public class AffectionRecord
{
    public int npcId;
    public int amount;
    public AffectionRecord(int id, int val) { npcId = id; amount = val; }
}

/// <summary>
/// 책임 : NPC 호감도 저장 레코드 목록을 보관하는 직렬화 DTO다.
/// </summary>
[System.Serializable]
public class AffectionSaveData
{
    public List<AffectionRecord> affectionRecords = new List<AffectionRecord>();
}

// =========================================================
// Boss encounter dialogue state
// =========================================================
/// <summary>
/// 책임 : 보스 조우 대사 조건에 필요한 NPC별 조우/승패 기록을 보관한다.
/// </summary>
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

/// <summary>
/// 책임 : 보스 조우 대사 기록 목록을 보관하고 NPC별 레코드를 조회/생성한다.
/// </summary>
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
/// <summary>
/// 책임 : 런 특수 NPC 건설 하나의 시작 시점과 완료 여부를 보관한다.
/// </summary>
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

/// <summary>
/// 책임 : 런 특수 NPC 건설 저장 레코드 목록을 보관하고 건설별 레코드를 조회/생성한다.
/// </summary>
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
/// <summary>
/// 책임 : 튜토리얼 완료 ID 목록을 보관하고 완료 상태를 갱신한다.
/// </summary>
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
/// <summary>
/// 책임 : 영구 업그레이드 구매/해금 상태와 레거시 런 modifier 캐시를 보관한다.
/// </summary>
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

/// <summary>
/// 책임 : 구매한 업그레이드로부터 계산된 런 modifier 값을 저장하는 레거시 캐시 DTO다.
/// </summary>
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
/// <summary>
/// 책임 : 프로필에 영구 저장되는 전체 게임 진행 데이터를 보관하는 루트 DTO다.
/// </summary>
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

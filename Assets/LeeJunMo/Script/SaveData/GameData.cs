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
// [기존] 업그레이드 데이터
// =========================================================
[System.Serializable]
public class UpgradeSaveData
{
    public List<int> purchasedIDs = new List<int>();
    public List<int> unlockedIDs = new List<int>();

    public UpgradeSaveData()
    {
        unlockedIDs.Add(0);
    }
}

// =========================================================
// [통합] 최종 게임 데이터 클래스
// =========================================================
[System.Serializable]
public class GameData
{
    public int magicStone;

    public UpgradeSaveData upgradeData = new UpgradeSaveData();
    public MapSaveData mapData = new MapSaveData();
    public AffectionSaveData affectionData = new AffectionSaveData();

    // [New] 아이템 해금 데이터 포함
    public ItemSaveData itemData = new ItemSaveData();
}
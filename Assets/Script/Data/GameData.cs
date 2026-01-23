using System.Collections.Generic;

// [저장용] 호감도 데이터
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

// [저장용] 업그레이드 데이터
[System.Serializable]
public class UpgradeSaveData
{
    public List<int> purchasedIDs = new List<int>(); // 구매 완료한 ID
    public List<int> unlockedIDs = new List<int>();  // 해금되어 구매 가능한 ID

    public UpgradeSaveData()
    {
        // 0번 노드는 기본적으로 해금 상태로 시작 (기획에 맞게 수정)
        unlockedIDs.Add(0);
    }
}

// [저장용] 맵 데이터
[System.Serializable]
public class MapSaveData
{
    public List<int> unlockedMapFeatureIDs = new List<int>(); // 해금된 지름길 ID 등
}

// [통합 컨테이너] JSON으로 저장될 최상위 클래스
[System.Serializable]
public class GameData
{
    public int gold;       // 재화 1
    public int magicStone; // 재화 2 (구 마정석)

    // 각 시스템별 데이터
    public UpgradeSaveData upgradeData = new UpgradeSaveData();
    public MapSaveData mapData = new MapSaveData();
    public AffectionSaveData affectionData = new AffectionSaveData();
}
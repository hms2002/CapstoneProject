using System.Collections.Generic;

// [기존 유지] 호감도 데이터 레코드
[System.Serializable]
public class AffectionRecord
{
    public int npcId;
    public int amount;
    public AffectionRecord(int id, int val) { npcId = id; amount = val; }
}

// [기존 유지] 호감도 저장 데이터
[System.Serializable]
public class AffectionSaveData
{
    public List<AffectionRecord> affectionRecords = new List<AffectionRecord>();
}

// [기존 유지] 업그레이드 저장 데이터
[System.Serializable]
public class UpgradeSaveData
{
    public List<int> purchasedIDs = new List<int>(); // 구매 완료한 ID
    public List<int> unlockedIDs = new List<int>();  // 해금되어 구매 가능한 ID

    public UpgradeSaveData()
    {
        // 0번 노드는 기본적으로 해금 상태로 시작
        unlockedIDs.Add(0);
    }
}

// =========================================================
// [New] 숏컷 시스템을 위해 새로 추가된 클래스
// =========================================================
[System.Serializable]
public class StageProgress
{
    public string mapID; // 맵(씬) 이름
    public List<string> unlockedShortcuts = new List<string>(); // 해금된 숏컷 ID들

    public StageProgress(string id) { mapID = id; }
}

// [수정됨] 맵 데이터
[System.Serializable]
public class MapSaveData
{
    // 기존에 있던 변수가 있다면 그대로 두셔도 됩니다.
    public List<int> unlockedMapFeatureIDs = new List<int>();

    // [New] 맵 별 숏컷 진행도 리스트 추가
    public List<StageProgress> stageProgressList = new List<StageProgress>();

    // [New] 특정 맵의 데이터를 가져오거나 없으면 생성하는 헬퍼 함수
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

// [통합 컨테이너] JSON으로 저장될 최상위 클래스
[System.Serializable]
public class GameData
{
    public int magicStone;

    // 각 시스템별 데이터
    public UpgradeSaveData upgradeData = new UpgradeSaveData();
    public MapSaveData mapData = new MapSaveData();
    public AffectionSaveData affectionData = new AffectionSaveData();
}
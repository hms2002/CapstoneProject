using System.Collections.Generic;
using UnityEngine;

public class AffectionManager : MonoBehaviour
{
    public static AffectionManager Instance { get; private set; }

    // NPC ID별 현재 레벨 저장 (세이브 시 이 데이터를 저장하면 됩니다)
    private Dictionary<int, int> npcLevelDict = new Dictionary<int, int>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 현재 레벨 가져오기
    public int GetLevel(int npcId)
    {
        return npcLevelDict.ContainsKey(npcId) ? npcLevelDict[npcId] : 1;
    }

    // 레벨 직접 추가 (amount만큼 레벨 상승)
    public void AddLevel(NPCData data, int amount)
    {
        int id = data.id;
        int oldLevel = GetLevel(id);

        if (!npcLevelDict.ContainsKey(id)) npcLevelDict[id] = 1;
        npcLevelDict[id] += amount;

        int newLevel = npcLevelDict[id];
        Debug.Log($"{data.npcName} 레벨 상승: {oldLevel} -> {newLevel}");

        // 건너뛴 레벨이 있을 수 있으므로 사이의 모든 보상을 체크하여 실행
        CheckRewards(data, oldLevel, newLevel);
    }

    private void CheckRewards(NPCData data, int fromLevel, int toLevel)
    {
        foreach (var reward in data.levelRewards)
        {
            // 도달한 레벨이 보상 설정 레벨보다 크거나 같으면 실행 (이미 받은 보상은 제외하는 로직은 추후 세이브와 연동)
            if (reward.targetLevel > fromLevel && reward.targetLevel <= toLevel)
            {
                reward.effect.Execute();
                Debug.Log($"보상 획득: {reward.effect.effectDescription}");
            }
        }
    }

    // 세이브 로드용 초기화
    public void SetLevel(int npcId, int value) => npcLevelDict[npcId] = value;
}
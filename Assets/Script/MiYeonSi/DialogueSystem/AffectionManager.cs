using System;
using System.Collections.Generic;
using UnityEngine;

public class AffectionManager : MonoBehaviour
{
    public static AffectionManager Instance { get; private set; }

    // 런타임에서 빠른 검색을 위해 Dictionary 사용
    private Dictionary<int, int> npcAffectionDic = new Dictionary<int, int>();

    private int currentNpcId;
    private AffectionUI linkedUI;

    public event Action<int, int> OnAffectionChanged;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    // [New] 게임 시작 시 저장된 호감도 데이터 불러오기
    private void Start()
    {
        LoadAffectionData();
    }

    private void LoadAffectionData()
    {
        if (GameDataManager.Instance == null) return;

        // GameData는 List<AffectionRecord> 형태이므로 Dictionary로 변환하여 메모리에 올림
        var savedList = GameDataManager.Instance.Data.affectionData.affectionRecords;

        foreach (var record in savedList)
        {
            if (npcAffectionDic.ContainsKey(record.npcId))
                npcAffectionDic[record.npcId] = record.amount;
            else
                npcAffectionDic.Add(record.npcId, record.amount);
        }

        Debug.Log($"[AffectionManager] 데이터 로드 완료. 기록된 NPC 수: {savedList.Count}");
    }

    public void SetLinkedUI(AffectionUI ui) => linkedUI = ui;

    public void SetCurrentNPC(int npcId)
    {
        currentNpcId = npcId;
        if (linkedUI != null) linkedUI.Setup(GetAffection(npcId));
    }

    public int GetAffection() => GetAffection(currentNpcId);

    public int GetAffection(int npcId)
    {
        if (npcAffectionDic.ContainsKey(npcId)) return npcAffectionDic[npcId];
        return 0;
    }

    public bool AddAffection(NPCData data, int amount)
    {
        if (data == null)
        {
            Debug.LogError("[AffectionManager] NPC 데이터가 Null입니다!");
            DialogueManager.GetInstance()?.ResumeDialogueAfterUI();
            return false;
        }

        int id = data.id;
        int oldAffection = GetAffection(id);

        // 1. 런타임 딕셔너리 갱신
        if (!npcAffectionDic.ContainsKey(id)) npcAffectionDic[id] = 0;
        npcAffectionDic[id] += amount;
        int newAffection = npcAffectionDic[id];

        // [핵심] 2. GameDataManager 데이터 갱신 및 저장
        UpdateGameData(id, newAffection);

        // [디버그] 실제 계산 결과 출력
        Debug.Log($"<color=cyan>[AffectionManager] {data.npcName}(ID:{id}) 호감도 변경: {oldAffection} -> {newAffection} (증가량: {amount})</color>");

        // 보상 체크
        bool hasReward = false;
        foreach (var reward in data.affectionRewards)
        {
            if (reward.targetLevel > oldAffection && reward.targetLevel <= newAffection)
            {
                hasReward = true;
                Debug.Log($"<color=green>[AffectionManager] 보상 달성! 목표 레벨: {reward.targetLevel}</color>");
                break;
            }
        }

        // 연출 실행
        if (linkedUI != null && linkedUI.gameObject.activeInHierarchy)
        {
            Debug.Log("[AffectionManager] UI 연출 시작");
            linkedUI.PlayGainAnimation(oldAffection, newAffection, () =>
            {
                Debug.Log("[AffectionManager] UI 연출 종료 -> 다음 단계 진행");
                if (hasReward) CheckRewards(data, oldAffection, newAffection);
                else DialogueManager.GetInstance().ResumeDialogueAfterUI();
            });
        }
        else
        {
            // UI가 없을 때
            Debug.LogWarning("[AffectionManager] 연결된 AffectionUI가 없거나 비활성화 상태입니다. 연출 없이 값만 변경합니다.");
            if (hasReward) CheckRewards(data, oldAffection, newAffection);
            else DialogueManager.GetInstance()?.ResumeDialogueAfterUI();
        }

        OnAffectionChanged?.Invoke(id, newAffection);
        return true;
    }

    // [New] GameDataManager의 List 데이터를 업데이트하고 파일로 저장
    private void UpdateGameData(int npcId, int amount)
    {
        if (GameDataManager.Instance == null) return;

        var recordList = GameDataManager.Instance.Data.affectionData.affectionRecords;

        // 이미 리스트에 있는지 확인
        var record = recordList.Find(x => x.npcId == npcId);

        if (record != null)
        {
            // 있으면 값 갱신
            record.amount = amount;
        }
        else
        {
            // 없으면 새로 추가
            recordList.Add(new AffectionRecord(npcId, amount));
        }

        // 파일에 쓰기 (영구 저장)
        GameDataManager.Instance.SaveData();
        Debug.Log("[AffectionManager] GameData에 호감도 저장 완료");
    }

    private void CheckRewards(NPCData data, int fromLevel, int toLevel)
    {
        List<AffectionEffect> earnedEffects = new List<AffectionEffect>();

        foreach (var reward in data.affectionRewards)
        {
            if (reward.targetLevel > fromLevel && reward.targetLevel <= toLevel)
            {
                if (reward.effect != null)
                {
                    reward.effect.Execute();
                    earnedEffects.Add(reward.effect);
                }
            }
        }

        if (earnedEffects.Count > 0 && RewardDisplayUI.Instance != null)
        {
            RewardDisplayUI.Instance.ShowReward(null, earnedEffects);
        }
        else
        {
            DialogueManager.GetInstance()?.ResumeDialogueAfterUI();
        }
    }

    public void SetAffection(int npcId, int value)
    {
        npcAffectionDic[npcId] = value;
        // 강제 설정 시에도 저장 로직 실행
        UpdateGameData(npcId, value);
    }
}
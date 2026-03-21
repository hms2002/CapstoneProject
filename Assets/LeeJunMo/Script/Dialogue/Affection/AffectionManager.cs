using System;
using System.Collections.Generic;
using UnityEngine;

public class AffectionManager : MonoBehaviour
{
    public static AffectionManager Instance { get; private set; }

    private Dictionary<int, int> npcAffectionDic = new Dictionary<int, int>();

    private int currentNpcId;
    private AffectionUI linkedUI;

    public event Action<int, int> OnAffectionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LoadAffectionData();
    }

    private void LoadAffectionData()
    {
        if (GameDataManager.Instance == null) return;

        var savedList = GameDataManager.Instance.GetAffectionRecordsSnapshot();

        foreach (var record in savedList)
        {
            if (npcAffectionDic.ContainsKey(record.npcId))
                npcAffectionDic[record.npcId] = record.amount;
            else
                npcAffectionDic.Add(record.npcId, record.amount);
        }
        Debug.Log($"[AffectionManager] 데이터 로드 완료. 기록된 NPC 수: {savedList.Count}");
    }

    public void SetLinkedUI(AffectionUI ui)
    {
        linkedUI = ui;

        // [버그 해결 핵심] UI가 뒤늦게 켜지면서 매니저와 연결될 때,
        // 현재 설정되어 있는 NPC의 호감도 값으로 즉시 화면을 갱신(초기화)합니다!
        linkedUI.Setup(GetAffection(currentNpcId));
    }

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

    public bool AddAffection(NPCData data, int amount, System.Action onComplete = null)
    {
        if (data == null)
        {
            Debug.LogError("[AffectionManager] NPC 데이터가 Null입니다!");
            onComplete?.Invoke();
            return false;
        }

        int id = data.id;
        int oldAffection = GetAffection(id);

        if (!npcAffectionDic.ContainsKey(id)) npcAffectionDic[id] = 0;
        npcAffectionDic[id] += amount;
        int newAffection = npcAffectionDic[id];

        UpdateGameDataMemoryOnly(id, newAffection);

        Debug.Log($"<color=cyan>[AffectionManager] {data.npcName}(ID:{id}) 호감도: {oldAffection} -> {newAffection} (증가량: {amount})</color>");

        bool hasReward = false;
        foreach (var reward in data.affectionRewards)
        {
            if (reward.targetLevel > oldAffection && reward.targetLevel <= newAffection)
            {
                hasReward = true;
                Debug.Log($"<color=green>[AffectionManager] 보상 달성! 목표 레벨: {reward.targetLevel}</color>");
                // [수정] 다중 보상을 놓치지 않기 위해 break를 지웠습니다!
            }
        }

        if (linkedUI != null && linkedUI.gameObject.activeInHierarchy)
        {
            linkedUI.PlayGainAnimation(oldAffection, newAffection, () =>
            {
                if (hasReward) CheckRewards(data, oldAffection, newAffection, onComplete);
                else onComplete?.Invoke();
            });
        }
        else
        {
            if (hasReward) CheckRewards(data, oldAffection, newAffection, onComplete);
            else onComplete?.Invoke();
        }

        OnAffectionChanged?.Invoke(id, newAffection);
        return true;
    }

    private void UpdateGameDataMemoryOnly(int npcId, int amount)
    {
        if (GameDataManager.Instance == null) return;

        GameDataManager.Instance.SetAffectionValue(npcId, amount);
    }

    private void CheckRewards(NPCData data, int fromLevel, int toLevel, System.Action onComplete)
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
            RewardDisplayUI.Instance.ShowReward(null, earnedEffects, onComplete);
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    public void SetAffection(int npcId, int value)
    {
        npcAffectionDic[npcId] = value;
        UpdateGameDataMemoryOnly(npcId, value);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}

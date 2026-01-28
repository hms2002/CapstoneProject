using System;
using System.Collections.Generic;
using UnityEngine;

public class AffectionManager : MonoBehaviour
{
    public static AffectionManager Instance { get; private set; }

    // NPC ID별 호감도 저장
    private Dictionary<int, int> npcAffectionDic = new Dictionary<int, int>();

    // 현재 상호작용 중인 NPC의 ID
    private int currentNpcId;

    // [수정] DialogueManager가 상황에 따라 연결해주는 UI
    private AffectionUI linkedUI;

    public event Action<int, int> OnAffectionChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // [추가] DialogueManager에서 현재 활성화된 UI(BossUI에 있는 것)를 연결해주는 함수
    public void SetLinkedUI(AffectionUI ui)
    {
        linkedUI = ui;
    }

    // 대화가 시작될 때, 현재 대화하는 NPC의 ID를 설정해주는 함수
    public void SetCurrentNPC(int npcId)
    {
        currentNpcId = npcId;

        // UI가 연결되어 있다면 현재 값으로 초기화
        if (linkedUI != null)
        {
            int currentVal = GetAffection(npcId);
            linkedUI.Setup(currentVal);
        }
    }

    public int GetAffection()
    {
        return GetAffection(currentNpcId);
    }

    public int GetAffection(int npcId)
    {
        if (npcAffectionDic.ContainsKey(npcId))
        {
            return npcAffectionDic[npcId];
        }
        return 0;
    }

    public void AddAffection(NPCData data, int amount)
    {
        int id = data.id;
        int oldAffection = GetAffection(id);

        if (!npcAffectionDic.ContainsKey(id))
        {
            npcAffectionDic[id] = 0;
        }

        npcAffectionDic[id] += amount;
        int newAffection = npcAffectionDic[id];

        Debug.Log($"{data.npcName} 호감도 상승: {oldAffection} -> {newAffection}");

        // [수정] 연결된 UI가 있으면 연출 실행, 없으면 바로 보상 체크
        if (linkedUI != null)
        {
            linkedUI.PlayGainAnimation(oldAffection, newAffection, () => {
                CheckRewards(data, oldAffection, newAffection);
            });
        }
        else
        {
            CheckRewards(data, oldAffection, newAffection);
        }

        OnAffectionChanged?.Invoke(id, newAffection);
    }

    private void CheckRewards(NPCData data, int fromLevel, int toLevel)
    {
        foreach (var reward in data.affectionRewards)
        {
            if (reward.targetLevel > fromLevel && reward.targetLevel <= toLevel)
            {
                if (reward.effect != null)
                {
                    reward.effect.Execute();
                    Debug.Log($"보상 획득: {reward.effect.effectDescription}");
                }
            }
        }
    }

    public void SetAffection(int npcId, int value)
    {
        npcAffectionDic[npcId] = value;
    }
}
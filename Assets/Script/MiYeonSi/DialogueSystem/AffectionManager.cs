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
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
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

    public void AddAffection(NPCData data, int amount)
    {
        int id = data.id;
        int oldAffection = GetAffection(id);

        if (!npcAffectionDic.ContainsKey(id)) npcAffectionDic[id] = 0;
        npcAffectionDic[id] += amount;
        int newAffection = npcAffectionDic[id];

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

                    // [추가] 보상 UI 호출 (호감도 리스트 전달)
                    if (RewardDisplayUI.Instance != null)
                    {
                        var effectList = new List<AffectionEffect> { reward.effect };
                        RewardDisplayUI.Instance.ShowReward(null, effectList);
                    }
                }
            }
        }
    }

    public void SetAffection(int npcId, int value) => npcAffectionDic[npcId] = value;
}
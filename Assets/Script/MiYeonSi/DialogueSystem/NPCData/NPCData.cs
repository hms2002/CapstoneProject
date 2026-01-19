using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct LevelReward
{
    public int targetLevel;         // 보상을 줄 특정 레벨
    public AffectionEffect effect;  // 실행할 효과 SO
}

[CreateAssetMenu(menuName = "NPC/NPC_Data")]
public class NPCData : ScriptableObject
{
    public int id;
    public string npcName;
    public bool isBoss;

    [Header("레벨별 보상 설정")]
    public List<LevelReward> levelRewards; // 특정 레벨에 도달했을 때의 보상 리스트
}
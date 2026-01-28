using System.Collections.Generic;
using UnityEngine;

// [New] NPC 데이터를 중앙에서 관리하는 데이터베이스 에셋
[CreateAssetMenu(fileName = "NPC Database", menuName = "Scriptable Object/System/NPC Database")]
public class NPCDatabase : ScriptableObject
{
    [Header("등록된 NPC 목록")]
    public List<NPCData> npcList = new List<NPCData>();

    // ID로 NPC 데이터 찾기 (없으면 null 반환)
    public NPCData GetNPC(int id)
    {
        return npcList.Find(x => x.id == id);
    }
}
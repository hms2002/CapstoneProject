using UnityEngine;
using System.Collections.Generic;

public class MonsterDrop : MonoBehaviour
{
    // 외부(EnemyHealth 등)에서 호출
    public void OnMonsterDead()
    {
        if (LootManager.Instance != null)
        {
            // 플레이어 인벤토리를 뒤지는 더러운 코드는 모두 LootManager가 가져갔습니다!
            // 몬스터는 그저 자기 위치만 넘겨주고 퇴장합니다.
            LootManager.Instance.SpawnMonsterLoot(transform.position);
        }
    }
}
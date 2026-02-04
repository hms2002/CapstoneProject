using UnityEngine;
using System.Collections.Generic;

public class MonsterDrop : MonoBehaviour
{
    // 외부(EnemyHealth 등)에서 호출
    public void OnMonsterDead()
    {
        // 1. 플레이어 무기 정보 (중복 방지용)
        HashSet<string> banList = new HashSet<string>();

        // ✅ [수정됨] 플레이어 인벤토리에서 ID 긁어오기
        if (SampleTopDownPlayer.Instance != null && SampleTopDownPlayer.Instance.weaponInventory != null)
        {
            List<string> playerWeaponIDs = SampleTopDownPlayer.Instance.weaponInventory.GetAllWeaponIDs();
            banList.UnionWith(playerWeaponIDs);
        }

        // 2. 매니저에게 요청
        LootManager.Instance.SpawnMonsterLoot(transform.position, banList);
    }
}
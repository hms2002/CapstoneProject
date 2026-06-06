using System;
using UnityEngine;

public class MonsterDrop : MonoBehaviour
{
    /// <summary>
    /// 몬스터가 죽었을 때 전역으로 아이템 드롭을 요청하는 이벤트입니다.
    /// LootManager 등 전역 루팅 시스템이 이를 구독하여 처리합니다.
    /// </summary>
    public static event Action<Vector3> OnAnyMonsterDropRequested;

    // 외부(EnemyHealth 등)에서 호출
    public void OnMonsterDead()
    {
        // 몬스터는 그저 자기 위치를 담아 드롭 이벤트를 발생시키고 퇴장합니다.
        // 실제 아이템 스폰 로직은 이 이벤트를 구독하는 시스템(LootManager)이 담당합니다.
        OnAnyMonsterDropRequested?.Invoke(transform.position);
    }
}
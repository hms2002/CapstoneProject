using UnityEngine;
using UnityGAS;

/// <summary>
/// 이 클래스의 책임:
/// 맥주 몬스터 프리팹의 공통 Mob FSM 본체 역할을 담당하고,
/// 사망 시 술 장판을 생성하는 맥주 몬스터 고유 기믹을 제공한다.
/// 추적/공격 판단은 같은 오브젝트의 TackleAttack 같은 helper가 맡는다.
/// </summary>
public class BeerMonster : Mob
{
    [Header("Death Puddle")]
    [Tooltip("맥주 몬스터가 사망할 때 생성할 술 장판 프리팹입니다.")]
    [SerializeField] private AlcoholPuddleArea alcoholPuddlePrefab;

    [Tooltip("사망 위치 기준 술 장판 생성 위치 보정값입니다.")]
    [SerializeField] private Vector2 puddleSpawnOffset;

    [Tooltip("꺼두면 사망해도 술 장판을 생성하지 않습니다. 디버그/특수 연출용 스위치입니다.")]
    [SerializeField] private bool spawnPuddleOnDeath = true;

    private bool hasSpawnedDeathPuddle;

    protected override void OnDeathStarted()
    {
        SpawnDeathPuddle();
        base.OnDeathStarted();
    }

    /// <summary>
    /// 책임:
    /// 사망 처리가 여러 경로에서 진입하더라도 술 장판 생성은 한 번만 수행한다.
    /// </summary>
    private void SpawnDeathPuddle()
    {
        if (!spawnPuddleOnDeath || hasSpawnedDeathPuddle)
            return;

        hasSpawnedDeathPuddle = true;

        if (alcoholPuddlePrefab == null)
        {
            Debug.LogWarning($"{nameof(BeerMonster)}: 사망 시 생성할 술 장판 프리팹이 비어 있습니다.", this);
            return;
        }

        Vector3 spawnPosition = transform.position + new Vector3(puddleSpawnOffset.x, puddleSpawnOffset.y, 0f);
        Instantiate(alcoholPuddlePrefab, spawnPosition, Quaternion.identity);
    }
}

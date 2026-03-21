using UnityEngine;

/// <summary>
/// 책임:
/// - MonsterSpawner가 몬스터를 생성할 때 필요한 최소 입력값을 묶는다.
/// - 어떤 몬스터를, 어디에, 어떤 회전으로 생성할지만 표현한다.
/// - 난이도 보정, UI 설치 같은 공통 후처리는 Spawner가 담당한다.
/// </summary>
public readonly struct MonsterSpawnRequest
{
    public readonly GameObject MonsterPrefab;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;

    public MonsterSpawnRequest(GameObject monsterPrefab, Vector3 position, Quaternion rotation)
    {
        MonsterPrefab = monsterPrefab;
        Position = position;
        Rotation = rotation;
    }

    public bool IsValid => MonsterPrefab != null;
}
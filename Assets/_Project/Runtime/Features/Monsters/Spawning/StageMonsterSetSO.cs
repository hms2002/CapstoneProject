using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 런 진행 단계 인덱스를 실제 몬스터 프리팹으로 해석하는 공통 몬스터 세트를 보관한다.
/// - 역할형 절차 방 스폰 지점과 기존 MonsterRoomSpawnProfileSO가 같은 진행도별 몬스터 정의를 재사용하게 한다.
/// </summary>
[CreateAssetMenu(fileName = "StageMonsterSet", menuName = "Gameplay/Monster Spawn/Stage Monster Set")]
public sealed class StageMonsterSetSO : ScriptableObject
{
    [Tooltip("Element 0/1/2는 각각 이번 런에서 보스를 0/1/2마리 처치한 단계에 대응합니다.")]
    [SerializeField] private List<GameObject> stagePrefabs = new();

    public IReadOnlyList<GameObject> StagePrefabs => stagePrefabs;

    /// <summary>
    /// 책임:
    /// - 0-based stageIndex를 받아 해당 단계에서 사용할 몬스터 프리팹을 반환한다.
    /// - stageIndex가 배열 범위를 넘으면 마지막 단계의 몬스터 프리팹으로 보정한다.
    /// </summary>
    public bool TryResolveMonsterPrefab(int stageIndex, out GameObject monsterPrefab)
    {
        monsterPrefab = null;

        if (stagePrefabs == null || stagePrefabs.Count == 0)
            return false;

        int clampedIndex = Mathf.Clamp(stageIndex, 0, stagePrefabs.Count - 1);
        monsterPrefab = stagePrefabs[clampedIndex];
        return monsterPrefab != null;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 책임:
    /// - 테스트와 콘텐츠 설치기가 보스 처치 단계 순서의 몬스터 프리팹을 명시적으로 구성한다.
    /// </summary>
    public void EditorSetStagePrefabs(IReadOnlyList<GameObject> prefabs)
    {
        stagePrefabs = prefabs != null
            ? new List<GameObject>(prefabs)
            : new List<GameObject>();
    }

    private void OnValidate()
    {
        stagePrefabs ??= new List<GameObject>();
    }
#endif
}

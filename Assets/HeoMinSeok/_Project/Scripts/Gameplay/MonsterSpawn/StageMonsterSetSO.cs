using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 런 진행 단계 인덱스를 실제 몬스터 프리팹으로 해석하는 공통 몬스터 세트를 보관한다.
/// - MonsterRoomSpawnProfileSO가 고정 프리팹과 단계형 몬스터 세트를 함께 사용할 수 있게 한다.
/// </summary>
[CreateAssetMenu(fileName = "StageMonsterSet", menuName = "Gameplay/Monster Spawn/Stage Monster Set")]
public sealed class StageMonsterSetSO : ScriptableObject
{
    [Tooltip("Element 0은 이번 런의 첫 번째 route set, Element 1은 두 번째 route set에 대응합니다.")]
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
    private void OnValidate()
    {
        stagePrefabs ??= new List<GameObject>();
    }
#endif
}

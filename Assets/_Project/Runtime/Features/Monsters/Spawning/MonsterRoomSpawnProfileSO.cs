using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임:
/// - 방 크기/종류별 몬스터 스폰 규칙 묶음을 데이터로 보관한다.
/// - 각 방 그룹이 선택할 수 있는 스폰 테이블 목록을 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "MonsterRoomSpawnProfile", menuName = "Gameplay/Monster Spawn/Room Spawn Profile")]
public sealed class MonsterRoomSpawnProfileSO : ScriptableObject
{
    /// <summary>
    /// 책임:
    /// - 방 스폰 테이블에서 사용할 몬스터 프리팹과 가중치를 함께 보관한다.
    /// </summary>
    [Serializable]
    public struct WeightedMonsterEntry
    {
        public GameObject monsterPrefab;
        public float weight;
    }

    /// <summary>
    /// 책임:
    /// - 방 스폰 테이블에서 사용할 단계형 공통 몬스터 세트와 가중치를 함께 보관한다.
    /// - 런 진행 단계에 따라 실제 몬스터 프리팹으로 변환되어 기존 WeightedMonsterEntry와 합쳐진다.
    /// </summary>
    [Serializable]
    public struct WeightedCommonMonsterEntry
    {
        public StageMonsterSetSO monsterSet;
        public float weight;
    }

    /// <summary>
    /// 책임:
    /// - 하나의 방 스폰 규칙 테이블을 표현한다.
    /// - 총 스폰 수와 가중치 엔트리 목록을 함께 보관한다.
    /// </summary>
    [Serializable]
    public sealed class SpawnTable
    {
        [SerializeField] private string tableName = "Default";
        [SerializeField] private int spawnCount = 3;
        [SerializeField] private List<WeightedMonsterEntry> entries = new();
        [SerializeField] private List<WeightedCommonMonsterEntry> commonEntries = new();

        public string TableName => string.IsNullOrWhiteSpace(tableName) ? "Unnamed" : tableName;
        public int SpawnCount => Mathf.Max(0, spawnCount);
        public IReadOnlyList<WeightedMonsterEntry> Entries => entries;
        public IReadOnlyList<WeightedCommonMonsterEntry> CommonEntries => commonEntries;

#if UNITY_EDITOR
        /// <summary>
        /// 책임:
        /// - 설치기가 이 테이블을 고정 프리팹 없이 진행도별 몬스터 세트만 사용하는 구성으로 초기화한다.
        /// </summary>
        public void EditorConfigureProgression(
            string configuredTableName,
            int configuredSpawnCount,
            IReadOnlyList<StageMonsterSetSO> monsterSets,
            IReadOnlyList<float> weights)
        {
            tableName = string.IsNullOrWhiteSpace(configuredTableName)
                ? "Progression"
                : configuredTableName;
            spawnCount = Mathf.Max(0, configuredSpawnCount);
            entries = new List<WeightedMonsterEntry>();
            commonEntries = new List<WeightedCommonMonsterEntry>();

            int entryCount = monsterSets != null ? monsterSets.Count : 0;
            for (int i = 0; i < entryCount; i++)
            {
                StageMonsterSetSO monsterSet = monsterSets[i];
                float weight = weights != null && i < weights.Count
                    ? weights[i]
                    : 1f;
                if (monsterSet == null || weight <= 0f)
                    continue;

                commonEntries.Add(new WeightedCommonMonsterEntry
                {
                    monsterSet = monsterSet,
                    weight = weight
                });
            }
        }
#endif

        public bool HasAnyResolvableEntry(int stageIndex)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    WeightedMonsterEntry entry = entries[i];
                    if (entry.monsterPrefab != null && entry.weight > 0f)
                        return true;
                }
            }

            if (commonEntries != null)
            {
                for (int i = 0; i < commonEntries.Count; i++)
                {
                    WeightedCommonMonsterEntry commonEntry = commonEntries[i];
                    if (commonEntry.monsterSet == null || commonEntry.weight <= 0f)
                        continue;

                    if (commonEntry.monsterSet.TryResolveMonsterPrefab(stageIndex, out _))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 책임:
        /// - 스폰 수와 엔트리 비율을 기준으로 이번 방에서 사용할 몬스터 프리팹 배치를 생성한다.
        /// - weight는 개별 랜덤 확률이 아니라 전체 구성 비율로 해석한다.
        /// </summary>
        public bool TryBuildSpawnPlan(List<GameObject> results, int stageIndex)
        {
            if (results == null)
                return false;

            results.Clear();

            List<WeightedMonsterEntry> validEntries = BuildResolvedEntries(stageIndex);

            float totalWeight = 0f;
            for (int i = 0; i < validEntries.Count; i++)
            {
                WeightedMonsterEntry entry = validEntries[i];
                totalWeight += entry.weight;
            }

            if (SpawnCount <= 0 || validEntries.Count == 0 || totalWeight <= 0f)
                return false;

            int[] assignedCounts = new int[validEntries.Count];
            float[] remainders = new float[validEntries.Count];
            int assignedTotal = 0;

            for (int i = 0; i < validEntries.Count; i++)
            {
                float rawCount = (validEntries[i].weight / totalWeight) * SpawnCount;
                int baseCount = Mathf.FloorToInt(rawCount);
                assignedCounts[i] = baseCount;
                remainders[i] = rawCount - baseCount;
                assignedTotal += baseCount;
            }

            int remaining = SpawnCount - assignedTotal;
            while (remaining > 0)
            {
                int nextIndex = GetNextLargestRemainderIndex(remainders);
                if (nextIndex < 0)
                    nextIndex = UnityEngine.Random.Range(0, validEntries.Count);

                assignedCounts[nextIndex]++;
                remainders[nextIndex] = 0f;
                remaining--;
            }

            for (int i = 0; i < validEntries.Count; i++)
            {
                for (int j = 0; j < assignedCounts[i]; j++)
                    results.Add(validEntries[i].monsterPrefab);
            }

            Shuffle(results);
            return results.Count > 0;
        }

        /// <summary>
        /// 책임:
        /// - 고정 프리팹 엔트리와 stage 기반 공통 몬스터 엔트리를 실제 프리팹 엔트리 목록으로 합친다.
        /// - 이후 스폰 수 배분 로직은 기존 WeightedMonsterEntry 흐름을 그대로 재사용한다.
        /// </summary>
        private List<WeightedMonsterEntry> BuildResolvedEntries(int stageIndex)
        {
            var validEntries = new List<WeightedMonsterEntry>();

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    WeightedMonsterEntry entry = entries[i];
                    if (entry.monsterPrefab == null || entry.weight <= 0f)
                        continue;

                    validEntries.Add(entry);
                }
            }

            if (commonEntries != null)
            {
                for (int i = 0; i < commonEntries.Count; i++)
                {
                    WeightedCommonMonsterEntry commonEntry = commonEntries[i];
                    if (commonEntry.monsterSet == null || commonEntry.weight <= 0f)
                        continue;

                    if (!commonEntry.monsterSet.TryResolveMonsterPrefab(stageIndex, out GameObject resolvedPrefab))
                        continue;

                    validEntries.Add(new WeightedMonsterEntry
                    {
                        monsterPrefab = resolvedPrefab,
                        weight = commonEntry.weight
                    });
                }
            }

            return validEntries;
        }

        private static int GetNextLargestRemainderIndex(float[] remainders)
        {
            int selectedIndex = -1;
            float bestValue = float.MinValue;
            for (int i = 0; i < remainders.Length; i++)
            {
                if (remainders[i] <= bestValue)
                    continue;

                bestValue = remainders[i];
                selectedIndex = i;
            }

            return selectedIndex;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, list.Count);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    [SerializeField] private List<SpawnTable> spawnTables = new();

    public IReadOnlyList<SpawnTable> SpawnTables => spawnTables;

#if UNITY_EDITOR
    /// <summary>
    /// 책임:
    /// - 콘텐츠 설치기가 기존 고정 프리팹 테이블을 진행도별 공통 몬스터 세트 테이블로 교체한다.
    /// - 테마별 방 수치와 런 진행도별 실제 몬스터 선택을 분리한 영속 프로필을 반복 생성한다.
    /// </summary>
    public void EditorConfigureProgressionTable(
        string configuredTableName,
        int configuredSpawnCount,
        IReadOnlyList<StageMonsterSetSO> monsterSets,
        IReadOnlyList<float> weights)
    {
        var table = new SpawnTable();
        table.EditorConfigureProgression(
            configuredTableName,
            configuredSpawnCount,
            monsterSets,
            weights);

        spawnTables ??= new List<SpawnTable>();
        spawnTables.Clear();
        spawnTables.Add(table);
    }
#endif

    /// <summary>
    /// 책임:
    /// - 현재 프로파일에 등록된 스폰 테이블 중 하나를 무작위로 선택한다.
    /// - 유효한 테이블이 없으면 실패를 반환한다.
    /// </summary>
    public bool TryGetRandomSpawnTable(out SpawnTable table, int stageIndex = 0)
    {
        table = null;
        List<SpawnTable> validTables = new List<SpawnTable>();
        for (int i = 0; i < spawnTables.Count; i++)
        {
            SpawnTable candidate = spawnTables[i];
            if (candidate == null)
                continue;

            if (candidate.SpawnCount <= 0 || !candidate.HasAnyResolvableEntry(stageIndex))
                continue;

            table = candidate;
            validTables.Add(candidate);
        }

        if (validTables.Count == 0)
            return false;

        table = validTables[UnityEngine.Random.Range(0, validTables.Count)];
        return true;
    }

}

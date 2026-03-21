using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임:
/// - 게임 전역에서 몬스터 스폰을 관리한다.
/// - 현재 난이도 수정자를 보관하고 SpawnAll / SpawnOne 양쪽 경로에 일관되게 적용한다.
/// - 씬 전환 후 새로운 MonsterSpawnPoint들을 자동 재수집한다.
/// - 스폰 후 난이도 적용, 공통 View 설치를 담당한다.
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    public static MonsterSpawner Instance { get; private set; }

    [Header("Spawn Points")]
    [SerializeField] private List<MonsterSpawnPoint> spawnPoints = new();

    [Header("Difficulty")]
    [SerializeField] private DifficultyModifiers difficultyModifiers = new();

    [Header("Installers")]
    [SerializeField] private MonsterElementGaugeViewInstaller gaugeViewInstaller;

    [Header("Scene Policy")]
    [SerializeField] private bool recollectSpawnPointsOnSceneLoaded = true;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool spawnOnSceneLoaded = true;
    [SerializeField] private bool clearAliveMonstersBeforeSceneSpawn = true;

    private readonly List<GameObject> spawnedMonsters = new();

    /// <summary>
    /// 책임:
    /// - 현재 스포너가 보관 중인 난이도 수정자를 외부에 제공한다.
    /// </summary>
    public DifficultyModifiers CurrentDifficulty => difficultyModifiers;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        if (recollectSpawnPointsOnSceneLoaded || spawnPoints.Count == 0)
            CollectSpawnPointsFromActiveScene();

        if (spawnOnStart)
            SpawnAll();
    }

    /// <summary>
    /// 책임:
    /// - UI/설정 시스템이 전달한 최신 난이도 수정자로 교체한다.
    /// - 기본 정책상 이후 생성되는 몬스터부터 새 수정자를 적용한다.
    /// </summary>
    public void SetDifficultyModifiers(DifficultyModifiers modifiers)
    {
        if (modifiers == null)
            return;

        difficultyModifiers = modifiers;
    }

    /// <summary>
    /// 책임:
    /// - 현재 활성 씬의 MonsterSpawnPoint들을 다시 수집한다.
    /// - DontDestroyOnLoad 싱글톤 환경에서 씬별 스폰 위치를 갱신하기 위한 진입점이다.
    /// </summary>
    [ContextMenu("Collect Spawn Points From Active Scene")]
    public void CollectSpawnPointsFromActiveScene()
    {
        spawnPoints.Clear();

#if UNITY_2023_1_OR_NEWER
        var found = FindObjectsByType<MonsterSpawnPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        var found = FindObjectsOfType<MonsterSpawnPoint>();
#endif

        for (int i = 0; i < found.Length; i++)
        {
            var point = found[i];
            if (point == null)
                continue;

            if (point.gameObject.scene != SceneManager.GetActiveScene())
                continue;

            spawnPoints.Add(point);
        }
    }

    /// <summary>
    /// 책임:
    /// - 현재 등록된 SpawnPoint들을 기준으로 기본 몬스터 배치를 생성한다.
    /// - 난이도 수정자의 extraSpawnRatio를 반영해 추가 스폰도 수행한다.
    /// </summary>
    [ContextMenu("Spawn All")]
    public void SpawnAll()
    {
        var defaultPoints = new List<MonsterSpawnPoint>();
        var extraCandidates = new List<MonsterSpawnPoint>();

        for (int i = 0; i < spawnPoints.Count; i++)
        {
            var point = spawnPoints[i];
            if (point == null || point.MonsterPrefab == null)
                continue;

            if (point.SpawnByDefault)
                defaultPoints.Add(point);

            if (!point.SpawnByDefault && point.AllowExtraSpawn)
                extraCandidates.Add(point);
        }

        for (int i = 0; i < defaultPoints.Count; i++)
        {
            SpawnOne(defaultPoints[i].CreateRequest());
        }

        int extraCount = CalculateExtraSpawnCount(defaultPoints.Count, difficultyModifiers.extraSpawnRatio);
        Shuffle(extraCandidates);

        for (int i = 0; i < extraCount && i < extraCandidates.Count; i++)
        {
            SpawnOne(extraCandidates[i].CreateRequest());
        }
    }

    /// <summary>
    /// 책임:
    /// - 외부 시스템이 요청한 단일 스폰 요청을 실행한다.
    /// - 난이도 적용 및 공통 View 설치를 일관되게 수행한다.
    /// </summary>
    public GameObject SpawnOne(MonsterSpawnRequest request)
    {
        if (!request.IsValid)
            return null;

        var monster = Instantiate(
            request.MonsterPrefab,
            request.Position,
            request.Rotation);

        ApplyDifficulty(monster, difficultyModifiers);
        InstallViews(monster);

        spawnedMonsters.Add(monster);
        return monster;
    }

    /// <summary>
    /// 책임:
    /// - 현재 스포너가 생성한 몬스터들을 정리한다.
    /// - null 참조도 함께 제거해 리스트를 정리한다.
    /// </summary>
    [ContextMenu("Clear Spawned")]
    public void ClearSpawnedMonsters()
    {
        for (int i = spawnedMonsters.Count - 1; i >= 0; i--)
        {
            var monster = spawnedMonsters[i];
            if (monster != null)
                Destroy(monster);
        }

        spawnedMonsters.Clear();
    }

    /// <summary>
    /// 책임:
    /// - 현재 살아 있는 스폰 몬스터들에게 최신 난이도 수정자를 다시 적용한다.
    /// - 전투 중 즉시 재적용은 기획적으로 위험할 수 있으므로 선택 기능으로 둔다.
    /// </summary>
    [ContextMenu("Reapply Difficulty To Alive Monsters")]
    public void ReapplyDifficultyToAliveMonsters()
    {
        CompactSpawnedMonsterList();

        for (int i = 0; i < spawnedMonsters.Count; i++)
        {
            var monster = spawnedMonsters[i];
            if (monster == null)
                continue;

            ApplyDifficulty(monster, difficultyModifiers);
        }
    }

    /// <summary>
    /// 책임:
    /// - 외부에서 특정 SpawnPoint를 등록할 수 있게 한다.
    /// - 동적 맵 생성 등 Find 기반 수집 외 상황에 대응하기 위한 보조 API다.
    /// </summary>
    public void RegisterSpawnPoint(MonsterSpawnPoint point)
    {
        if (point == null)
            return;

        if (!spawnPoints.Contains(point))
            spawnPoints.Add(point);
    }

    /// <summary>
    /// 책임:
    /// - 외부에서 특정 SpawnPoint를 해제할 수 있게 한다.
    /// </summary>
    public void UnregisterSpawnPoint(MonsterSpawnPoint point)
    {
        if (point == null)
            return;

        spawnPoints.Remove(point);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (recollectSpawnPointsOnSceneLoaded)
            CollectSpawnPointsFromActiveScene();

        CompactSpawnedMonsterList();

        if (clearAliveMonstersBeforeSceneSpawn)
            ClearSpawnedMonsters();

        if (spawnOnSceneLoaded)
            SpawnAll();
    }

    private void ApplyDifficulty(GameObject monster, DifficultyModifiers modifiers)
    {
        if (monster == null || modifiers == null)
            return;

        var receivers = monster.GetComponentsInChildren<IMonsterDifficultyReceiver>(true);
        for (int i = 0; i < receivers.Length; i++)
        {
            receivers[i].ApplyDifficulty(modifiers);
        }
    }

    private void InstallViews(GameObject monster)
    {
        if (gaugeViewInstaller != null)
            gaugeViewInstaller.Install(monster);
    }

    private int CalculateExtraSpawnCount(int baseCount, float extraRatio)
    {
        if (baseCount <= 0 || extraRatio <= 0f)
            return 0;

        float raw = baseCount * extraRatio;
        int count = Mathf.FloorToInt(raw);

        float fractional = raw - count;
        if (Random.value < fractional)
            count += 1;

        return count;
    }

    private void CompactSpawnedMonsterList()
    {
        for (int i = spawnedMonsters.Count - 1; i >= 0; i--)
        {
            if (spawnedMonsters[i] == null)
                spawnedMonsters.RemoveAt(i);
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int j = Random.Range(i, list.Count);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        spawnPoints.RemoveAll(p => p == null);
    }
#endif
}
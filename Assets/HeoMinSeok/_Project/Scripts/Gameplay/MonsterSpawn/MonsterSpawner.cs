using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임:
/// - 게임 전역에서 하나의 MonsterSpawner singleton을 유지한다.
/// - 현재 난이도 수정자를 보관하고 scene-local spawn director에 전달한다.
/// - 기존 public API와 serialized 설정을 유지해 씬/프리팹 추가 설정 없이 동작하게 한다.
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    public static MonsterSpawner Instance { get; private set; }

    [Header("Spawn Points")]
    [SerializeField] private List<MonsterSpawnContainer> spawnPoints = new();
    [SerializeField] private List<MonsterSpawnRoomGroup> spawnRooms = new();

    [Header("Difficulty")]
    [SerializeField] private DifficultyModifiers difficultyModifiers = new();

    [Header("Installers")]
    [SerializeField] private MonsterElementGaugeViewInstaller gaugeViewInstaller;

    [Header("Navigation")]
    [SerializeField] private TilemapPathfinder2D pathfinder;

    [Header("Scene Policy")]
    [SerializeField] private bool recollectSpawnPointsOnSceneLoaded = true;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private bool spawnOnSceneLoaded = true;
    [SerializeField] private bool clearAliveMonstersBeforeSceneSpawn = true;

    private SceneMonsterSpawnDirector sceneDirector;

    /// <summary>
    /// 책임:
    /// - 현재 스포너가 보관 중인 난이도 수정자를 외부에 제공한다.
    /// </summary>
    public DifficultyModifiers CurrentDifficulty => difficultyModifiers;

    private SceneMonsterSpawnDirector SceneDirector
    {
        get
        {
            sceneDirector ??= new SceneMonsterSpawnDirector(
                spawnPoints,
                spawnRooms,
                gaugeViewInstaller,
                pathfinder);

            return sceneDirector;
        }
    }

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
        SceneDirector.ResolveSceneServices();
        SyncSceneServiceReferences();

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
    /// - DontDestroyOnLoad singleton 환경에서 씬별 스폰 위치를 갱신하기 위한 진입점이다.
    /// </summary>
    [ContextMenu("Collect Spawn Points From Active Scene")]
    public void CollectSpawnPointsFromActiveScene()
    {
        SceneDirector.CollectSpawnPointsFromActiveScene();
        SyncSceneServiceReferences();
    }

    /// <summary>
    /// 책임:
    /// - 현재 등록된 SpawnPoint들을 기준으로 기본 몬스터 배치를 생성한다.
    /// - 난이도 수정자의 extraSpawnRatio를 반영해 추가 스폰도 수행한다.
    /// </summary>
    [ContextMenu("Spawn All")]
    public void SpawnAll()
    {
        SceneDirector.SpawnAll(difficultyModifiers);
        SyncSceneServiceReferences();
    }

    /// <summary>
    /// 책임:
    /// - 외부 시스템이 요청한 단일 스폰 요청을 실행한다.
    /// - 난이도 적용 및 공통 View 설치를 일관되게 수행한다.
    /// </summary>
    public GameObject SpawnOne(MonsterSpawnRequest request)
    {
        GameObject monster = SceneDirector.SpawnOne(request, difficultyModifiers);
        SyncSceneServiceReferences();
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
        SceneDirector.ClearSpawnedMonsters();
    }

    /// <summary>
    /// 책임:
    /// - 현재 살아 있는 스폰 몬스터들에게 최신 난이도 수정자를 다시 적용한다.
    /// - 전투 중 즉시 재적용은 기획적으로 위험할 수 있으므로 선택 기능으로 둔다.
    /// </summary>
    [ContextMenu("Reapply Difficulty To Alive Monsters")]
    public void ReapplyDifficultyToAliveMonsters()
    {
        SceneDirector.ReapplyDifficultyToAliveMonsters(difficultyModifiers);
    }

    /// <summary>
    /// 책임:
    /// - 외부에서 특정 SpawnPoint를 등록할 수 있게 한다.
    /// - 동적 맵 생성 등 Find 기반 수집 외 상황에 대응하기 위한 보조 API다.
    /// </summary>
    public void RegisterSpawnPoint(MonsterSpawnContainer point)
    {
        SceneDirector.RegisterSpawnPoint(point);
    }

    /// <summary>
    /// 책임:
    /// - 외부에서 특정 SpawnPoint를 해제할 수 있게 한다.
    /// </summary>
    public void UnregisterSpawnPoint(MonsterSpawnContainer point)
    {
        SceneDirector.UnregisterSpawnPoint(point);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneDirector.HandleSceneLoaded(
            new SceneMonsterSpawnPolicy(
                recollectSpawnPointsOnSceneLoaded,
                spawnOnSceneLoaded,
                clearAliveMonstersBeforeSceneSpawn),
            difficultyModifiers);

        SyncSceneServiceReferences();
    }

    private void SyncSceneServiceReferences()
    {
        gaugeViewInstaller = SceneDirector.GaugeViewInstaller;
        pathfinder = SceneDirector.Pathfinder;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        spawnPoints.RemoveAll(point => point == null);
        spawnRooms.RemoveAll(room => room == null);
        sceneDirector?.RemoveNullAuthoringEntries();
    }
#endif
}

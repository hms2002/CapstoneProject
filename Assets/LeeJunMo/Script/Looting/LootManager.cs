using System.Collections.Generic;
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LootManager : MonoBehaviour
{
    private const string DefaultFieldItemPrefabResourcePath = "PF_FieldHealPickup2D";

    public static LootManager Instance { get; private set; }

    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool verboseLogging;

    [Header("Settings")]
    [SerializeField] private GameObject worldItemPrefab;
    [SerializeField] private GameObject fieldItemPrefab;

    [Header("References")]
    [SerializeField] private List<StageLootTable> stageTables = new List<StageLootTable>();

    [Header("Grave References")]
    [SerializeField] private GraveLootTable graveLootTable;

    [Header("Fallback State")]
    [SerializeField, Min(0)] private int currentStageIndex = 0;

    private LootTableResolver tableResolver;
    private LootPoolService poolService;
    private LootRollService rollService;
    private LootSpawnService spawnService;
    private ChestLootGenerationService chestLootGenerationService;
    private MonsterLootDropService monsterLootDropService;
    private GraveLootDropService graveLootDropService;

    // 외부 시스템(Portal, RunModifier)과의 결합을 끊기 위한 데이터 제공자(Provider) 델리게이트
    // 외부(예: StageManager 등)에서 이 Func를 할당해주어 LootManager가 싱글톤에 의존하지 않게 합니다.
    public Func<int> StageIndexProvider { get; set; }
    public Func<ChestRunModifierDelta> ChestModifierProvider { get; set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        RefreshServices();
    }

    private void OnEnable()
    {
        MonsterDrop.OnAnyMonsterDropRequested += SpawnMonsterLoot;
    }

    private void OnDisable()
    {
        MonsterDrop.OnAnyMonsterDropRequested -= SpawnMonsterLoot;
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // Avoid asset loading during import/build-time validation. Unity invokes OnValidate
        // while opening scenes for player builds, and Resources.Load from there trips
        // editor-only SendMessage restrictions.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
#endif

        RefreshServices(editorSafe: true);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int CurrentStageIndex
    {
        get
        {
            if (StageIndexProvider != null)
                return StageIndexProvider.Invoke();

            return currentStageIndex;
        }
    }

    public void SetFallbackStageIndex(int stageIndex)
    {
        currentStageIndex = Mathf.Max(0, stageIndex);
    }

    public WeaponDefinition GetRandomWeapon(HashSet<string> exclusionList)
    {
        EnsureServices();
        return poolService.GetRandomWeapon(exclusionList);
    }

    public RelicDefinition GetRandomRelic()
    {
        StageLootTable table = GetCurrentTable();
        if (table == null)
            return null;

        EnsureServices();
        ItemRarity rarity = rollService.RollStageRelicRarity(table);
        return GetRandomRelicByRarity(rarity);
    }

    public List<ScriptableObject> GenerateChestLoot()
    {
        return GenerateChestLoot(default);
    }

    public List<ScriptableObject> GenerateChestLoot(ChestRunModifierDelta extraModifiers)
    {
        return GenerateChestLootResult(new ChestLootRequest(extraModifiers)).ToList();
    }

    public ChestLootResult GenerateChestLootResult(ChestLootRequest request)
    {
        StageLootTable table = GetCurrentTable();
        if (table == null)
            return ChestLootResult.Empty;

        EnsureServices();

        // 싱글톤 직접 참조 대신 외부에서 주입된 Provider를 통해 업그레이드 보너스 수치를 받아옵니다.
        ChestRunModifierDelta chestModifiers = ResolveChestModifiers(request.ExtraModifiers);

        return chestLootGenerationService.Generate(table, request, chestModifiers);
    }

    public List<ScriptableObject> GenerateBossChestLoot()
    {
        return GenerateBossChestLoot(default);
    }

    public List<ScriptableObject> GenerateBossChestLoot(ChestRunModifierDelta extraModifiers)
    {
        return GenerateBossChestLootResult(new ChestLootRequest(extraModifiers)).ToList();
    }

    public ChestLootResult GenerateBossChestLootResult(ChestLootRequest request)
    {
        StageLootTable table = GetCurrentTable();
        if (table == null)
            return ChestLootResult.Empty;

        EnsureServices();
        return chestLootGenerationService.GenerateBoss(table, request, request.ExtraModifiers);
    }

    private ChestRunModifierDelta ResolveChestModifiers(ChestRunModifierDelta extraModifiers)
    {
        ChestRunModifierDelta modifiers = ChestModifierProvider != null
            ? ChestModifierProvider.Invoke()
            : RunModifierService.CurrentRewardSnapshot.ChestModifiers;

        modifiers.Add(extraModifiers);
        return modifiers;
    }

    public void SpawnMonsterLoot(Vector3 position)
    {
        StageLootTable table = GetCurrentTable();
        if (table == null)
            return;

        EnsureServices();
        monsterLootDropService.Spawn(new MonsterLootDropRequest(position, table));
    }

    public void SpawnLootObject(Vector3 position, ScriptableObject itemData)
    {
        EnsureServices();
        spawnService.SpawnLootObject(position, itemData);
    }

    public void SpawnFieldHealPickup(Vector3 position)
    {
        EnsureServices();
        spawnService.SpawnFieldHealPickup(position);
    }

    public int GetBossMagicStoneCount()
    {
        StageLootTable table = GetCurrentTable();
        return table != null ? table.bossStoneCount : 0;
    }

    public int GetBossFieldHealBaseCount()
    {
        StageLootTable table = GetCurrentTable();
        return table != null ? Mathf.Max(0, table.BossFieldHealBaseCount) : 0;
    }

    public void SpawnGraveLoot(Vector3 position, GraveType type, int bonusMinCount = 0, int bonusMaxCount = 0, float bonusRareChance = 0f, float bonusEpicChance = 0f)
    {
        EnsureServices();

        GraveLootTable currentGraveTable = tableResolver.GetGraveLootTable();
        
        // ItemManager 체크는 이미 poolService 내부에서 안전하게 처리하고 있으므로 여기서 강하게 결합할 필요가 없습니다.
        if (currentGraveTable == null)
            return;

        graveLootDropService.Spawn(new GraveLootDropRequest(
            position,
            type,
            currentGraveTable,
            bonusMinCount,
            bonusMaxCount,
            bonusRareChance,
            bonusEpicChance));
    }

    private void EnsureServices()
    {
        if (tableResolver == null ||
            poolService == null ||
            rollService == null ||
            spawnService == null ||
            chestLootGenerationService == null ||
            monsterLootDropService == null ||
            graveLootDropService == null)
        {
            RefreshServices();
        }
    }

    private void RefreshServices(bool editorSafe = false)
    {
        tableResolver = new LootTableResolver(stageTables, graveLootTable);
        poolService = new LootPoolService();
        rollService = new LootRollService();
        spawnService = new LootSpawnService(worldItemPrefab, ResolveFieldItemPrefab(editorSafe));
        chestLootGenerationService = new ChestLootGenerationService(poolService, rollService, GetRandomRelic, GetRandomRelicByRarity);
        monsterLootDropService = new MonsterLootDropService(poolService, rollService, spawnService, GetRandomRelic);
        graveLootDropService = new GraveLootDropService(poolService, rollService, spawnService, GetRandomRelicByRarity);
    }

    private GameObject ResolveFieldItemPrefab(bool editorSafe = false)
    {
        if (fieldItemPrefab != null)
            return fieldItemPrefab;

#if UNITY_EDITOR
        if (editorSafe && !Application.isPlaying)
            return null;
#endif

        return Resources.Load<GameObject>(DefaultFieldItemPrefabResourcePath);
    }

    private StageLootTable GetCurrentTable()
    {
        EnsureServices();
        return tableResolver.GetCurrentTable(CurrentStageIndex);
    }

    private RelicDefinition GetRandomRelicByRarity(ItemRarity targetRarity)
    {
        EnsureServices();
        return poolService.GetRandomRelicByRarity(targetRarity);
    }

}

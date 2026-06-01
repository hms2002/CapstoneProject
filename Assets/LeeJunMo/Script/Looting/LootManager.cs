using System.Collections.Generic;
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LootManager : MonoBehaviour
{
    private const string DefaultFieldItemPrefabResourcePath = "PF_FieldHealPickup2D";
    private const string DefaultMagicStonePrefabResourcePath = "MagicStonePrefab";

    public static LootManager Instance { get; private set; }

    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool verboseLogging;

    [Header("Settings")]
    [SerializeField] private GameObject worldItemPrefab;
    [SerializeField] private GameObject fieldItemPrefab;
    [SerializeField] private GameObject magicStonePrefab;

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

#if UNITY_EDITOR
        TryAssignEditorDefaultPrefabs();
#endif

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

        TryAssignEditorDefaultPrefabs();
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
        SpawnMonsterLoot(position, null);
    }

    public void SpawnMonsterLoot(Vector3 position, GameObject source)
    {
        StageLootTable table = GetCurrentTable();
        if (table == null)
            return;

        EnsureServices();
        monsterLootDropService.Spawn(new MonsterLootDropRequest(position, table, source));
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

    public void SpawnMagicStonePickup(Vector3 position, int amount = 1)
    {
        EnsureServices();
        spawnService.SpawnMagicStonePickup(position, amount);
    }

    public int GetBossMagicStoneCount()
    {
        StageLootTable table = GetCurrentTable();
        return table != null ? Mathf.Max(0, table.bossStoneCount) : 0;
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
        spawnService = new LootSpawnService(
            worldItemPrefab,
            ResolveFieldItemPrefab(editorSafe),
            ResolveMagicStonePrefab(editorSafe));
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

    private GameObject ResolveMagicStonePrefab(bool editorSafe = false)
    {
        if (magicStonePrefab != null)
            return magicStonePrefab;

#if UNITY_EDITOR
        if (editorSafe && !Application.isPlaying)
            return null;
#endif

        return Resources.Load<GameObject>(DefaultMagicStonePrefabResourcePath);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 책임:
    /// - 에디터에서 LootManager authoring 누락을 줄이기 위해 기본 loot prefab 참조를 프로젝트 에셋에서 자동 보완한다.
    /// - 런타임/빌드 중 Resources.Load를 호출하지 않도록 OnValidate의 안전한 에디터 구간에서만 실행된다.
    /// </summary>
    private void TryAssignEditorDefaultPrefabs()
    {
        bool changed = false;

        if (magicStonePrefab == null &&
            TryFindPrefabAsset(DefaultMagicStonePrefabResourcePath, out GameObject resolvedMagicStonePrefab))
        {
            magicStonePrefab = resolvedMagicStonePrefab;
            changed = true;
        }

        if (changed)
            EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// 책임:
    /// - Resources 폴더에 있지 않은 프로젝트 prefab도 이름 기준으로 찾아 LootManager 자동 참조 보정에 제공한다.
    /// - 같은 이름 후보가 여러 개면 Resources 경로 후보를 우선하고, 없으면 첫 번째 prefab을 사용한다.
    /// </summary>
    private static bool TryFindPrefabAsset(string prefabNameWithoutExtension, out GameObject prefab)
    {
        prefab = null;
        if (string.IsNullOrWhiteSpace(prefabNameWithoutExtension))
            return false;

        string[] guids = AssetDatabase.FindAssets($"{prefabNameWithoutExtension} t:Prefab");
        if (guids == null || guids.Length == 0)
            return false;

        string fallbackPath = null;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!string.Equals(fileName, prefabNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                continue;

            if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                return prefab != null;
            }

            fallbackPath ??= path;
        }

        if (string.IsNullOrWhiteSpace(fallbackPath))
            return false;

        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fallbackPath);
        return prefab != null;
    }
#endif

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

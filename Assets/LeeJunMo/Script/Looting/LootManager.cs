using System.Collections.Generic;
using UnityEngine;

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

    private void OnValidate()
    {
        RefreshServices();
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
            if (PortalRouteManager.Instance != null && PortalRouteManager.Instance.HasActivePlan)
                return PortalRouteManager.Instance.CurrentStageIndex;

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
        var drops = new List<ScriptableObject>();
        StageLootTable table = GetCurrentTable();
        if (table == null)
            return drops;

        EnsureServices();

        HashSet<string> banList = poolService.BuildPlayerWeaponExclusionSet();
        ChestRunModifierDelta chestModifiers = RunModifierService.Instance != null
            ? RunModifierService.Instance.ChestModifiers
            : default;

        int weaponCount = rollService.PickCountInProfile(
            table.ChestWeaponCountProfile,
            chestModifiers.chestWeaponMinBonus,
            chestModifiers.chestWeaponMaxBonus);
        for (int i = 0; i < weaponCount; i++)
        {
            WeaponDefinition weapon = poolService.GetRandomWeapon(banList);
            if (weapon == null)
                continue;

            drops.Add(weapon);
            banList.Add(weapon.weaponId);
        }

        int relicCount = rollService.PickCountInProfile(
            table.ChestRelicCountProfile,
            chestModifiers.chestRelicMinBonus,
            chestModifiers.chestRelicMaxBonus);
        for (int i = 0; i < relicCount; i++)
        {
            RelicDefinition relic = GetRandomRelic();
            if (relic != null)
                drops.Add(relic);
        }

        int consumableCount = rollService.PickCountInProfile(table.ChestConsumableCountProfile);
        for (int i = 0; i < consumableCount; i++)
        {
            ConsumableDefinition consumable = poolService.GetRandomConsumable();
            if (consumable != null)
                drops.Add(consumable);
        }

        return drops;
    }

    public void SpawnMonsterLoot(Vector3 position)
    {
        StageLootTable table = GetCurrentTable();
        if (table == null)
            return;

        EnsureServices();

        MonsterLootType lootType = rollService.RollMonsterLootType(table);
        switch (lootType)
        {
            case MonsterLootType.None:
                return;

            case MonsterLootType.Weapon:
            {
                HashSet<string> banList = poolService.BuildPlayerWeaponExclusionSet();
                WeaponDefinition weapon = poolService.GetRandomWeapon(banList);
                if (weapon != null)
                    SpawnLootObject(position, weapon);
                return;
            }

            case MonsterLootType.Relic:
            {
                RelicDefinition relic = GetRandomRelic();
                if (relic != null)
                    SpawnLootObject(position, relic);
                return;
            }

            case MonsterLootType.Consumable:
                return;

            case MonsterLootType.FieldItem:
                spawnService.SpawnFieldHealPickup(position);
                return;

            default:
                return;
        }
    }

    public void SpawnLootObject(Vector3 position, ScriptableObject itemData)
    {
        EnsureServices();
        spawnService.SpawnLootObject(position, itemData);
    }

    public int GetBossMagicStoneCount()
    {
        StageLootTable table = GetCurrentTable();
        return table != null ? table.bossStoneCount : 0;
    }

    public void SpawnGraveLoot(Vector3 position, GraveType type, int bonusMinCount = 0, int bonusMaxCount = 0, float bonusRareChance = 0f, float bonusEpicChance = 0f)
    {
        EnsureServices();

        GraveLootTable currentGraveTable = tableResolver.GetGraveLootTable();
        if (currentGraveTable == null || ItemManager.Instance == null)
            return;

        switch (type)
        {
            case GraveType.Weapon:
                SpawnWeaponGraveLoot(position, currentGraveTable, bonusMinCount, bonusMaxCount);
                break;

            case GraveType.Relic:
                SpawnRelicGraveLoot(position, currentGraveTable, bonusMinCount, bonusMaxCount, bonusRareChance, bonusEpicChance);
                break;
        }
    }

    private void SpawnWeaponGraveLoot(Vector3 position, GraveLootTable currentGraveTable, int bonusMinCount, int bonusMaxCount)
    {
        int totalCount = rollService.PickCountInProfile(
            currentGraveTable.WeaponDropCountProfile,
            bonusMinCount,
            bonusMaxCount);

        for (int i = 0; i < totalCount; i++)
        {
            WeaponDefinition weapon = poolService.GetRandomWeapon(new HashSet<string>());
            if (weapon != null)
                SpawnLootObject(position + spawnService.GetRandomScatterOffset(), weapon);
        }
    }

    private void SpawnRelicGraveLoot(Vector3 position, GraveLootTable currentGraveTable, int bonusMinCount, int bonusMaxCount, float bonusRareChance, float bonusEpicChance)
    {
        int totalCount = rollService.PickCountInProfile(
            currentGraveTable.RelicDropCountProfile,
            bonusMinCount,
            bonusMaxCount);

        for (int i = 0; i < totalCount; i++)
        {
            ItemRarity rarity = rollService.RollGraveRelicRarity(currentGraveTable, bonusRareChance, bonusEpicChance);
            RelicDefinition relic = GetRandomRelicByRarity(rarity);
            if (relic != null)
                SpawnLootObject(position + spawnService.GetRandomScatterOffset(), relic);
        }
    }

    private void EnsureServices()
    {
        if (tableResolver == null || poolService == null || rollService == null || spawnService == null)
            RefreshServices();
    }

    private void RefreshServices()
    {
        tableResolver = new LootTableResolver(stageTables, graveLootTable);
        poolService = new LootPoolService();
        rollService = new LootRollService();
        spawnService = new LootSpawnService(worldItemPrefab, ResolveFieldItemPrefab());
    }

    private GameObject ResolveFieldItemPrefab()
    {
        if (fieldItemPrefab != null)
            return fieldItemPrefab;

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

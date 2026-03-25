using System.Collections.Generic;
using UnityEngine;

public class LootManager : MonoBehaviour
{
    public static LootManager Instance { get; private set; }

    [Header("Settings")]
    public GameObject worldItemPrefab;

    [Header("References")]
    public List<StageLootTable> stageTables;

    [Header("Grave References")]
    public GraveLootTable graveLootTable;

    [Header("State")]
    public int currentStageIndex = 0;

    private LootTableResolver tableResolver;
    private LootPoolService poolService;
    private LootRollService rollService;
    private LootSpawnService spawnService;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            RefreshServices();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnValidate()
    {
        RefreshServices();
    }

    private void RefreshServices()
    {
        tableResolver = new LootTableResolver(stageTables, graveLootTable);
        poolService = new LootPoolService();
        rollService = new LootRollService();
        spawnService = new LootSpawnService(worldItemPrefab);
    }

    private StageLootTable GetCurrentTable()
    {
        if (tableResolver == null)
            RefreshServices();

        return tableResolver.GetCurrentTable(currentStageIndex);
    }

    public WeaponDefinition GetRandomWeapon(HashSet<string> exclusionList)
    {
        if (poolService == null)
            RefreshServices();

        return poolService.GetRandomWeapon(exclusionList);
    }

    public RelicDefinition GetRandomRelic()
    {
        StageLootTable table = GetCurrentTable();
        if (table == null)
            return null;

        if (rollService == null || poolService == null)
            RefreshServices();

        ItemRarity rarity = rollService.RollStageRelicRarity(table);
        return GetRandomRelicByRarity(rarity);
    }

    public List<ScriptableObject> GenerateChestLoot()
    {
        List<ScriptableObject> drops = new List<ScriptableObject>();
        StageLootTable table = GetCurrentTable();
        if (table == null)
            return drops;

        if (rollService == null || poolService == null)
            RefreshServices();

        HashSet<string> banList = poolService.BuildPlayerWeaponExclusionSet();

        int weaponCount = rollService.PickCount(table.chestWeaponCounts);
        for (int i = 0; i < weaponCount; i++)
        {
            WeaponDefinition weapon = poolService.GetRandomWeapon(banList);
            if (weapon == null)
                continue;

            drops.Add(weapon);
            banList.Add(weapon.weaponId);
        }

        int relicCount = rollService.PickCount(table.chestRelicCounts);
        for (int i = 0; i < relicCount; i++)
        {
            RelicDefinition relic = GetRandomRelic();
            if (relic != null)
                drops.Add(relic);
        }

        return drops;
    }

    public void SpawnMonsterLoot(Vector3 position)
    {
        StageLootTable table = GetCurrentTable();
        if (table == null)
            return;

        if (rollService == null || poolService == null)
            RefreshServices();

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
            case MonsterLootType.FieldItem:
            default:
                return;
        }
    }

    public void SpawnLootObject(Vector3 position, ScriptableObject itemData)
    {
        if (spawnService == null)
            RefreshServices();

        spawnService.SpawnLootObject(position, itemData);
    }

    public int GetBossMagicStoneCount()
    {
        StageLootTable table = GetCurrentTable();
        return table != null ? table.bossStoneCount : 0;
    }

    public void SpawnGraveLoot(Vector3 position, GraveType type, int bonusCount = 0, float bonusRareChance = 0f, float bonusEpicChance = 0f)
    {
        if (tableResolver == null || rollService == null || poolService == null || spawnService == null)
            RefreshServices();

        GraveLootTable currentGraveTable = tableResolver.GetGraveLootTable();
        if (currentGraveTable == null || ItemManager.Instance == null)
            return;

        switch (type)
        {
            case GraveType.Weapon:
                SpawnWeaponGraveLoot(position, currentGraveTable, bonusCount);
                break;
            case GraveType.Relic:
                SpawnRelicGraveLoot(position, currentGraveTable, bonusCount, bonusRareChance, bonusEpicChance);
                break;
        }
    }

    private void SpawnWeaponGraveLoot(Vector3 position, GraveLootTable currentGraveTable, int bonusCount)
    {
        int baseCount = rollService.PickCount(currentGraveTable.weaponDropCounts);
        int totalCount = baseCount + bonusCount;

        for (int i = 0; i < totalCount; i++)
        {
            WeaponDefinition weapon = poolService.GetRandomWeapon(new HashSet<string>());
            if (weapon != null)
                SpawnLootObject(position + spawnService.GetRandomScatterOffset(), weapon);
        }
    }

    private void SpawnRelicGraveLoot(Vector3 position, GraveLootTable currentGraveTable, int bonusCount, float bonusRareChance, float bonusEpicChance)
    {
        int baseCount = rollService.PickCount(currentGraveTable.relicDropCounts);
        int totalCount = baseCount + bonusCount;

        for (int i = 0; i < totalCount; i++)
        {
            ItemRarity rarity = rollService.RollGraveRelicRarity(currentGraveTable, bonusRareChance, bonusEpicChance);
            RelicDefinition relic = GetRandomRelicByRarity(rarity);
            if (relic != null)
                SpawnLootObject(position + spawnService.GetRandomScatterOffset(), relic);
        }
    }

    private RelicDefinition GetRandomRelicByRarity(ItemRarity targetRarity)
    {
        if (poolService == null)
            RefreshServices();

        return poolService.GetRandomRelicByRarity(targetRarity);
    }
}

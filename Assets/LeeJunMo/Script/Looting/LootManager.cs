using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LootManager : MonoBehaviour
{
    public static LootManager Instance { get; private set; }

    [Header("Settings")]
    public GameObject worldItemPrefab; // 바닥에 떨어질 아이템 프리팹

    [Header("References")]
    public List<StageLootTable> stageTables;

    [Header("시작 방(유해) References")]
    public GraveLootTable graveLootTable; // 인스펙터 연결 필수!

    [Header("State")]
    public int currentStageIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private StageLootTable GetCurrentTable()
    {
        int idx = Mathf.Clamp(currentStageIndex, 0, stageTables.Count - 1);
        return stageTables[idx];
    }

    // =========================================================
    // 1. 유틸리티 (확률 계산)
    // =========================================================
    private int PickCount(List<DropCountOption> options)
    {
        if (options == null || options.Count == 0) return 0;
        int total = options.Sum(o => o.weight);
        int rand = Random.Range(0, total);
        int sum = 0;
        foreach (var opt in options) { sum += opt.weight; if (rand < sum) return opt.count; }
        return options.Last().count;
    }

    private ItemRarity RollRelicRarity(StageLootTable table)
    {
        int total = table.commonWeight + table.rareWeight + table.epicWeight;
        int rand = Random.Range(0, total);
        int sum = 0;

        sum += table.commonWeight; if (rand < sum) return ItemRarity.Common;
        sum += table.rareWeight; if (rand < sum) return ItemRarity.Rare;

        return ItemRarity.Epic;
    }

    // =========================================================
    // 2. 단일 아이템 데이터 뽑기
    // =========================================================
    public WeaponDefinition GetRandomWeapon(HashSet<string> exclusionList)
    {
        if (ItemManager.Instance == null) return null;

        var pool = ItemManager.Instance.GetUnlockedWeaponIDs();
        var valid = pool.Where(w => !exclusionList.Contains(w)).ToList();

        if (valid.Count == 0) return null;

        string pickedID = valid[Random.Range(0, valid.Count)];
        return ItemManager.Instance.GetWeaponData(pickedID);
    }

    public RelicDefinition GetRandomRelic()
    {
        if (ItemManager.Instance == null) return null;

        var pool = ItemManager.Instance.GetUnlockedRelicIDs();
        if (pool.Count == 0) return null;

        StageLootTable table = GetCurrentTable();
        ItemRarity rarity = RollRelicRarity(table);

        return GetRandomRelicByRarity(rarity);
    }

    // =========================================================
    // 3. [상자용] 드롭 리스트 생성
    // =========================================================
    public List<ScriptableObject> GenerateChestLoot()
    {
        List<ScriptableObject> drops = new List<ScriptableObject>();
        StageLootTable table = GetCurrentTable();

        HashSet<string> banList = new HashSet<string>();
        if (SampleTopDownPlayer.Instance != null)
        {
            WeaponInventory2D weaponInventory = SampleTopDownPlayer.Instance.GetComponent<WeaponInventory2D>();
            if (weaponInventory != null)
                banList.UnionWith(weaponInventory.GetAllWeaponIDs());
        }

        int wCount = PickCount(table.chestWeaponCounts);
        for (int i = 0; i < wCount; i++)
        {
            var weapon = GetRandomWeapon(banList);
            if (weapon != null)
            {
                drops.Add(weapon);
                banList.Add(weapon.weaponId);
            }
        }

        int rCount = PickCount(table.chestRelicCounts);
        for (int i = 0; i < rCount; i++)
        {
            var relic = GetRandomRelic();
            if (relic != null) drops.Add(relic);
        }

        return drops;
    }

    // =========================================================
    // 4. [일반 몬스터용] 확률 드롭 및 스폰
    // =========================================================
    public void SpawnMonsterLoot(Vector3 position)
    {
        StageLootTable table = GetCurrentTable();

        int totalWeight = table.mobNothingWeight + table.mobWeaponWeight + table.mobRelicWeight
                          + table.mobConsumableWeight + table.mobFieldItemWeight;

        int rand = Random.Range(0, totalWeight);
        int sum = 0;

        sum += table.mobNothingWeight;
        if (rand < sum) return;

        sum += table.mobWeaponWeight;
        if (rand < sum)
        {
            HashSet<string> banList = new HashSet<string>();
            if (SampleTopDownPlayer.Instance != null)
            {
                WeaponInventory2D weaponInventory = SampleTopDownPlayer.Instance.GetComponent<WeaponInventory2D>();
                banList.UnionWith(weaponInventory.GetAllWeaponIDs());
            }

            var weapon = GetRandomWeapon(banList);
            if (weapon != null) SpawnLootObject(position, weapon);
            return;
        }

        sum += table.mobRelicWeight;
        if (rand < sum)
        {
            var relic = GetRandomRelic();
            if (relic != null) SpawnLootObject(position, relic);
            return;
        }

        sum += table.mobConsumableWeight;
        if (rand < sum) return;

        sum += table.mobFieldItemWeight;
        if (rand < sum) return;
    }

    public void SpawnLootObject(Vector3 position, ScriptableObject itemData)
    {
        if (worldItemPrefab == null)
        {
            Debug.LogError("LootManager: WorldItemPrefab이 연결되지 않았습니다.");
            return;
        }

        GameObject go = Instantiate(worldItemPrefab, position, Quaternion.identity);
        var pickup = go.GetComponent<WorldItemPickup2D>();
        if (pickup != null)
        {
            pickup.SetItem(itemData);
        }
    }

    public int GetBossMagicStoneCount()
    {
        StageLootTable table = GetCurrentTable();
        if (table == null) return 0;
        return table.bossStoneCount;
    }

    // =========================================================
    // 🌟 5. 유해(Grave) 전용 드롭 시스템
    // =========================================================
    public void SpawnGraveLoot(Vector3 position, GraveType type, int bonusCount = 0, float bonusRareChance = 0f, float bonusEpicChance = 0f)
    {
        if (graveLootTable == null || ItemManager.Instance == null) return;

        if (type == GraveType.Weapon)
        {
            // [수정] 무기 유해도 테이블 확률에 따라 기본 개수를 뽑습니다!
            int baseCount = PickCount(graveLootTable.weaponDropCounts);
            int totalCount = baseCount + bonusCount;

            for (int i = 0; i < totalCount; i++)
            {
                var weapon = GetRandomWeapon(new HashSet<string>()); // 중복 드롭 허용
                if (weapon != null) SpawnLootObject(position + GetRandomOffset(), weapon);
            }
        }
        else if (type == GraveType.Relic)
        {
            int baseCount = PickCount(graveLootTable.relicDropCounts);
            int totalCount = baseCount + bonusCount;

            for (int i = 0; i < totalCount; i++)
            {
                ItemRarity rarity = RollGraveRelicRarity(bonusRareChance, bonusEpicChance);
                var relic = GetRandomRelicByRarity(rarity);

                if (relic != null) SpawnLootObject(position + GetRandomOffset(), relic);
            }
        }
    }

    private Vector3 GetRandomOffset()
    {
        return new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
    }

    private ItemRarity RollGraveRelicRarity(float bonusRareChance, float bonusEpicChance)
    {
        float normalW = graveLootTable.normalRelicWeight;
        float rareW = graveLootTable.rareRelicWeight + bonusRareChance;
        float epicW = graveLootTable.epicRelicWeight + bonusEpicChance;

        float total = normalW + rareW + epicW;
        float rand = Random.Range(0f, total);

        if (rand < normalW) return ItemRarity.Common;
        if (rand < normalW + rareW) return ItemRarity.Rare;

        return ItemRarity.Epic;
    }

    private RelicDefinition GetRandomRelicByRarity(ItemRarity targetRarity)
    {
        var pool = ItemManager.Instance.GetUnlockedRelicIDs();
        if (pool.Count == 0) return null;

        List<string> filteredPool = new List<string>();

        foreach (var id in pool)
        {
            var relicData = ItemManager.Instance.GetRelicData(id);
            if (relicData != null)
            {
                filteredPool.Add(id);
            }
        }

        if (filteredPool.Count == 0)
            return ItemManager.Instance.GetRelicData(pool[Random.Range(0, pool.Count)]);

        string pickedID = filteredPool[Random.Range(0, filteredPool.Count)];
        return ItemManager.Instance.GetRelicData(pickedID);
    }
}
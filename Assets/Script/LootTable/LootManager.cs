using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LootManager : MonoBehaviour
{
    public static LootManager Instance { get; private set; }

    [Header("Settings")]
    public GameObject worldItemPrefab; // 바닥에 떨어질 아이템 프리팹 (WorldItemPickup2D)

    [Header("References")]
    public ItemDatabase itemDatabase;
    public List<StageLootTable> stageTables;

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

    // [수정됨] Legendary 제외 버전
    private ItemRarity RollRelicRarity(StageLootTable table)
    {
        // Legendary 가중치 제거 (Common + Rare + Epic)
        int total = table.commonWeight + table.rareWeight + table.epicWeight;
        int rand = Random.Range(0, total);
        int sum = 0;

        sum += table.commonWeight; if (rand < sum) return ItemRarity.Common;
        sum += table.rareWeight; if (rand < sum) return ItemRarity.Rare;

        // 남은 확률은 무조건 Epic
        return ItemRarity.Epic;
    }

    // =========================================================
    // 2. 단일 아이템 데이터 뽑기 (DB 조회 + 해금 + 중복체크)
    // =========================================================

    // 무기: 중복 방지 리스트(exclusionList) 적용
    public WeaponDefinition GetRandomWeapon(HashSet<string> exclusionList)
    {
        // 해금된 목록에서만 조회
        var pool = itemDatabase.unlockedWeapons;

        // 제외 목록에 없는 것만 필터링
        var valid = pool.Where(w => !exclusionList.Contains(w.weaponId)).ToList();

        if (valid.Count == 0) return null; // 뽑을 게 없음
        return valid[Random.Range(0, valid.Count)];
    }

    // 유물: 스테이지별 등급 확률 적용
    public RelicDefinition GetRandomRelic()
    {
        // 해금된 목록에서만 조회
        var pool = itemDatabase.unlockedRelics;
        if (pool.Count == 0) return null;

        StageLootTable table = GetCurrentTable();
        ItemRarity rarity = RollRelicRarity(table);

        // 해당 등급이면서 해금된 유물 필터링
        var valid = pool.Where(r => r.rarity == rarity).ToList();

        // 해당 등급에 해금된 게 없으면 전체 해금 목록에서 랜덤 (Fallback)
        if (valid.Count == 0) valid = pool;

        return valid[Random.Range(0, valid.Count)];
    }

    // =========================================================
    // 3. [상자용] 드롭 리스트 생성
    // =========================================================
    public List<ScriptableObject> GenerateChestLoot(HashSet<string> playerWeapons)
    {
        List<ScriptableObject> drops = new List<ScriptableObject>();
        StageLootTable table = GetCurrentTable();

        // A. 무기 생성 (플레이어 소지품 + 현재 상자 내 중복 방지)
        int wCount = PickCount(table.chestWeaponCounts);
        HashSet<string> banList = new HashSet<string>(playerWeapons);

        for (int i = 0; i < wCount; i++)
        {
            var weapon = GetRandomWeapon(banList);
            if (weapon != null)
            {
                drops.Add(weapon);
                banList.Add(weapon.weaponId); // 이번 상자에서 중복 방지 등록
            }
        }

        // B. 유물 생성
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
    public void SpawnMonsterLoot(Vector3 position, HashSet<string> playerWeapons)
    {
        StageLootTable table = GetCurrentTable();

        // 전체 가중치 합산 (꽝 포함)
        int totalWeight = table.mobNothingWeight + table.mobWeaponWeight + table.mobRelicWeight
                          + table.mobConsumableWeight + table.mobFieldItemWeight;

        int rand = Random.Range(0, totalWeight);
        int sum = 0;

        // 1. 꽝 (Nothing)
        sum += table.mobNothingWeight;
        if (rand < sum) return;

        // 2. 무기
        sum += table.mobWeaponWeight;
        if (rand < sum)
        {
            var weapon = GetRandomWeapon(playerWeapons);
            if (weapon != null) SpawnLootObject(position, weapon);
            return;
        }

        // 3. 유물
        sum += table.mobRelicWeight;
        if (rand < sum)
        {
            var relic = GetRandomRelic();
            if (relic != null) SpawnLootObject(position, relic);
            return;
        }

        // 4. 소비 아이템
        sum += table.mobConsumableWeight;
        if (rand < sum)
        {
            // [TODO] var potion = GetRandomConsumable();
            // if (potion != null) SpawnLootObject(position, potion);
            return;
        }

        // 5. 필드 아이템
        sum += table.mobFieldItemWeight;
        if (rand < sum)
        {
            // [TODO] var heart = GetRandomFieldItem();
            // if (heart != null) SpawnLootObject(position, heart);
            return;
        }
    }

    // =========================================================
    // 5. 실제 월드 오브젝트 생성
    // =========================================================
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

    // =========================================================
    // [New] 보스 마정석 개수 계산 (현재 스테이지 테이블 기준)
    // =========================================================
    public int GetBossMagicStoneCount()
    {
        StageLootTable table = GetCurrentTable();
        if (table == null) return 0;

        // 테이블에 적힌 개수 그대로 반환 (예: 5)
        return table.bossStoneCount;
    }
}
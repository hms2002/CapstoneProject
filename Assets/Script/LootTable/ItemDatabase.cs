using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game/Database/Item Database")]
public class ItemDatabase : ScriptableObject
{
    // =========================================================
    // [원본 데이터] 절대 코드에서 Add/Remove 하지 않음! (ReadOnly)
    // =========================================================
    [Header("Source Data (Edit Here)")]
    [Tooltip("게임의 모든 무기를 여기에 등록하세요")]
    public List<WeaponDefinition> allWeapons;

    [Tooltip("처음부터 해금되어 있을 무기만 여기에 등록하세요")]
    public List<WeaponDefinition> defaultUnlockedWeapons;

    [Tooltip("게임의 모든 유물을 여기에 등록하세요")]
    public List<RelicDefinition> allRelics;

    [Tooltip("처음부터 해금되어 있을 유물만 여기에 등록하세요")]
    public List<RelicDefinition> defaultUnlockedRelics;

    // =========================================================
    // [런타임 데이터] 게임 중에만 사용하는 임시 리스트
    // =========================================================
    // 인스펙터에서 헷갈리지 않게 숨기거나, 디버깅용으로만 봅니다.
    [Header("Runtime State (Do Not Edit)")]
    public List<WeaponDefinition> unlockedWeapons = new List<WeaponDefinition>();
    public List<WeaponDefinition> lockedWeapons = new List<WeaponDefinition>();

    public List<RelicDefinition> unlockedRelics = new List<RelicDefinition>();
    public List<RelicDefinition> lockedRelics = new List<RelicDefinition>();

    // =========================================================
    // 초기화 로직
    // =========================================================
    public void Initialize(ItemSaveData saveData)
    {
        // 1. 런타임 리스트 초기화 (싹 비우고 시작)
        unlockedWeapons.Clear();
        lockedWeapons.Clear();
        unlockedRelics.Clear();
        lockedRelics.Clear();

        // 2. 기본 해금 아이템 적용
        if (defaultUnlockedWeapons != null) unlockedWeapons.AddRange(defaultUnlockedWeapons);
        if (defaultUnlockedRelics != null) unlockedRelics.AddRange(defaultUnlockedRelics);

        // 3. 잠긴 아이템 계산 (전체 - 해금 = 잠김)
        // 무기
        if (allWeapons != null)
        {
            foreach (var w in allWeapons)
            {
                // 이미 해금 목록에 없으면 잠긴 목록에 추가
                if (!unlockedWeapons.Contains(w)) lockedWeapons.Add(w);
            }
        }

        // 유물
        if (allRelics != null)
        {
            foreach (var r in allRelics)
            {
                if (!unlockedRelics.Contains(r)) lockedRelics.Add(r);
            }
        }

        // 4. 세이브 데이터 적용 (저장된 ID가 있으면 잠김 -> 해금 이동)
        if (saveData != null)
        {
            ApplySaveData(saveData);
        }

        Debug.Log($"[ItemDatabase] 초기화 완료. (무기 해금: {unlockedWeapons.Count}/{allWeapons.Count})");
    }

    private void ApplySaveData(ItemSaveData data)
    {
        // 무기 복구
        if (data.unlockedWeaponIDs != null)
        {
            foreach (string id in data.unlockedWeaponIDs)
            {
                if (unlockedWeapons.Exists(x => x.weaponId == id)) continue; // 이미 해금됨

                var target = lockedWeapons.Find(x => x.weaponId == id);
                if (target != null)
                {
                    lockedWeapons.Remove(target);
                    unlockedWeapons.Add(target);
                }
            }
        }

        // 유물 복구
        if (data.unlockedRelicIDs != null)
        {
            foreach (string id in data.unlockedRelicIDs)
            {
                if (unlockedRelics.Exists(x => x.relicId == id)) continue;

                var target = lockedRelics.Find(x => x.relicId == id);
                if (target != null)
                {
                    lockedRelics.Remove(target);
                    unlockedRelics.Add(target);
                }
            }
        }
    }

    // [수정됨] ID 필드를 비교하여 검색
    public WeaponDefinition GetWeaponByID(string id)
    {
        // w.name == id (X) -> w.weaponId == id (O)
        return allWeapons.Find(w => w.weaponId == id);
    }

    // [수정됨] ID 필드를 비교하여 검색
    public RelicDefinition GetRelicByID(string id)
    {
        // r.name == id (X) -> r.relicId == id (O)
        return allRelics.Find(r => r.relicId == id);
    }

    public void UnlockWeapon(string id)
    {
        // 이미 해금되었는지 확인 (weaponId로 비교)
        if (unlockedWeapons.Exists(w => w.weaponId == id)) return;

        // 전체 리스트에서 ID로 찾기
        var weapon = allWeapons.Find(w => w.weaponId == id);

        if (weapon != null)
        {
            unlockedWeapons.Add(weapon);
            Debug.Log($"[ItemDatabase] 무기 해금됨: {weapon.displayName}");
        }
        else
        {
            Debug.LogWarning($"[ItemDatabase] 해금 실패: ID '{id}'에 해당하는 무기를 찾을 수 없습니다.");
        }
    }

    // 유물 해금 로직에서도 ID 비교
    public void UnlockRelic(string id)
    {
        if (IsRelicUnlocked(id)) return;

        // ID 필드로 검색
        var relic = allRelics.Find(r => r.relicId == id);

        if (relic != null)
        {
            // 해금 리스트(unlockedRelics)가 List<RelicDefinition>이라면 바로 추가
            if (!unlockedRelics.Contains(relic))
            {
                unlockedRelics.Add(relic);
            }
        }
    }

    public bool IsRelicUnlocked(string id)
    {
        // 여기서도 ID 필드로 비교
        return unlockedRelics.Exists(r => r.relicId == id);
    }
}
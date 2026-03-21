using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Game/Database/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [Header("Source Data (Edit Here)")]
    [Tooltip("게임의 모든 무기를 여기에 등록하세요")]
    public List<WeaponDefinition> allWeapons;

    [Tooltip("처음부터 해금되어 있을 무기만 여기에 등록하세요")]
    public List<WeaponDefinition> defaultUnlockedWeapons;

    [Tooltip("게임의 모든 유물을 여기에 등록하세요")]
    public List<RelicDefinition> allRelics;

    [Tooltip("처음부터 해금되어 있을 유물만 여기에 등록하세요")]
    public List<RelicDefinition> defaultUnlockedRelics;

    // 런타임 캐싱용 딕셔너리
    private Dictionary<string, WeaponDefinition> weaponDict;
    private Dictionary<string, RelicDefinition> relicDict;

    public void InitializeCache()
    {
        weaponDict = new Dictionary<string, WeaponDefinition>();
        if (allWeapons != null)
        {
            foreach (var w in allWeapons)
            {
                if (w != null && !string.IsNullOrEmpty(w.weaponId))
                    weaponDict[w.weaponId] = w;
            }
        }

        relicDict = new Dictionary<string, RelicDefinition>();
        if (allRelics != null)
        {
            foreach (var r in allRelics)
            {
                if (r != null && !string.IsNullOrEmpty(r.relicId))
                    relicDict[r.relicId] = r;
            }
        }
    }

    public WeaponDefinition GetWeaponByID(string id)
    {
        if (weaponDict == null) InitializeCache();

        if (weaponDict.TryGetValue(id, out var weapon))
            return weapon;

        return null;
    }

    public RelicDefinition GetRelicByID(string id)
    {
        if (relicDict == null) InitializeCache();

        if (relicDict.TryGetValue(id, out var relic))
            return relic;

        return null;
    }
}
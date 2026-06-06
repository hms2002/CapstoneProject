using System;
using System.Collections.Generic;
using UnityEngine;

public enum EncyclopediaCategory
{
    Weapon,
    Monster,
    Boss,
    Relic,
    Consumable
}

public enum EncyclopediaMainTab
{
    Item,
    Monster,
    Boss
}

public enum EncyclopediaItemSubTab
{
    Weapon,
    Relic,
    Consumable
}

[Serializable]
public sealed class EncyclopediaWeaponEntry
{
    public string idOverride;
    public WeaponDefinition weapon;
    public Sprite imageOverride;
    public string stageText;

    public string Id => !string.IsNullOrWhiteSpace(idOverride)
        ? idOverride
        : weapon != null ? weapon.weaponId : string.Empty;

    public string DisplayName => weapon != null ? weapon.displayName : Id;
    public Sprite Image => imageOverride != null ? imageOverride : weapon != null ? weapon.icon : null;
}

[Serializable]
public sealed class EncyclopediaMonsterEntry
{
    public string id;
    public string displayName;
    public Sprite image;
    public string type;
    public string attackStyle;
    public string stageText;
    [TextArea] public string storyText;
    public GameObject sourcePrefab;
}

[Serializable]
public sealed class EncyclopediaBossEntry
{
    public string id;
    public string displayName;
    public Sprite image;
    public string type;
    public string attackStyle;
    public string stageText;
    [TextArea] public string storyText;
    public GameObject sourcePrefab;
    public NPCData npcData;
}

[CreateAssetMenu(fileName = "EncyclopediaCatalog", menuName = "Game/Encyclopedia/Catalog")]
public sealed class EncyclopediaCatalogSO : ScriptableObject
{
    [Header("Weapons")]
    [SerializeField] private List<EncyclopediaWeaponEntry> weaponEntries = new();

    [Header("Monsters")]
    [SerializeField] private List<EncyclopediaMonsterEntry> monsterEntries = new();

    [Header("Bosses")]
    [SerializeField] private List<EncyclopediaBossEntry> bossEntries = new();

    public IReadOnlyList<EncyclopediaWeaponEntry> WeaponEntries => weaponEntries;
    public IReadOnlyList<EncyclopediaMonsterEntry> MonsterEntries => monsterEntries;
    public IReadOnlyList<EncyclopediaBossEntry> BossEntries => bossEntries;

    public int GetCount(EncyclopediaCategory category)
    {
        return category switch
        {
            EncyclopediaCategory.Weapon => weaponEntries != null ? weaponEntries.Count : 0,
            EncyclopediaCategory.Monster => monsterEntries != null ? monsterEntries.Count : 0,
            EncyclopediaCategory.Boss => bossEntries != null ? bossEntries.Count : 0,
            _ => 0
        };
    }

    public string GetDisplayName(EncyclopediaCategory category, int index)
    {
        return category switch
        {
            EncyclopediaCategory.Weapon => TryGetWeapon(index, out var weapon) ? weapon.DisplayName : string.Empty,
            EncyclopediaCategory.Monster => TryGetMonster(index, out var monster) ? monster.displayName : string.Empty,
            EncyclopediaCategory.Boss => TryGetBoss(index, out var boss) ? boss.displayName : string.Empty,
            _ => string.Empty
        };
    }

    public Sprite GetImage(EncyclopediaCategory category, int index)
    {
        return category switch
        {
            EncyclopediaCategory.Weapon => TryGetWeapon(index, out var weapon) ? weapon.Image : null,
            EncyclopediaCategory.Monster => TryGetMonster(index, out var monster) ? monster.image : null,
            EncyclopediaCategory.Boss => TryGetBoss(index, out var boss) ? boss.image : null,
            _ => null
        };
    }

    public bool TryGetWeapon(int index, out EncyclopediaWeaponEntry entry)
    {
        return TryGetEntry(weaponEntries, index, out entry);
    }

    public bool TryGetMonster(int index, out EncyclopediaMonsterEntry entry)
    {
        return TryGetEntry(monsterEntries, index, out entry);
    }

    public bool TryGetBoss(int index, out EncyclopediaBossEntry entry)
    {
        return TryGetEntry(bossEntries, index, out entry);
    }

#if UNITY_EDITOR
    public void SetEntries(
        IEnumerable<EncyclopediaWeaponEntry> weapons,
        IEnumerable<EncyclopediaMonsterEntry> monsters,
        IEnumerable<EncyclopediaBossEntry> bosses)
    {
        weaponEntries = weapons != null ? new List<EncyclopediaWeaponEntry>(weapons) : new List<EncyclopediaWeaponEntry>();
        monsterEntries = monsters != null ? new List<EncyclopediaMonsterEntry>(monsters) : new List<EncyclopediaMonsterEntry>();
        bossEntries = bosses != null ? new List<EncyclopediaBossEntry>(bosses) : new List<EncyclopediaBossEntry>();
    }
#endif

    private static bool TryGetEntry<T>(IReadOnlyList<T> entries, int index, out T entry) where T : class
    {
        if (entries != null && index >= 0 && index < entries.Count)
        {
            entry = entries[index];
            return entry != null;
        }

        entry = default;
        return false;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 저장된 문자열 식별자를 실제 게임 데이터 객체로 해석하는 씬 복원용 Resolver 구현체.
/// 무기/유물은 ItemManager를 통해 조회하고,
/// Attribute / Tag / Ability / Effect 는 PlayerRuntimeRestoreCatalogSO를 통해 조회한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRuntimeResolverBridge : MonoBehaviour, IPlayerRuntimeResolver
{
    [Header("Restore Catalog")]
    [SerializeField] private PlayerRuntimeRestoreCatalogSO restoreCatalog;

    private readonly Dictionary<string, AttributeDefinition> attributeById = new();
    private readonly Dictionary<string, GameplayTag> tagByKey = new();
    private readonly Dictionary<string, AbilityDefinition> abilityById = new();
    private readonly Dictionary<string, GameplayEffect> effectById = new();

    private void Awake()
    {
        RebuildCaches();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildCaches();
    }
#endif

    /// <summary>
    /// 책임 : 연결된 복원 카탈로그를 읽어 런타임 조회용 캐시를 재구성한다.
    /// </summary>
    public void RebuildCaches()
    {
        attributeById.Clear();
        tagByKey.Clear();
        abilityById.Clear();
        effectById.Clear();

        if (restoreCatalog == null)
            return;

        RegisterAttributes(restoreCatalog.AttributeCatalog);
        RegisterTags(restoreCatalog.TagSets, restoreCatalog.ExtraTags);
        RegisterAbilities(restoreCatalog.Abilities);
        RegisterEffects(restoreCatalog.Effects);
    }

    /// <summary>
    /// 책임 : AttributeCatalogSO에 들어 있는 AttributeDefinition을 캐시에 등록한다.
    /// 현재 저장 키가 def.name 이므로 동일하게 asset.name 기준으로 등록한다.
    /// </summary>
    private void RegisterAttributes(AttributeCatalogSO catalog)
    {
        if (catalog == null || catalog.Attributes == null)
            return;

        var source = catalog.Attributes;
        for (int i = 0; i < source.Length; i++)
        {
            var def = source[i];
            if (def == null)
                continue;

            AddIfValid(attributeById, def.name, def, "Attribute");
        }
    }

    /// <summary>
    /// 책임 : TagSet과 추가 태그 목록을 모두 수집해 태그 캐시에 등록한다.
    /// 현재 저장 키는 tag.Name 이지만, 호환성을 위해 Path / AssetName 도 함께 등록한다.
    /// </summary>
    private void RegisterTags(GameplayTagSet[] sets, GameplayTag[] extras)
    {
        var collected = new HashSet<GameplayTag>();

        if (sets != null)
        {
            for (int i = 0; i < sets.Length; i++)
            {
                sets[i]?.CollectTags(collected);
            }
        }

        if (extras != null)
        {
            for (int i = 0; i < extras.Length; i++)
            {
                var tag = extras[i];
                if (tag != null)
                    collected.Add(tag);
            }
        }

        foreach (var tag in collected)
        {
            RegisterSingleTag(tag);
        }
    }

    /// <summary>
    /// 책임 : 단일 GameplayTag를 복원 호환 키들(Name / Path / AssetName)로 캐시에 등록한다.
    /// </summary>
    private void RegisterSingleTag(GameplayTag tag)
    {
        if (tag == null)
            return;

        AddIfValid(tagByKey, tag.Name, tag, "Tag(Name)");
        AddIfValid(tagByKey, tag.Path, tag, "Tag(Path)");
        AddIfValid(tagByKey, tag.name, tag, "Tag(AssetName)");
    }

    /// <summary>
    /// 책임 : AbilityDefinition 목록을 캐시에 등록한다.
    /// 현재 저장 키가 def.name 이므로 asset.name 기준으로 등록한다.
    /// </summary>
    private void RegisterAbilities(AbilityDefinition[] source)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Length; i++)
        {
            var def = source[i];
            if (def == null)
                continue;

            AddIfValid(abilityById, def.name, def, "Ability");
        }
    }

    /// <summary>
    /// 책임 : GameplayEffect 목록을 캐시에 등록한다.
    /// 현재 저장 키가 effect.name 이므로 asset.name 기준으로 등록한다.
    /// </summary>
    private void RegisterEffects(GameplayEffect[] source)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Length; i++)
        {
            var effect = source[i];
            if (effect == null)
                continue;

            AddIfValid(effectById, effect.name, effect, "Effect");
        }
    }

    /// <summary>
    /// 책임 : 유효한 키/값만 캐시에 등록하고, 중복 키가 있으면 첫 등록값을 유지한다.
    /// </summary>
    private void AddIfValid<T>(Dictionary<string, T> dict, string key, T value, string label)
        where T : Object
    {
        if (string.IsNullOrWhiteSpace(key) || value == null)
            return;

        if (dict.ContainsKey(key))
        {
            Debug.LogWarning($"[PlayerRuntimeResolverBridge] 중복 {label} 키 감지: '{key}'", value);
            return;
        }

        dict.Add(key, value);
    }

    /// <summary>
    /// 책임 : 저장된 weaponId를 실제 WeaponDefinition으로 해석한다.
    /// </summary>
    public WeaponDefinition ResolveWeapon(string weaponId)
    {
        if (string.IsNullOrWhiteSpace(weaponId))
            return null;

        if (ItemManager.Instance == null)
        {
            Debug.LogWarning("[PlayerRuntimeResolverBridge] ItemManager.Instance가 없어 Weapon 조회를 할 수 없습니다.", this);
            return null;
        }

        return ItemManager.Instance.GetWeaponData(weaponId);
    }

    /// <summary>
    /// 책임 : 저장된 relicId를 실제 RelicDefinition으로 해석한다.
    /// </summary>
    public RelicDefinition ResolveRelic(string relicId)
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return null;

        if (ItemManager.Instance == null)
        {
            Debug.LogWarning("[PlayerRuntimeResolverBridge] ItemManager.Instance가 없어 Relic 조회를 할 수 없습니다.", this);
            return null;
        }

        return ItemManager.Instance.GetRelicData(relicId);
    }

    /// <summary>
    /// 책임 : 저장된 attributeId를 실제 AttributeDefinition으로 해석한다.
    /// </summary>
    public AttributeDefinition ResolveAttribute(string attributeId)
    {
        if (string.IsNullOrWhiteSpace(attributeId))
            return null;

        attributeById.TryGetValue(attributeId, out var def);
        return def;
    }

    /// <summary>
    /// 책임 : 저장된 tagName을 실제 GameplayTag로 해석한다.
    /// </summary>
    public GameplayTag ResolveTag(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName))
            return null;

        tagByKey.TryGetValue(tagName, out var tag);
        return tag;
    }

    /// <summary>
    /// 책임 : 저장된 abilityId를 실제 AbilityDefinition으로 해석한다.
    /// </summary>
    public AbilityDefinition ResolveAbility(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
            return null;

        abilityById.TryGetValue(abilityId, out var def);
        Debug.Log($"[Resolver] ResolveAbility id={abilityId}, found={(def != null)}", this);
        return def;
    }

    /// <summary>
    /// 책임 : 저장된 effectId를 실제 GameplayEffect로 해석한다.
    /// </summary>
    public GameplayEffect ResolveEffect(string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId))
            return null;

        effectById.TryGetValue(effectId, out var effect);
        return effect;
    }
}
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 플레이어 씬 전이 복원 시 필요한 정의 자산들의 "공식 조회 목록"을 제공한다.
/// 이미 별도 카탈로그가 있는 데이터(Attribute)는 그 카탈로그를 참조하고,
/// 별도 카탈로그가 없는 데이터(Tag / Ability / Effect)는 수동 목록으로 묶어 제공한다.
/// </summary>
[CreateAssetMenu(fileName = "PlayerRuntimeRestoreCatalog", menuName = "Game/Runtime/Player Runtime Restore Catalog")]
public sealed class PlayerRuntimeRestoreCatalogSO : ScriptableObject
{
    [Header("Existing Catalogs")]
    [SerializeField] private AttributeCatalogSO attributeCatalog;

    [Header("Tag Sources")]
    [SerializeField] private GameplayTagSet[] tagSets;
    [SerializeField] private GameplayTag[] extraTags;

    [Header("Manual Definition Sources")]
    [SerializeField] private AbilityDefinition[] abilities;
    [SerializeField] private GameplayEffect[] effects;

    public AttributeCatalogSO AttributeCatalog => attributeCatalog;
    public GameplayTagSet[] TagSets => tagSets;
    public GameplayTag[] ExtraTags => extraTags;
    public AbilityDefinition[] Abilities => abilities;
    public GameplayEffect[] Effects => effects;
}
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 저장된 문자열 식별자를 실제 게임 데이터 객체로 해석한다.
/// 씬 복원 코디네이터가 카탈로그/DB 구현을 직접 몰라도 되도록 중간 해석 창구 역할을 한다.
/// </summary>
public interface IPlayerRuntimeResolver
{
    WeaponDefinition ResolveWeapon(string weaponId);
    RelicDefinition ResolveRelic(string relicId);
    AttributeDefinition ResolveAttribute(string attributeId);
    GameplayTag ResolveTag(string tagName);
    AbilityDefinition ResolveAbility(string abilityId);
    GameplayEffect ResolveEffect(string effectId);
}
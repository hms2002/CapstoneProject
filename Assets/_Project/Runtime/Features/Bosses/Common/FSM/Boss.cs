using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// 일반 Enemy의 전투 기능 위에 보스 전용 규칙(그로기, 보스 사망 처리 등)을 추가한다.
/// </summary>
public class Boss : Enemy
{
    [Header("Boss's Attributes")]
    [SerializeField] protected AttributeDefinition staggerDef;

    [Header("Boss's Effects")]
    [SerializeField] private GameplayEffect groggyEffect;

    /// <summary>보스 Attribute 변화에 따라 그로기와 사망 처리를 실행합니다.</summary>
    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);

        if (attribute == staggerDef && newValue <= 0 && oldValue > 0)
        {
            ApplyGroggy();
        }

        // 체력이 0 이하가 되는 순간 사망 처리
        if (attribute == healthDef && newValue <= 0 && oldValue > 0)
        {
            Die();
        }
    }

    /// <summary>보스에게 그로기 GameplayEffect를 적용합니다.</summary>
    private void ApplyGroggy()
    {
        TryApplySelfEffect(groggyEffect);
    }

    /// <summary>보스 사망 보상을 처리하고 공통 사망 처리를 실행합니다.</summary>
    protected override void OnDeathStarted()
    {
        RunProgressPlayback.NotifyBossRewardsReady(null);
    }

    /// <summary>보스가 현재 그로기 상태인지 반환합니다.</summary>
    public bool IsGroggy()
    {
        return HasStateTagPath("State.Status.Groggy");
    }

    /// <summary>현재 타겟을 대상으로 지정한 Ability 사용을 시도합니다.</summary>
    public bool TryUseAbility(AbilityDefinition ability)
    {
        return TryActivateAbility(ability, target != null ? target.gameObject : null);
    }

    /// <summary>
    /// 책임 :
    /// - 오래된 보스 베이스가 자기 자신에게 효과를 적용할 때 EffectRunner 세부 호출을 한 곳에 가둔다.
    /// - 하위 보스 구현이 공통 자기 효과 적용 규칙을 재사용하게 돕는다.
    /// </summary>
    protected bool TryApplySelfEffect(GameplayEffect effect)
    {
        if (effectRunner == null || effect == null)
            return false;

        effectRunner.ApplyEffect(effect, gameObject, gameObject);
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 오래된 보스 베이스가 태그 경로 해석과 TagSystem 조회를 한 곳에 모아 직접 인지 범위를 줄인다.
    /// - 하위 구현은 경로만 넘기고 실제 태그 시스템 세부를 직접 알지 않게 한다.
    /// </summary>
    protected bool HasStateTagPath(string tagPath)
    {
        if (tagSystem == null || string.IsNullOrWhiteSpace(tagPath))
            return false;

        return tagSystem.HasTagId(TagRegistry.GetIdByPath(tagPath));
    }

    /// <summary>
    /// 책임 :
    /// - 오래된 보스 베이스가 AbilitySystem 직접 호출을 한 곳에 가둬 하위 구현의 결합을 줄인다.
    /// - 현재 타깃 기본값 규칙을 함께 소유해 보스별 사용 시점을 단순화한다.
    /// </summary>
    protected bool TryActivateAbility(AbilityDefinition ability, GameObject explicitTarget = null)
    {
        if (abilitySystem == null || ability == null)
            return false;

        GameObject targetObject = explicitTarget != null ? explicitTarget : target != null ? target.gameObject : null;
        return abilitySystem.TryActivateAbility(ability, targetObject);
    }
}

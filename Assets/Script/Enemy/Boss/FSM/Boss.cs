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

    private BossDrop bossDrop;

    protected override void Awake()
    {
        base.Awake();
        bossDrop = GetComponent<BossDrop>();
    }

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
        effectRunner.ApplyEffect(groggyEffect, gameObject, gameObject);
    }

    /// <summary>보스 사망 보상을 처리하고 공통 사망 처리를 실행합니다.</summary>
    protected override void OnDeathStarted()
    {
        if (bossDrop != null)
        {
            bossDrop.OnBossDead();
        }
    }

    /// <summary>보스가 현재 그로기 상태인지 반환합니다.</summary>
    public bool IsGroggy()
    {
        return tagSystem.HasTagId(TagRegistry.GetIdByPath("State.Status.Groggy"));
    }

    /// <summary>현재 타겟을 대상으로 지정한 Ability 사용을 시도합니다.</summary>
    public bool TryUseAbility(AbilityDefinition ability)
    {
        return abilitySystem.TryActivateAbility(ability, target?.gameObject);
    }
}

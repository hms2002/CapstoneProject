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

    private void ApplyGroggy()
    {
        effectRunner.ApplyEffect(groggyEffect, gameObject, gameObject);
    }

    protected override void Die()
    {
        if (bossDrop != null)
        {
            bossDrop.OnBossDead();
        }

        base.Die();
    }

    /// <summary>BT Condition: 지금 그로기 상태인가?</summary>
    public bool IsGroggy()
    {
        return tagSystem.HasTagId(TagRegistry.GetIdByPath("State.Status.Groggy"));
    }

    /// <summary>BT Action: 스킬 사용해!</summary>
    public bool TryUseAbility(AbilityDefinition ability)
    {
        return abilitySystem.TryActivateAbility(ability, target?.gameObject);
    }
}
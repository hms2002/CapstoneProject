using UnityEngine;
using UnityGAS;

public class Boss : Enemy
{
    [Header("Boss's Attributes")]
    [SerializeField] protected AttributeDefinition staggerDef;

    [Header("Boss's Effects")]
    [SerializeField] private GameplayEffect groggyEffect; // 3초 지속 GE

    // ----------------------------------------------------
    // [이벤트 리스너] 즉각적인 반응이 필요한 규칙 처리

    protected override void OnEnemyAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        base.OnEnemyAttributeChanged(attribute, oldValue, newValue);

        // 1. 그로기 체크
        if (attribute == staggerDef && newValue <= 0 && oldValue > 0)
        {
            ApplyGroggy();
        }
    }

    private void ApplyGroggy()
    {
        // 보스에게 3초간 Groggy 태그를 가진 GE 적용
        effectRunner.ApplyEffect(groggyEffect, gameObject, gameObject);

        // 3초 후 게이지 회복은 GE_Groggy_Status의 OnRemove 컨테이너나 
        // 별도의 코루틴 어빌리티에서 처리 권장
    }

    // ----------------------------------------------------
    // [BT 전용 인터페이스] BT 노드들이 호출할 함수들

    /// <summary> BT Condition: "지금 그로기 상태인가?" </summary>
    public bool IsGroggy()
    {
        // TagSystem에 그로기 태그가 있는지 확인
        return tagSystem.HasTagId(TagRegistry.GetIdByPath("State.Status.Groggy"));
    }

    /// <summary> BT Action: "스킬 사용해!" </summary>
    public bool TryUseAbility(AbilityDefinition ability)
    {
        // 그로기 상태라면 시스템 레벨에서 막히거나, 이미 BT에서 걸러짐
        return abilitySystem.TryActivateAbility(ability, target?.gameObject);
    }
}
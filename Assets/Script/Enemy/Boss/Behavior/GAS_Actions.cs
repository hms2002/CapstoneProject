using System;
using UnityEngine;
using Unity.Behavior;
using UnityGAS;
using Action = Unity.Behavior.Action;

/// <summary>
/// Action Node: GAS 스킬 실행 (비동기 대기 포함)
/// </summary>
[Serializable, Unity.Properties.GeneratePropertyBag]
[NodeDescription(name: "Activate GAS Ability", story: "Use [Ability] on [Target]", category: "GAS", id: "GAS_ActivateAbility")]
public class ActivateGASAbilityAction : Action
{
    [SerializeReference] public  BlackboardVariable<AbilityDefinition>  Ability;
    [SerializeReference] public BlackboardVariable<GameObject>          Target;

    private AbilitySystem       abilitySystem;
    private bool                isRunning;
    private bool                isSuccess;
    private AbilityDefinition   cachedDef; //없애도 될듯

    protected override Status OnStart()
    {
        if (Ability.Value == null) return Status.Failure;

        abilitySystem = this.GameObject.GetComponent<AbilitySystem>();
        if (abilitySystem == null) return Status.Failure;

        cachedDef = Ability.Value;

        // 스킬 쿨타임/비용 등을 체크하고 실행 시도
        if (abilitySystem.TryActivateAbility(cachedDef, Target.Value))
        {
            isRunning = true;
            isSuccess = false;

            // 스킬이 끝날 때까지 결과를 기다리기 위해 이벤트 구독
            abilitySystem.OnAbilityCastCompleted += OnCompleted;
            abilitySystem.OnAbilityCastCancelled += OnCancelled;

            // "아직 실행 중입니다"라고 시스템에 보고 (-> 다음 프레임에 OnUpdate 호출됨)
            return Status.Running;
        }

        // 쿨타임 중이거나 실행 불가
        return Status.Failure;
    }

    protected override Status OnUpdate()
    {
        // 이벤트가 와서 _isRunning이 false가 될 때까지 계속 Running 반환
        if (isRunning) return Status.Running;

        // 이벤트 수신 후 결과 반환
        return isSuccess ? Status.Success : Status.Failure;
    }

    protected override void OnEnd()
    {
        // 노드 종료 시 이벤트 구독 해제 (안전장치)
        if (abilitySystem != null)
        {
            abilitySystem.OnAbilityCastCompleted -= OnCompleted;
            abilitySystem.OnAbilityCastCancelled -= OnCancelled;
        }

        isRunning       = false;
        abilitySystem   = null;
        cachedDef       = null;
    }

    // GAS 시스템에서 호출해주는 콜백
    private void OnCompleted(AbilityDefinition def)
    {
        if (def == cachedDef)
        {
            isSuccess = true;
            isRunning = false;
        }
    }

    private void OnCancelled(AbilityDefinition def)
    {
        if (def == cachedDef)
        {
            isSuccess = false; // 취소됨 = 실패 처리
            isRunning = false;
        }
    }
}

// --------------------------------------------------------------------------
// 2. Condition Node: 태그 보유 여부 체크

[Serializable, Unity.Properties.GeneratePropertyBag]
[NodeDescription(name: "Has GAS Tag", story: "Self has tag [TagName]", category: "GAS", id: "GAS_HasTag")]
public class HasGASTagCondition : Condition
{
    [SerializeReference] public BlackboardVariable<string>      TagName;
    [SerializeReference] public BlackboardVariable<GameObject>  Self;

    public override bool IsTrue()
    {
        if (Self.Value == null || string.IsNullOrEmpty(TagName.Value)) return false;

        TagSystem tagSystem = Self.Value.GetComponent<TagSystem>();
        if (tagSystem == null) return false;

        int id = TagRegistry.GetIdByPath(TagName.Value);
        if (id == -1) return false;

        GameplayTag tag = TagRegistry.GetTag(id);

        return tagSystem.HasTag(tag);
    }
}

// --------------------------------------------------------------------------
// 3. Condition Node: 체력 비율 체크 (HP < 50% 등)
// --------------------------------------------------------------------------
[Serializable, Unity.Properties.GeneratePropertyBag]
[NodeDescription(name: "Check Attribute Ratio", story: "Is [CurrentAttr] / [MaxAttr] <= [Ratio]", category: "GAS", id: "GAS_CheckRatio")]
public class CheckAttributeRatioCondition : Condition
{
    [SerializeReference] public BlackboardVariable<AttributeDefinition> CurrentAttribute;
    [SerializeReference] public BlackboardVariable<AttributeDefinition> MaxAttribute;
    [SerializeReference] public BlackboardVariable<float>               Ratio;
    [SerializeReference] public BlackboardVariable<GameObject>          Agent;

    public override bool IsTrue()
    {
        if (Agent.Value == null || CurrentAttribute.Value == null || MaxAttribute.Value == null) return false;

        AttributeSet attributeSet = Agent.Value.GetComponent<AttributeSet>();

        if (attributeSet == null) return false;

        float current   = attributeSet.GetAttributeValue(CurrentAttribute.Value);
        float max       = attributeSet.GetAttributeValue(MaxAttribute.Value);

        if (max <= 0) return false;

        return (current / max) <= Ratio.Value;
    }
}
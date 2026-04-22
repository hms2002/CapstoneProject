using System;
using UnityEngine;
using Unity.Behavior;
using UnityGAS;
using Action = Unity.Behavior.Action;

/// <summary>
/// 책임 :
/// - BT Action 노드가 공통 AI-ASC bridge만 통해 능력 실행 문맥에 접근하게 만든다.
/// - BT Action 구현이 AbilitySystem, TagSystem 직접 참조 대신 bridge 해석 결과만 쓰도록 강제하는 최소 기반을 제공한다.
/// </summary>
public abstract class AIAbilityBridgeActionBase : Action
{
    protected bool TryResolveBridge(GameObject owner, out IAIAbilityBridge bridge)
    {
        bridge = AIAbilityBridgeResolver.Resolve(owner);
        return bridge != null;
    }
}

/// <summary>
/// 책임 :
/// - BT Condition 노드가 공통 AI-ASC bridge만 통해 상태 질의를 수행하게 만든다.
/// - BT Condition 구현이 AbilitySystem, TagSystem 직접 접근 없이 얕은 bridge 질의만 쓰도록 강제하는 최소 기반을 제공한다.
/// </summary>
public abstract class AIAbilityBridgeConditionBase : Condition
{
    protected bool TryResolveBridge(GameObject owner, out IAIAbilityBridge bridge)
    {
        bridge = AIAbilityBridgeResolver.Resolve(owner);
        return bridge != null;
    }
}

/// <summary>
/// 책임 :
/// - BT 노드가 대상 오브젝트에서 공통 AI-ASC bridge를 일관된 방식으로 찾도록 돕는다.
/// - BT가 AbilitySystem, TagSystem 구현 세부를 직접 알지 않게 만드는 최소 해석 규칙을 제공한다.
/// </summary>
internal static class AIAbilityBridgeResolver
{
    public static IAIAbilityBridge Resolve(GameObject owner)
    {
        if (owner == null)
            return null;

        MonoBehaviour[] behaviours = owner.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IAIAbilityBridge bridge)
                return bridge;
        }

        return null;
    }
}

/// <summary>
/// Action Node: GAS 스킬 실행 (비동기 대기 포함)
/// </summary>
[Serializable, Unity.Properties.GeneratePropertyBag]
[NodeDescription(name: "Activate GAS Ability", story: "Use [Ability] on [Target]", category: "GAS", id: "GAS_ActivateAbility")]
public class ActivateGASAbilityAction : AIAbilityBridgeActionBase
{
    [SerializeReference] public  BlackboardVariable<AbilityDefinition>  Ability;
    [SerializeReference] public BlackboardVariable<GameObject>          Target;

    private IAIAbilityBridge aiAbilityBridge;
    private bool isRunning;
    private bool isSuccess;

    protected override Status OnStart()
    {
        if (Ability.Value == null) return Status.Failure;

        if (!TryResolveBridge(this.GameObject, out aiAbilityBridge)) return Status.Failure;

        if (aiAbilityBridge.TryStartAbility(Ability.Value, Target.Value))
        {
            isRunning = true;
            isSuccess = true;
            return Status.Running;
        }

        return Status.Failure;
    }

    protected override Status OnUpdate()
    {
        if (!isRunning)
            return isSuccess ? Status.Success : Status.Failure;

        if (aiAbilityBridge != null && aiAbilityBridge.IsAbilityExecutionBusy)
            return Status.Running;

        isRunning = false;
        return isSuccess ? Status.Success : Status.Failure;
    }

    protected override void OnEnd()
    {
        isRunning = false;
        aiAbilityBridge = null;
    }
}

// --------------------------------------------------------------------------
// 2. Condition Node: 태그 보유 여부 체크

[Serializable, Unity.Properties.GeneratePropertyBag]
[NodeDescription(name: "Has GAS Tag", story: "Self has tag [TagName]", category: "GAS", id: "GAS_HasTag")]
public class HasGASTagCondition : AIAbilityBridgeConditionBase
{
    [SerializeReference] public BlackboardVariable<string>      TagName;
    [SerializeReference] public BlackboardVariable<GameObject>  Self;

    public override bool IsTrue()
    {
        if (Self.Value == null || string.IsNullOrEmpty(TagName.Value)) return false;

        int id = TagRegistry.GetIdByPath(TagName.Value);
        if (id == -1) return false;

        GameplayTag tag = TagRegistry.GetTag(id);
        if (!TryResolveBridge(Self.Value, out IAIAbilityBridge aiAbilityBridge)) return false;

        return aiAbilityBridge.HasStateTag(tag);
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

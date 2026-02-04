using System;
using UnityEngine;
using UnityGAS;
using Unity.Behavior;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Cast GAS Ability", story: "Witch casts [Ability] on Player", category: "Action", id: "Custom/CastGasAbility")]
public class Witch_CastAbility : Action
{
    [SerializeReference] public BlackboardVariable<WitchBoss> witchBoss;
    [SerializeReference] public BlackboardVariable<AbilityDefinition> AbilityToCast;

    protected override Status OnUpdate()
    {
        // 값이 연결 안 되어 있으면 실패 처리
        if (witchBoss.Value == null || AbilityToCast.Value == null) return Status.Failure;

        // WitchBoss의 함수 호출
        // Boss.Value가 실제 WitchBoss 컴포넌트를 가리킵니다.
        bool isCastStarted = witchBoss.Value.TryUseAbility(AbilityToCast.Value);

        if (isCastStarted)  return Status.Success; // 스킬 시전 성공! -> 다음 노드로
        else                return Status.Failure; // 쿨타임/그로기 등으로 실패 -> 다른 분기로
    }
}

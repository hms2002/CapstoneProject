using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[NodeDescription(name: "Random Selector", story: "Run Random Child", category: "Flow", id: "RandomSelector")]
public class BT_RandomSelector : Composite
{
    private Node currentChild;

    // 노드가 시작될 때 (랜덤 선택)
    protected override Status OnStart()
    {
        if (Children.Count == 0) return Status.Success;

        // 1. 랜덤으로 자식 하나 뽑기
        int index = UnityEngine.Random.Range(0, Children.Count);
        currentChild = Children[index];

        // 2. 해당 자식 실행 시작 (Update() 대신 StartNode 사용)
        StartNode(currentChild);

        // 3. 자식이 즉시 끝났는지, 실행 중인지 확인 후 반환
        return currentChild.CurrentStatus;
    }

    // 매 프레임 실행 (자식 상태 모니터링)
    protected override Status OnUpdate()
    {
        if (currentChild == null) return Status.Success;

        // 선택된 자식의 현재 상태를 그대로 반환 (Running, Success, Failure)
        return currentChild.CurrentStatus;
    }

    protected override void OnEnd()
    {
        currentChild = null;
    }
}
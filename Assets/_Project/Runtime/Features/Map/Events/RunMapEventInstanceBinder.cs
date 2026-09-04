using UnityEngine;

/// <summary>
/// 책임 : 이벤트 방 프리팹 안의 NPC/오브젝트가 생성된 방 문맥과 런 이벤트 진행 API를 인스펙터 이벤트로 사용할 수 있게 연결한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RunMapEventInstanceBinder : MonoBehaviour, IProceduralRoomRuntimeFeature
{
    [SerializeField] private RunMapEventDefinitionSO eventDefinition;
    [SerializeField] private bool markCompletedOnDisable;

    public RunMapEventDefinitionSO EventDefinition => eventDefinition;
    public ProceduralRoomRuntimeContext RoomContext { get; private set; }
    public int RoomPlacementId => RoomContext != null ? RoomContext.RoomPlacementId : -1;
    public string EventId => eventDefinition != null ? eventDefinition.EventId : string.Empty;

    public bool TryBindProceduralRoom(
        ProceduralRoomRuntimeContext context,
        out string failureReason)
    {
        RoomContext = context;
        failureReason = string.Empty;
        return true;
    }

    public void MarkCompleted()
    {
        RunMapEventProgress.MarkEventCompleted(RunSessionStore.Data, EventId);
    }

    public void QueueFollowUp(string followUpId)
    {
        if (eventDefinition == null || string.IsNullOrWhiteSpace(followUpId))
            return;

        RunMapEventProgress.QueueNextUnvisitedBossRouteFollowUp(eventDefinition, followUpId);
    }

    public void QueueFirstFollowUp()
    {
        if (eventDefinition == null || eventDefinition.FollowUps.Count == 0)
            return;

        RunMapEventFollowUpDefinition followUp = eventDefinition.FollowUps[0];
        if (followUp != null)
            QueueFollowUp(followUp.FollowUpId);
    }

    private void OnDisable()
    {
        if (markCompletedOnDisable)
            MarkCompleted();
    }
}

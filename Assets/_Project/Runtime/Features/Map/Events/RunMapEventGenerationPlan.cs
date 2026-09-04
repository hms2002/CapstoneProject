using System.Collections.Generic;

/// <summary>
/// 책임 : 던전 생성 전 계산된 이벤트 방 추가 목록과 생성 성공 후 커밋해야 할 런 상태 변경을 보관한다.
/// </summary>
public sealed class RunMapEventGenerationPlan
{
    private readonly GamePlayData data;
    private string visitedRouteThemeId;
    private bool shouldCommitRouteVisit;

    public RunMapEventGenerationPlan(
        GamePlayData data,
        List<RoomTemplateSO> guaranteedRoomTemplates)
    {
        this.data = data;
        GuaranteedRoomTemplates = guaranteedRoomTemplates ?? new List<RoomTemplateSO>();
    }

    public List<RoomTemplateSO> GuaranteedRoomTemplates { get; }
    public List<string> PresentedEventIds { get; } = new();
    public List<PendingRunMapEventPlacement> ConsumedPendingPlacements { get; } = new();

    public void SetRouteVisit(string routeThemeId, bool shouldCommitVisit)
    {
        visitedRouteThemeId = routeThemeId;
        shouldCommitRouteVisit = shouldCommitVisit;
    }

    public void AddGuaranteedRoom(RoomTemplateSO roomTemplate)
    {
        if (roomTemplate != null && !GuaranteedRoomTemplates.Contains(roomTemplate))
            GuaranteedRoomTemplates.Add(roomTemplate);
    }

    public void AddPresentedEvent(string eventId)
    {
        if (!string.IsNullOrWhiteSpace(eventId) && !PresentedEventIds.Contains(eventId))
            PresentedEventIds.Add(eventId);
    }

    public void AddConsumedPendingPlacement(PendingRunMapEventPlacement placement)
    {
        if (placement != null && !ConsumedPendingPlacements.Contains(placement))
            ConsumedPendingPlacements.Add(placement);
    }

    public void Commit()
    {
        if (data == null)
            return;

        if (shouldCommitRouteVisit)
            RunMapEventProgress.MarkBossRouteVisited(data, visitedRouteThemeId);

        for (int i = 0; i < PresentedEventIds.Count; i++)
            RunMapEventProgress.MarkEventPresented(data, PresentedEventIds[i]);

        if (ConsumedPendingPlacements.Count == 0)
            return;

        data.pendingRunMapEventPlacements ??= new List<PendingRunMapEventPlacement>();
        for (int i = 0; i < ConsumedPendingPlacements.Count; i++)
            data.pendingRunMapEventPlacements.Remove(ConsumedPendingPlacements[i]);
    }
}

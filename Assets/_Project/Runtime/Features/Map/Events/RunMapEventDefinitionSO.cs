using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 후속 복도에 강제로 배치할 이벤트 방 템플릿과 배치 타이밍을 데이터로 제공한다.
/// </summary>
[Serializable]
public sealed class RunMapEventFollowUpDefinition
{
    [SerializeField] private string followUpId;
    [SerializeField] private string displayName;
    [SerializeField] private RoomTemplateSO roomTemplate;
    [SerializeField] private RunMapEventFollowUpPlacementTiming placementTiming =
        RunMapEventFollowUpPlacementTiming.NextUnvisitedBossRoute;

    public string FollowUpId => followUpId;
    public string DisplayName => displayName;
    public RoomTemplateSO RoomTemplate => roomTemplate;
    public RunMapEventFollowUpPlacementTiming PlacementTiming => placementTiming;
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(followUpId) &&
        roomTemplate != null;
}

/// <summary>
/// 책임 : 후속 이벤트 방을 어느 복도 생성 시점에 끼워 넣을지 구분한다.
/// </summary>
public enum RunMapEventFollowUpPlacementTiming
{
    NextUnvisitedBossRoute = 0,
    ExplicitRouteThemeId = 1
}

/// <summary>
/// 책임 : 절차 이벤트 하나의 등장 조건, 선택 가중치, 시작 방과 후속 방 계약을 NPC 구현과 맵 생성기 사이에서 공유한다.
/// </summary>
[CreateAssetMenu(fileName = "RunMapEventDefinition", menuName = "Gameplay/Map Events/Event Definition")]
public sealed class RunMapEventDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string eventId;
    [SerializeField] private string displayName;

    [Header("Selection")]
    [SerializeField, Min(0f)] private float selectionWeight = 1f;
    [SerializeField] private bool allowRepeatInRun;
    [SerializeField] private bool requireBossRouteContext = true;
    [SerializeField, Min(1)] private int minimumBossRouteVisitOrder = 1;
    [SerializeField, Min(0)] private int maximumBossRouteVisitOrder = 0;

    [Header("Rooms")]
    [SerializeField] private RoomTemplateSO eventRoomTemplate;
    [SerializeField] private List<RunMapEventFollowUpDefinition> followUps = new();

    public string EventId => !string.IsNullOrWhiteSpace(eventId) ? eventId : name;
    public string DisplayName => displayName;
    public float SelectionWeight => Mathf.Max(0f, selectionWeight);
    public bool AllowRepeatInRun => allowRepeatInRun;
    public bool RequireBossRouteContext => requireBossRouteContext;
    public int MinimumBossRouteVisitOrder => Mathf.Max(1, minimumBossRouteVisitOrder);
    public int MaximumBossRouteVisitOrder => Mathf.Max(0, maximumBossRouteVisitOrder);
    public RoomTemplateSO EventRoomTemplate => eventRoomTemplate;
    public IReadOnlyList<RunMapEventFollowUpDefinition> FollowUps =>
        followUps ?? (IReadOnlyList<RunMapEventFollowUpDefinition>)Array.Empty<RunMapEventFollowUpDefinition>();

    public bool CanStartAtBossRouteVisit(int visitOrder, int maxBossRouteVisitCount)
    {
        int safeVisitOrder = Mathf.Max(1, visitOrder);
        if (safeVisitOrder < MinimumBossRouteVisitOrder)
            return false;

        int explicitMaxOrder = MaximumBossRouteVisitOrder;
        int resolvedMaxOrder = explicitMaxOrder > 0
            ? explicitMaxOrder
            : Mathf.Max(0, maxBossRouteVisitCount);
        return resolvedMaxOrder <= 0 || safeVisitOrder <= resolvedMaxOrder;
    }

    public bool TryGetFollowUp(string followUpId, out RunMapEventFollowUpDefinition followUp)
    {
        followUp = null;
        if (string.IsNullOrWhiteSpace(followUpId) || followUps == null)
            return false;

        for (int i = 0; i < followUps.Count; i++)
        {
            RunMapEventFollowUpDefinition candidate = followUps[i];
            if (candidate != null &&
                string.Equals(candidate.FollowUpId, followUpId, StringComparison.Ordinal))
            {
                followUp = candidate;
                return true;
            }
        }

        return false;
    }
}

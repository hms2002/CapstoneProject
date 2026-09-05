using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 절차 이벤트가 GamePlayData의 런 한정 방문·완료·후속 배치 상태를 안전하게 읽고 변경하게 한다.
/// </summary>
public static class RunMapEventProgress
{
    public static bool TryResolveCurrentBossRouteThemeId(out string routeThemeId)
    {
        routeThemeId = null;
        CorridorBossRouteSetSO stageSet = RunRoutePlayback.CurrentStageSet;
        if (stageSet == null)
            return false;

        string candidate = stageSet.StableThemeId;
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        routeThemeId = candidate;
        return true;
    }

    public static bool HasVisitedBossRoute(GamePlayData data, string routeThemeId)
    {
        if (data?.visitedRunMapEventRouteThemeIds == null || string.IsNullOrWhiteSpace(routeThemeId))
            return false;

        return data.visitedRunMapEventRouteThemeIds.Exists(
            candidate => string.Equals(candidate, routeThemeId, StringComparison.Ordinal));
    }

    public static int GetBossRouteVisitOrder(GamePlayData data, string routeThemeId)
    {
        if (data?.visitedRunMapEventRouteThemeIds == null || string.IsNullOrWhiteSpace(routeThemeId))
            return 0;

        for (int i = 0; i < data.visitedRunMapEventRouteThemeIds.Count; i++)
        {
            if (string.Equals(data.visitedRunMapEventRouteThemeIds[i], routeThemeId, StringComparison.Ordinal))
                return i + 1;
        }

        return 0;
    }

    public static int GetNextBossRouteVisitOrder(GamePlayData data, string routeThemeId)
    {
        if (string.IsNullOrWhiteSpace(routeThemeId))
            return 0;

        int existingOrder = GetBossRouteVisitOrder(data, routeThemeId);
        if (existingOrder > 0)
            return existingOrder;

        if (data == null)
            return 1;

        data.visitedRunMapEventRouteThemeIds ??= new List<string>();
        return data.visitedRunMapEventRouteThemeIds.Count + 1;
    }

    public static bool MarkBossRouteVisited(GamePlayData data, string routeThemeId)
    {
        if (data == null || string.IsNullOrWhiteSpace(routeThemeId))
            return false;

        data.visitedRunMapEventRouteThemeIds ??= new List<string>();
        if (HasVisitedBossRoute(data, routeThemeId))
            return false;

        data.visitedRunMapEventRouteThemeIds.Add(routeThemeId);
        return true;
    }

    public static bool WasEventPresented(GamePlayData data, string eventId)
    {
        return ContainsId(data?.presentedRunMapEventIds, eventId);
    }

    public static bool IsEventCompleted(GamePlayData data, string eventId)
    {
        return ContainsId(data?.completedRunMapEventIds, eventId);
    }

    public static void MarkEventPresented(GamePlayData data, string eventId)
    {
        if (data == null)
            return;

        data.presentedRunMapEventIds ??= new List<string>();
        AddUniqueId(data.presentedRunMapEventIds, eventId);
    }

    public static void MarkEventCompleted(GamePlayData data, string eventId)
    {
        if (data == null)
            return;

        data.completedRunMapEventIds ??= new List<string>();
        AddUniqueId(data.completedRunMapEventIds, eventId);
    }

    public static bool QueueNextUnvisitedBossRouteFollowUp(
        RunMapEventDefinitionSO definition,
        string followUpId,
        int payloadCount = 1)
    {
        if (definition == null ||
            !definition.TryGetFollowUp(followUpId, out RunMapEventFollowUpDefinition followUp) ||
            followUp == null ||
            followUp.PlacementTiming != RunMapEventFollowUpPlacementTiming.NextUnvisitedBossRoute ||
            !TryResolveCurrentBossRouteThemeId(out string sourceRouteThemeId))
        {
            return false;
        }

        return QueuePendingPlacement(
            RunSessionStore.Data,
            definition.EventId,
            followUp.FollowUpId,
            sourceRouteThemeId,
            targetRouteThemeId: null,
            consumeOnNextUnvisitedRoute: true,
            payloadCount);
    }

    public static bool QueueNextUnvisitedBossRouteFollowUp(
        string eventId,
        string followUpId,
        int payloadCount = 1)
    {
        if (string.IsNullOrWhiteSpace(eventId) ||
            string.IsNullOrWhiteSpace(followUpId) ||
            !TryResolveCurrentBossRouteThemeId(out string sourceRouteThemeId))
        {
            return false;
        }

        return QueuePendingPlacement(
            RunSessionStore.Data,
            eventId,
            followUpId,
            sourceRouteThemeId,
            targetRouteThemeId: null,
            consumeOnNextUnvisitedRoute: true,
            payloadCount);
    }

    public static bool QueueExplicitRouteFollowUp(
        RunMapEventDefinitionSO definition,
        string followUpId,
        string targetRouteThemeId,
        int payloadCount = 1)
    {
        if (definition == null ||
            string.IsNullOrWhiteSpace(targetRouteThemeId) ||
            !definition.TryGetFollowUp(followUpId, out RunMapEventFollowUpDefinition followUp) ||
            followUp == null)
        {
            return false;
        }

        return QueuePendingPlacement(
            RunSessionStore.Data,
            definition.EventId,
            followUp.FollowUpId,
            sourceRouteThemeId: null,
            targetRouteThemeId,
            consumeOnNextUnvisitedRoute: false,
            payloadCount);
    }

    public static void ClearRunMapEventState(GamePlayData data)
    {
        if (data == null)
            return;

        data.visitedRunMapEventRouteThemeIds ??= new List<string>();
        data.visitedRunMapEventRouteThemeIds.Clear();
        data.presentedRunMapEventIds ??= new List<string>();
        data.presentedRunMapEventIds.Clear();
        data.completedRunMapEventIds ??= new List<string>();
        data.completedRunMapEventIds.Clear();
        data.pendingRunMapEventPlacements ??= new List<PendingRunMapEventPlacement>();
        data.pendingRunMapEventPlacements.Clear();
    }

    private static bool QueuePendingPlacement(
        GamePlayData data,
        string eventId,
        string followUpId,
        string sourceRouteThemeId,
        string targetRouteThemeId,
        bool consumeOnNextUnvisitedRoute,
        int payloadCount)
    {
        if (data == null ||
            string.IsNullOrWhiteSpace(eventId) ||
            string.IsNullOrWhiteSpace(followUpId))
        {
            return false;
        }

        string normalizedSourceRouteThemeId = sourceRouteThemeId ?? string.Empty;
        string normalizedTargetRouteThemeId = targetRouteThemeId ?? string.Empty;
        data.pendingRunMapEventPlacements ??= new List<PendingRunMapEventPlacement>();
        PendingRunMapEventPlacement existing = data.pendingRunMapEventPlacements.Find(
            candidate =>
                candidate != null &&
                string.Equals(candidate.eventId, eventId, StringComparison.Ordinal) &&
                string.Equals(candidate.followUpId, followUpId, StringComparison.Ordinal) &&
                string.Equals(candidate.targetRouteThemeId, normalizedTargetRouteThemeId, StringComparison.Ordinal) &&
                candidate.consumeOnNextUnvisitedRoute == consumeOnNextUnvisitedRoute);

        int safePayloadCount = Mathf.Max(1, payloadCount);
        if (existing != null)
        {
            existing.payloadCount = Mathf.Max(existing.payloadCount, safePayloadCount);
            return true;
        }

        data.pendingRunMapEventPlacements.Add(
            new PendingRunMapEventPlacement(
                eventId,
                followUpId,
                normalizedSourceRouteThemeId,
                normalizedTargetRouteThemeId,
                consumeOnNextUnvisitedRoute,
                safePayloadCount));
        return true;
    }

    private static bool ContainsId(List<string> ids, string id)
    {
        if (ids == null || string.IsNullOrWhiteSpace(id))
            return false;

        return ids.Exists(candidate => string.Equals(candidate, id, StringComparison.Ordinal));
    }

    private static void AddUniqueId(List<string> ids, string id)
    {
        if (ids == null || string.IsNullOrWhiteSpace(id) || ContainsId(ids, id))
            return;

        ids.Add(id);
    }
}

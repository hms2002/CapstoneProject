using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 현재 런 방문 순서와 이벤트 상태를 기준으로 절차 생성기에 추가할 이벤트 방 템플릿 목록을 계산한다.
/// </summary>
public static class RunMapEventGenerationResolver
{
    public static RunMapEventGenerationPlan CreatePlan(
        RunMapEventGenerationProfileSO profile,
        IReadOnlyList<RoomTemplateSO> baseGuaranteedRoomTemplates,
        int seed)
    {
        var plan = new RunMapEventGenerationPlan(
            RunSessionStore.Data,
            ResolveBaseGuaranteedRooms(baseGuaranteedRoomTemplates));

        if (profile == null || !RunSessionStore.IsRunActive)
            return plan;

        bool hasRouteContext = RunMapEventProgress.TryResolveCurrentBossRouteThemeId(
            out string currentRouteThemeId);
        int nextVisitOrder = hasRouteContext
            ? RunMapEventProgress.GetNextBossRouteVisitOrder(RunSessionStore.Data, currentRouteThemeId)
            : 0;
        bool isFirstVisitToRoute = hasRouteContext &&
            !RunMapEventProgress.HasVisitedBossRoute(RunSessionStore.Data, currentRouteThemeId);

        if (hasRouteContext)
            plan.SetRouteVisit(currentRouteThemeId, shouldCommitVisit: isFirstVisitToRoute);

        AddPendingFollowUpRooms(
            profile,
            plan,
            currentRouteThemeId,
            isFirstVisitToRoute);
        AddStartEventRooms(
            profile,
            plan,
            seed,
            hasRouteContext,
            nextVisitOrder);

        if (profile.LogSelection)
            LogPlan(profile, plan, currentRouteThemeId, nextVisitOrder);

        return plan;
    }

    private static void AddPendingFollowUpRooms(
        RunMapEventGenerationProfileSO profile,
        RunMapEventGenerationPlan plan,
        string currentRouteThemeId,
        bool isFirstVisitToRoute)
    {
        List<PendingRunMapEventPlacement> pendingPlacements =
            RunSessionStore.Data?.pendingRunMapEventPlacements;
        if (pendingPlacements == null || pendingPlacements.Count == 0)
            return;

        for (int i = 0; i < pendingPlacements.Count; i++)
        {
            PendingRunMapEventPlacement pending = pendingPlacements[i];
            if (!ShouldConsumePendingPlacement(pending, currentRouteThemeId, isFirstVisitToRoute))
                continue;

            if (!TryResolveFollowUpRoom(profile, pending, out RoomTemplateSO roomTemplate))
                continue;

            plan.AddGuaranteedRoom(roomTemplate);
            plan.AddConsumedPendingPlacement(pending);
        }
    }

    private static void AddStartEventRooms(
        RunMapEventGenerationProfileSO profile,
        RunMapEventGenerationPlan plan,
        int seed,
        bool hasRouteContext,
        int nextVisitOrder)
    {
        int maxCount = Mathf.Max(
            0,
            profile.MaximumStartEventsPerCorridor - plan.ConsumedPendingPlacements.Count);
        if (maxCount <= 0)
            return;

        maxCount -= AddGuaranteedStartEventRooms(
            profile,
            plan,
            hasRouteContext,
            nextVisitOrder,
            maxCount);
        if (maxCount <= 0)
            return;

        var candidates = new List<RunMapEventDefinitionSO>();
        IReadOnlyList<RunMapEventDefinitionSO> definitions = profile.EventDefinitions;
        for (int i = 0; i < definitions.Count; i++)
        {
            RunMapEventDefinitionSO definition = definitions[i];
            if (!plan.PresentedEventIds.Contains(definition?.EventId) &&
                CanSelectStartEvent(profile, definition, hasRouteContext, nextVisitOrder))
            {
                candidates.Add(definition);
            }
        }

        if (candidates.Count == 0)
            return;

        var random = new System.Random(seed ^ 0x66D1A11);
        for (int i = 0; i < maxCount && candidates.Count > 0; i++)
        {
            RunMapEventDefinitionSO selected = SelectWeighted(candidates, random);
            if (selected == null)
                break;

            plan.AddGuaranteedRoom(selected.EventRoomTemplate);
            plan.AddPresentedEvent(selected.EventId);
            candidates.Remove(selected);
        }
    }

    private static int AddGuaranteedStartEventRooms(
        RunMapEventGenerationProfileSO profile,
        RunMapEventGenerationPlan plan,
        bool hasRouteContext,
        int nextVisitOrder,
        int maxCount)
    {
        if (profile == null || plan == null || maxCount <= 0)
            return 0;

        int addedCount = 0;
        IReadOnlyList<RunMapEventDefinitionSO> definitions =
            profile.GuaranteedStartEventDefinitions;
        for (int i = 0; i < definitions.Count && addedCount < maxCount; i++)
        {
            RunMapEventDefinitionSO definition = definitions[i];
            if (plan.PresentedEventIds.Contains(definition?.EventId) ||
                !CanForceStartEvent(profile, definition, hasRouteContext, nextVisitOrder))
            {
                continue;
            }

            plan.AddGuaranteedRoom(definition.EventRoomTemplate);
            plan.AddPresentedEvent(definition.EventId);
            addedCount++;
        }

        return addedCount;
    }

    private static bool CanForceStartEvent(
        RunMapEventGenerationProfileSO profile,
        RunMapEventDefinitionSO definition,
        bool hasRouteContext,
        int nextVisitOrder)
    {
        if (profile == null ||
            definition == null ||
            definition.EventRoomTemplate == null)
        {
            return false;
        }

        if (!definition.RequireBossRouteContext)
            return true;

        return hasRouteContext &&
               definition.CanStartAtBossRouteVisit(
                   nextVisitOrder,
                   profile.PlannedBossRouteVisitCount);
    }

    private static bool ShouldConsumePendingPlacement(
        PendingRunMapEventPlacement pending,
        string currentRouteThemeId,
        bool isFirstVisitToRoute)
    {
        if (pending == null || string.IsNullOrWhiteSpace(currentRouteThemeId))
            return false;

        if (!string.IsNullOrWhiteSpace(pending.targetRouteThemeId))
        {
            return string.Equals(
                pending.targetRouteThemeId,
                currentRouteThemeId,
                StringComparison.Ordinal);
        }

        return pending.consumeOnNextUnvisitedRoute &&
               isFirstVisitToRoute &&
               !string.Equals(
                   pending.sourceRouteThemeId,
                   currentRouteThemeId,
                   StringComparison.Ordinal);
    }

    private static bool TryResolveFollowUpRoom(
        RunMapEventGenerationProfileSO profile,
        PendingRunMapEventPlacement pending,
        out RoomTemplateSO roomTemplate)
    {
        roomTemplate = null;
        if (profile == null ||
            pending == null ||
            !profile.TryGetDefinition(pending.eventId, out RunMapEventDefinitionSO definition) ||
            definition == null ||
            !definition.TryGetFollowUp(pending.followUpId, out RunMapEventFollowUpDefinition followUp) ||
            followUp == null ||
            followUp.RoomTemplate == null)
        {
            return false;
        }

        roomTemplate = followUp.RoomTemplate;
        return true;
    }

    private static bool CanSelectStartEvent(
        RunMapEventGenerationProfileSO profile,
        RunMapEventDefinitionSO definition,
        bool hasRouteContext,
        int nextVisitOrder)
    {
        if (profile == null ||
            definition == null ||
            definition.EventRoomTemplate == null ||
            definition.SelectionWeight <= 0f)
        {
            return false;
        }

        GamePlayData data = RunSessionStore.Data;
        if (!definition.AllowRepeatInRun &&
            (RunMapEventProgress.WasEventPresented(data, definition.EventId) ||
             RunMapEventProgress.IsEventCompleted(data, definition.EventId)))
        {
            return false;
        }

        if (!definition.RequireBossRouteContext)
            return true;

        return hasRouteContext &&
               definition.CanStartAtBossRouteVisit(
                   nextVisitOrder,
                   profile.PlannedBossRouteVisitCount);
    }

    private static RunMapEventDefinitionSO SelectWeighted(
        IReadOnlyList<RunMapEventDefinitionSO> candidates,
        System.Random random)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        double totalWeight = 0d;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += Math.Max(0d, candidates[i]?.SelectionWeight ?? 0f);

        if (totalWeight <= 0d)
            return null;

        double roll = random.NextDouble() * totalWeight;
        for (int i = 0; i < candidates.Count; i++)
        {
            RunMapEventDefinitionSO candidate = candidates[i];
            roll -= Math.Max(0d, candidate?.SelectionWeight ?? 0f);
            if (roll <= 0d)
                return candidate;
        }

        return candidates[candidates.Count - 1];
    }

    private static List<RoomTemplateSO> ResolveBaseGuaranteedRooms(
        IReadOnlyList<RoomTemplateSO> baseGuaranteedRoomTemplates)
    {
        var rooms = new List<RoomTemplateSO>();
        if (baseGuaranteedRoomTemplates == null)
            return rooms;

        for (int i = 0; i < baseGuaranteedRoomTemplates.Count; i++)
        {
            RoomTemplateSO room = baseGuaranteedRoomTemplates[i];
            if (room != null && !rooms.Contains(room))
                rooms.Add(room);
        }

        return rooms;
    }

    private static void LogPlan(
        RunMapEventGenerationProfileSO profile,
        RunMapEventGenerationPlan plan,
        string routeThemeId,
        int visitOrder)
    {
        Debug.Log(
            $"[RunMapEventGeneration] profile={profile.name}, route={routeThemeId ?? "<none>"}, " +
            $"visitOrder={visitOrder}, guaranteedRooms={plan.GuaranteedRoomTemplates.Count}, " +
            $"presentedEvents={plan.PresentedEventIds.Count}, consumedFollowUps={plan.ConsumedPendingPlacements.Count}");
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 한 절차 복도 생성 프로필에서 선택 가능한 런 이벤트 풀과 방문 순서 정책을 보관한다.
/// </summary>
[CreateAssetMenu(fileName = "RunMapEventGenerationProfile", menuName = "Gameplay/Map Events/Generation Profile")]
public sealed class RunMapEventGenerationProfileSO : ScriptableObject
{
    [Header("Selection")]
    [SerializeField] private List<RunMapEventDefinitionSO> eventDefinitions = new();
    [SerializeField, Min(0)] private int maximumStartEventsPerCorridor = 1;
    [SerializeField, Min(1)] private int plannedBossRouteVisitCount = 3;

    [Header("Diagnostics")]
    [SerializeField] private bool logSelection;

    public IReadOnlyList<RunMapEventDefinitionSO> EventDefinitions =>
        eventDefinitions ?? (IReadOnlyList<RunMapEventDefinitionSO>)Array.Empty<RunMapEventDefinitionSO>();
    public int MaximumStartEventsPerCorridor => Mathf.Max(0, maximumStartEventsPerCorridor);
    public int PlannedBossRouteVisitCount => Mathf.Max(1, plannedBossRouteVisitCount);
    public bool LogSelection => logSelection;

    public bool TryGetDefinition(string eventId, out RunMapEventDefinitionSO definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(eventId) || eventDefinitions == null)
            return false;

        for (int i = 0; i < eventDefinitions.Count; i++)
        {
            RunMapEventDefinitionSO candidate = eventDefinitions[i];
            if (candidate != null &&
                string.Equals(candidate.EventId, eventId, StringComparison.Ordinal))
            {
                definition = candidate;
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    public void EditorConfigure(
        IReadOnlyList<RunMapEventDefinitionSO> configuredDefinitions,
        int configuredMaximumStartEventsPerCorridor,
        int configuredPlannedBossRouteVisitCount,
        bool configuredLogSelection)
    {
        eventDefinitions ??= new List<RunMapEventDefinitionSO>();
        eventDefinitions.Clear();
        if (configuredDefinitions != null)
        {
            for (int i = 0; i < configuredDefinitions.Count; i++)
            {
                RunMapEventDefinitionSO definition = configuredDefinitions[i];
                if (definition != null && !eventDefinitions.Contains(definition))
                    eventDefinitions.Add(definition);
            }
        }

        maximumStartEventsPerCorridor = Mathf.Max(0, configuredMaximumStartEventsPerCorridor);
        plannedBossRouteVisitCount = Mathf.Max(1, configuredPlannedBossRouteVisitCount);
        logSelection = configuredLogSelection;
    }
#endif
}

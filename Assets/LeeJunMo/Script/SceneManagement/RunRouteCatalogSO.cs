using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RunRouteCatalog",
    menuName = "Capstone/Scene Management/Run Route Catalog")]
public sealed class RunRouteCatalogSO : ScriptableObject
{
    [Header("Normal Route Sets")]
    [Tooltip("Set to 0 to skip normal routes and start directly from the final route set.")]
    [SerializeField, Min(0)] private int normalStageCount = 3;
    [SerializeField] private List<CorridorBossRouteSetSO> normalRouteSets = new();
    [SerializeField] private bool allowDuplicateNormalRoutes;

    [Header("Final Route Set")]
    [SerializeField] private CorridorBossRouteSetSO finalRouteSet;

    [Header("Hub Return")]
    [SerializeField] private string hubSceneName;
    [SerializeField] private string hubEntryPointId = "Default";

    public int NormalStageCount => normalStageCount;
    public IReadOnlyList<CorridorBossRouteSetSO> NormalRouteSets => normalRouteSets;
    public bool AllowDuplicateNormalRoutes => allowDuplicateNormalRoutes;
    public CorridorBossRouteSetSO FinalRouteSet => finalRouteSet;
    public string HubSceneName => hubSceneName;
    public string HubEntryPointId => hubEntryPointId;

    public bool HasEnoughNormalSets => GetValidNormalRouteSetCount() >= RequiredNormalRouteCount;
    public bool HasFinalRouteSet => finalRouteSet != null && finalRouteSet.IsValid;
    public int RequiredNormalRouteCount
    {
        get
        {
            if (normalStageCount <= 0)
                return 0;

            return allowDuplicateNormalRoutes ? 1 : normalStageCount;
        }
    }

    public bool TryCreateHubReturnRoute(TransitionType transitionType, out PortalRouteDecision route)
    {
        route = default;
        if (string.IsNullOrWhiteSpace(hubSceneName))
            return false;

        route = new PortalRouteDecision(
            hubSceneName,
            string.IsNullOrWhiteSpace(hubEntryPointId) ? "Default" : hubEntryPointId,
            transitionType);
        return true;
    }

    public int GetValidNormalRouteSetCount()
    {
        if (normalRouteSets == null || normalRouteSets.Count == 0)
            return 0;

        int validCount = 0;
        for (int i = 0; i < normalRouteSets.Count; i++)
        {
            var candidate = normalRouteSets[i];
            if (candidate != null && candidate.IsValid)
                validCount++;
        }

        return validCount;
    }
}

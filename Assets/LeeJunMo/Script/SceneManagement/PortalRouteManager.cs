using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PortalRouteManager : MonoBehaviour
{
    private sealed class PendingPortalPlan
    {
        public PendingPortalPlan(RunRouteCatalogSO catalog, List<CorridorBossRouteSetSO> stages)
        {
            Catalog = catalog;
            Stages = stages;
        }

        public RunRouteCatalogSO Catalog { get; }
        public List<CorridorBossRouteSetSO> Stages { get; }
    }

    public static PortalRouteManager Instance { get; private set; }
    public static event Action<PortalRouteManager> InstanceChanged;

    private static bool s_isQuitting;

    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool verboseLogging;
    [SerializeField] private List<CorridorBossRouteSetSO> activeRouteStages = new();
    [SerializeField, Min(0)] private int currentStageIndex;

    private readonly Dictionary<string, PendingPortalPlan> pendingPlansByPortalId = new();
    private RunRouteCatalogSO activeRouteCatalog;

    public event Action<PortalRouteManager> LoadWindowChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        var go = new GameObject(nameof(PortalRouteManager));
        go.AddComponent<PortalRouteManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InstanceChanged?.Invoke(this);

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            InstanceChanged?.Invoke(null);
        }
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public bool HasActivePlan =>
        activeRouteStages != null &&
        activeRouteStages.Count > 0 &&
        currentStageIndex >= 0 &&
        currentStageIndex < activeRouteStages.Count;

    public int CurrentStageIndex => currentStageIndex;
    public int TotalStageCount => activeRouteStages?.Count ?? 0;
    public RunRouteCatalogSO ActiveRouteCatalog => activeRouteCatalog;
    public CorridorBossRouteSetSO CurrentStageSet => HasActivePlan ? activeRouteStages[currentStageIndex] : null;
    public int NextStageIndex => HasActivePlan ? currentStageIndex + 1 : -1;
    public CorridorBossRouteSetSO NextStageSet =>
        HasActivePlan && currentStageIndex + 1 >= 0 && currentStageIndex + 1 < activeRouteStages.Count
            ? activeRouteStages[currentStageIndex + 1]
            : null;
    public RouteSetLoadManifestSO CurrentStageLoadManifest => CurrentStageSet != null ? CurrentStageSet.LoadManifest : null;
    public RouteSetLoadManifestSO NextStageLoadManifest => NextStageSet != null ? NextStageSet.LoadManifest : null;
    public LoadManifestSO ActiveRunCommonLoadManifest => activeRouteCatalog != null ? activeRouteCatalog.RunCommonLoadManifest : null;

    public bool TryGetActiveLoadWindow(
        out LoadManifestSO runCommonManifest,
        out RouteSetLoadManifestSO currentStageManifest,
        out RouteSetLoadManifestSO nextStageManifest)
    {
        runCommonManifest = ActiveRunCommonLoadManifest;
        currentStageManifest = CurrentStageLoadManifest;
        nextStageManifest = NextStageLoadManifest;
        return HasActivePlan;
    }

    public bool EnsurePendingPlan(ScenePortal portal)
    {
        if (!TryValidateStartPortal(portal, out var catalog))
            return false;

        if (HasActivePlan)
            return false;

        if (pendingPlansByPortalId.TryGetValue(portal.PortalId, out var existingPlan) &&
            existingPlan.Catalog == catalog)
        {
            return true;
        }

        if (!TryBuildRunPlan(catalog, out var stages))
            return false;

        pendingPlansByPortalId[portal.PortalId] = new PendingPortalPlan(catalog, stages);

        if (verboseLogging)
        {
            Debug.Log(
                $"[PortalRouteManager] Prepared pending run plan. portal={portal.name}, stageCount={stages.Count}, catalog={catalog.name}",
                portal);
        }

        return true;
    }

    public void ClearPlan()
    {
        activeRouteStages.Clear();
        pendingPlansByPortalId.Clear();
        activeRouteCatalog = null;
        currentStageIndex = 0;

        if (verboseLogging)
        {
            Debug.Log("[PortalRouteManager] Cleared active and pending run plans.", this);
        }

        RaiseLoadWindowChanged();
    }

    public bool CanResolveRoute(ScenePortal portal)
    {
        if (portal == null)
            return false;

        if (portal.PortalTransitionType == TransitionType.HubToRunStart)
        {
            if (HasActivePlan)
                return false;

            return EnsurePendingPlan(portal);
        }

        return TryResolveRoute(portal, out _);
    }

    public bool TryResolveRoute(ScenePortal portal, out PortalRouteDecision route)
    {
        route = default;

        if (portal == null)
            return false;

        switch (portal.PortalTransitionType)
        {
            case TransitionType.HubToRunStart:
                if (!TryActivatePendingPlan(portal))
                    return false;

                route = ResolveHubToRunStart();
                break;

            case TransitionType.CorridorToBoss:
                if (!HasActivePlan)
                    return false;

                route = ResolveCorridorToBoss();
                break;

            case TransitionType.BossToCorridor:
                if (!HasActivePlan)
                    return false;

                route = ResolveBossToCorridor();
                break;

            case TransitionType.ReturnToHubAfterRun:
                if (!HasActivePlan)
                    return false;

                route = ResolveReturnToHub();
                break;
        }

        if (!route.IsValid)
            return false;

        if (verboseLogging)
        {
            Debug.Log(
                $"[PortalRouteManager] Resolved route. transitionType={portal.PortalTransitionType}, stageIndex={currentStageIndex}, target={route.TargetSceneName}, entry={route.EntryPointId}",
                this);
        }

        return true;
    }

    public void NotifyTransitionConsumed(TransitionType transitionType)
    {
        if (!HasActivePlan)
            return;

        if (transitionType == TransitionType.BossToCorridor &&
            currentStageIndex + 1 < activeRouteStages.Count)
        {
            currentStageIndex++;

            if (verboseLogging)
            {
                Debug.Log(
                    $"[PortalRouteManager] Advanced to next stage. currentStageIndex={currentStageIndex}",
                    this);
            }

            RaiseLoadWindowChanged();
        }
    }

    private PortalRouteDecision ResolveHubToRunStart()
    {
        if (currentStageIndex != 0)
            return default;

        return activeRouteStages[0] != null &&
               activeRouteStages[0].TryCreateCorridorRoute(TransitionType.HubToRunStart, out var route)
            ? route
            : default;
    }

    private PortalRouteDecision ResolveCorridorToBoss()
    {
        var current = CurrentStageSet;
        return current != null && current.TryCreateBossRoute(TransitionType.CorridorToBoss, out var route)
            ? route
            : default;
    }

    private PortalRouteDecision ResolveBossToCorridor()
    {
        int nextStageIndex = currentStageIndex + 1;
        if (nextStageIndex < 0)
            return default;

        if (nextStageIndex >= activeRouteStages.Count)
            return ResolveReturnToHub();

        var next = activeRouteStages[nextStageIndex];
        return next != null && next.TryCreateCorridorRoute(TransitionType.BossToCorridor, out var route)
            ? route
            : default;
    }

    private PortalRouteDecision ResolveReturnToHub()
    {
        if (activeRouteCatalog == null || currentStageIndex != activeRouteStages.Count - 1)
            return default;

        return activeRouteCatalog.TryCreateHubReturnRoute(TransitionType.ReturnToHubAfterRun, out var route)
            ? route
            : default;
    }

    private bool TryActivatePendingPlan(ScenePortal portal)
    {
        if (!TryValidateStartPortal(portal, out _))
            return false;

        if (HasActivePlan)
            return false;

        if (!pendingPlansByPortalId.TryGetValue(portal.PortalId, out var pendingPlan))
        {
            if (!EnsurePendingPlan(portal))
                return false;

            if (!pendingPlansByPortalId.TryGetValue(portal.PortalId, out pendingPlan))
                return false;
        }

        activeRouteStages.Clear();
        activeRouteStages.AddRange(pendingPlan.Stages);
        activeRouteCatalog = pendingPlan.Catalog;
        currentStageIndex = 0;
        pendingPlansByPortalId.Clear();

        if (verboseLogging)
        {
            Debug.Log(
                $"[PortalRouteManager] Activated run plan. portal={portal.name}, catalog={activeRouteCatalog.name}, stageCount={activeRouteStages.Count}",
                portal);
        }

        RaiseLoadWindowChanged();

        return true;
    }

    private bool TryBuildRunPlan(RunRouteCatalogSO catalog, out List<CorridorBossRouteSetSO> stages)
    {
        stages = null;

        if (catalog == null)
        {
            Debug.LogError("[PortalRouteManager] RunRouteCatalogSO is not assigned.", this);
            return false;
        }

        if (!catalog.HasFinalRouteSet)
        {
            Debug.LogError($"[PortalRouteManager] Final route set is missing or invalid. catalog={catalog.name}", this);
            return false;
        }

        var validNormalRoutes = CollectValidNormalRoutes(catalog);
        if (validNormalRoutes.Count < catalog.RequiredNormalRouteCount)
        {
            Debug.LogError(
                $"[PortalRouteManager] Normal route sets are insufficient. catalog={catalog.name}, required={catalog.RequiredNormalRouteCount}, valid={validNormalRoutes.Count}, allowDuplicate={catalog.AllowDuplicateNormalRoutes}",
                this);
            return false;
        }

        stages = new List<CorridorBossRouteSetSO>(catalog.NormalStageCount + 1);

        if (catalog.AllowDuplicateNormalRoutes)
        {
            for (int i = 0; i < catalog.NormalStageCount; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, validNormalRoutes.Count);
                stages.Add(validNormalRoutes[randomIndex]);
            }
        }
        else
        {
            var candidates = new List<CorridorBossRouteSetSO>(validNormalRoutes);
            for (int i = 0; i < catalog.NormalStageCount; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, candidates.Count);
                stages.Add(candidates[randomIndex]);
                candidates.RemoveAt(randomIndex);
            }
        }

        stages.Add(catalog.FinalRouteSet);
        return true;
    }

    private static List<CorridorBossRouteSetSO> CollectValidNormalRoutes(RunRouteCatalogSO catalog)
    {
        var validNormalRoutes = new List<CorridorBossRouteSetSO>();
        var routes = catalog.NormalRouteSets;

        if (routes == null)
            return validNormalRoutes;

        for (int i = 0; i < routes.Count; i++)
        {
            var candidate = routes[i];
            if (candidate != null && candidate.IsValid)
                validNormalRoutes.Add(candidate);
        }

        return validNormalRoutes;
    }

    private bool TryValidateStartPortal(ScenePortal portal, out RunRouteCatalogSO catalog)
    {
        catalog = null;

        if (portal == null)
            return false;

        if (portal.PortalTransitionType != TransitionType.HubToRunStart)
            return false;

        catalog = portal.StartRunRouteCatalog;
        if (catalog != null)
            return true;

        Debug.LogError(
            $"[PortalRouteManager] Hub start portal is missing RunRouteCatalogSO. portal={portal.name}",
            portal);
        return false;
    }

    private void RaiseLoadWindowChanged()
    {
        LoadWindowChanged?.Invoke(this);
    }
}

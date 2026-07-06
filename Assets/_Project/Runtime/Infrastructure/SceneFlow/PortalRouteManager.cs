using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : 현재 런의 포탈 route plan, stage 진행도, 로딩 문맥을 보관하고 Gameplay route 계약의 backend로 동작하는 Infrastructure manager이다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PortalRouteManager : MonoBehaviour, IRunRouteBackend
{
    public readonly struct DebugTransitionEntry
    {
        public DebugTransitionEntry(float realtimeSeconds, string message)
        {
            RealtimeSeconds = realtimeSeconds;
            Message = message;
        }

        public float RealtimeSeconds { get; }
        public string Message { get; }
    }

    // 책임: 특정 포탈에 아직 확정 적용되지 않은 route catalog와 stage 목록을 보관한다.
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
    private const int MaxTransitionHistoryEntries = 32;

    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool verboseLogging;
    [SerializeField] private List<CorridorBossRouteSetSO> activeRouteStages = new();
    [SerializeField, Min(0)] private int currentStageIndex;

    private readonly Dictionary<string, PendingPortalPlan> pendingPlansByPortalId = new();
    private readonly List<DebugTransitionEntry> transitionHistory = new();
    private RunRouteCatalogSO activeRouteCatalog;

    public event Action<PortalRouteManager> LoadWindowChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting || Instance != null)
            return;

        EnsureInstance();
    }

    public static PortalRouteManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        PortalRouteManager existing = FindFirstObjectByType<PortalRouteManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        if (s_isQuitting)
            return null;

        var go = new GameObject(nameof(PortalRouteManager));
        return go.AddComponent<PortalRouteManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RunRoutePlayback.RegisterBackend(this);
        InstanceChanged?.Invoke(this);

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        RunRoutePlayback.UnregisterBackend(this);

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
    public string LastTransitionEvent =>
        transitionHistory.Count > 0 ? transitionHistory[transitionHistory.Count - 1].Message : "<none>";
    public TransitionType LastLoadPresentationTransitionType { get; private set; }
    public string LastLoadPresentationTargetSceneName { get; private set; }
    public string LastLoadPresentationEntryPointId { get; private set; }
    public float LastLoadPresentationRealtimeSeconds { get; private set; }

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

    public DebugTransitionEntry[] GetTransitionHistorySnapshot(int maxCount = 16)
    {
        int safeMaxCount = Mathf.Max(1, maxCount);
        int resultCount = Mathf.Min(safeMaxCount, transitionHistory.Count);
        var results = new DebugTransitionEntry[resultCount];
        for (int i = 0; i < resultCount; i++)
        {
            int sourceIndex = transitionHistory.Count - 1 - i;
            results[i] = transitionHistory[sourceIndex];
        }

        return results;
    }

    public static bool IsCorridorEntryTransition(TransitionType transitionType)
    {
        return transitionType == TransitionType.HubToRunStart ||
               transitionType == TransitionType.BossToCorridor;
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
        RecordTransitionEvent(
            $"Prepared pending plan. portal={portal.name}, catalog={catalog.name}, stages={stages.Count}");

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

        ClearLoadPresentationContext();
        RecordTransitionEvent("Cleared active and pending run plans.");
        RaiseLoadWindowChanged();
    }

    public void CompleteLoadPresentationContext(string reason = null)
    {
        if (LastLoadPresentationTransitionType == TransitionType.None &&
            string.IsNullOrWhiteSpace(LastLoadPresentationTargetSceneName) &&
            string.IsNullOrWhiteSpace(LastLoadPresentationEntryPointId))
        {
            return;
        }

        ClearLoadPresentationContext();

        if (!string.IsNullOrWhiteSpace(reason))
            RecordTransitionEvent(reason);
    }

    public void SeedDevelopmentPlan(RunRouteCatalogSO catalog, CorridorBossRouteSetSO currentStageSet, string sourceSceneName = null)
    {
        activeRouteStages.Clear();
        pendingPlansByPortalId.Clear();
        activeRouteCatalog = catalog;
        currentStageIndex = 0;

        if (TryBuildDevelopmentPlan(catalog, currentStageSet, out List<CorridorBossRouteSetSO> stages, out int stageIndex))
        {
            activeRouteStages.AddRange(stages);
            currentStageIndex = stageIndex;
        }
        else if (currentStageSet != null)
        {
            activeRouteStages.Add(currentStageSet);
        }

        ClearLoadPresentationContext();
        RecordTransitionEvent(
            $"Seeded development plan. scene={sourceSceneName ?? "<unknown>"}, stage={(currentStageSet != null ? currentStageSet.name : "<none>")}, catalog={(catalog != null ? catalog.name : "<none>")}, index={currentStageIndex + 1}/{Mathf.Max(1, activeRouteStages.Count)}");
        RaiseLoadWindowChanged();
    }

    private static bool TryBuildDevelopmentPlan(
        RunRouteCatalogSO catalog,
        CorridorBossRouteSetSO currentStageSet,
        out List<CorridorBossRouteSetSO> stages,
        out int stageIndex)
    {
        stages = null;
        stageIndex = -1;

        if (catalog == null || currentStageSet == null)
            return false;

        stages = new List<CorridorBossRouteSetSO>(catalog.NormalStageCount + 1);
        IReadOnlyList<CorridorBossRouteSetSO> normalRoutes = catalog.NormalRouteSets;
        if (normalRoutes != null)
        {
            for (int i = 0; i < normalRoutes.Count && stages.Count < catalog.NormalStageCount; i++)
            {
                CorridorBossRouteSetSO routeSet = normalRoutes[i];
                if (routeSet != null && routeSet.IsValid)
                    stages.Add(routeSet);
            }
        }

        if (catalog.FinalRouteSet != null && catalog.FinalRouteSet.IsValid)
            stages.Add(catalog.FinalRouteSet);

        for (int i = 0; i < stages.Count; i++)
        {
            if (!ReferenceEquals(stages[i], currentStageSet))
                continue;

            stageIndex = i;
            return true;
        }

        stages = null;
        stageIndex = -1;
        return false;
    }

    public bool CanResolveRoute(ScenePortal portal)
    {
        if (portal == null)
            return false;

        TransitionType effectiveTransitionType = ResolveEffectiveTransitionType(portal);
        if (effectiveTransitionType == TransitionType.HubToRunStart)
        {
            return TryPrepareHubStartPlan(portal);
        }

        return TryResolveRoute(portal, out _);
    }

#if UNITY_EDITOR
    public string GetDebugResolveStatus(ScenePortal portal)
    {
        if (portal == null)
            return "portal=null";

        bool hasManager = Instance != null;
        bool hasActivePlan = HasActivePlan;
        bool hasPendingPlan = pendingPlansByPortalId.ContainsKey(portal.PortalId);
        bool isRunActive = GamePlayDataManager.Instance != null &&
                           GamePlayDataManager.Instance.Data != null &&
                           GamePlayDataManager.Instance.Data.isRunActive;

        bool validStartPortal = TryValidateStartPortal(portal, out var catalog);
        List<CorridorBossRouteSetSO> stages = null;
        bool canBuildRunPlan = validStartPortal && TryBuildRunPlan(catalog, out stages);
        int stageCount = canBuildRunPlan && stages != null ? stages.Count : 0;

        TransitionType effectiveTransitionType = ResolveEffectiveTransitionType(portal);

        return
            $"manager={hasManager}, transition={portal.PortalTransitionType}, effectiveTransition={effectiveTransitionType}, validStartPortal={validStartPortal}, " +
            $"catalog={(catalog != null ? catalog.name : "<none>")}, hasActivePlan={hasActivePlan}, " +
            $"hasPendingPlan={hasPendingPlan}, isRunActive={isRunActive}, canBuildRunPlan={canBuildRunPlan}, " +
            $"stageCount={stageCount}, currentStageIndex={currentStageIndex}, totalStageCount={TotalStageCount}";
    }
#endif

    public bool TryResolveRoute(ScenePortal portal, out PortalRouteDecision route)
    {
        route = default;

        if (portal == null)
            return false;

        TransitionType effectiveTransitionType = ResolveEffectiveTransitionType(portal);

        switch (effectiveTransitionType)
        {
            case TransitionType.HubToRunStart:
                ClearStaleHubStartPlanIfNeeded();
                SetLoadPresentationContext(effectiveTransitionType, null, null);
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

        SetLoadPresentationContext(route.TransitionType, route.TargetSceneName, route.EntryPointId);

        if (verboseLogging)
        {
            Debug.Log(
                $"[PortalRouteManager] Resolved route. configuredTransitionType={portal.PortalTransitionType}, effectiveTransitionType={effectiveTransitionType}, stageIndex={currentStageIndex}, target={route.TargetSceneName}, entry={route.EntryPointId}",
                this);
        }

        RecordTransitionEvent(
            $"Resolved {effectiveTransitionType}. configured={portal.PortalTransitionType}, stage={currentStageIndex + 1}/{Mathf.Max(1, activeRouteStages.Count)}, target={route.TargetSceneName}, entry={route.EntryPointId}");
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

            RecordTransitionEvent(
                $"Consumed {transitionType}. advanced to stage {currentStageIndex + 1}/{activeRouteStages.Count}.");
            // SceneTransitionCoordinator refreshes the preload window after fade-out so
            // corridor loading is presented once, in the managed transition sequence.
            return;
        }

        RecordTransitionEvent(
            $"Consumed {transitionType}. stage={currentStageIndex + 1}/{Mathf.Max(1, activeRouteStages.Count)}.");
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

    private TransitionType ResolveEffectiveTransitionType(ScenePortal portal)
    {
        if (portal == null)
            return TransitionType.None;

        TransitionType configuredTransitionType = portal.PortalTransitionType;
        if (configuredTransitionType == TransitionType.HubToRunStart)
            return configuredTransitionType;

        if (!HasActivePlan)
            return configuredTransitionType;

        CorridorBossRouteSetSO currentStageSet = CurrentStageSet;
        if (currentStageSet == null)
            return configuredTransitionType;

        string portalSceneName = ResolvePortalSceneName(portal);
        if (string.IsNullOrWhiteSpace(portalSceneName))
            return configuredTransitionType;

        if (currentStageSet.MatchesCorridorScene(portalSceneName))
            return TransitionType.CorridorToBoss;

        if (!currentStageSet.MatchesBossScene(portalSceneName))
            return configuredTransitionType;

        return currentStageIndex + 1 < activeRouteStages.Count
            ? TransitionType.BossToCorridor
            : TransitionType.ReturnToHubAfterRun;
    }

    private static string ResolvePortalSceneName(ScenePortal portal)
    {
        if (portal == null)
            return null;

        var scene = portal.gameObject.scene;
        return scene.IsValid() ? scene.name : null;
    }

    private bool TryActivatePendingPlan(ScenePortal portal)
    {
        if (!TryValidateStartPortal(portal, out _))
            return false;

        ClearStaleHubStartPlanIfNeeded();

        if (HasActivePlan)
            return false;

        if (!pendingPlansByPortalId.TryGetValue(portal.PortalId, out var pendingPlan))
        {
            if (!TryPrepareHubStartPlan(portal))
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

        RecordTransitionEvent(
            $"Activated run plan. portal={portal.name}, catalog={activeRouteCatalog.name}, stages={activeRouteStages.Count}");
        // SceneTransitionCoordinator refreshes the preload window after fade-out so
        // run-start loading does not begin before the loading presentation is visible.

        return true;
    }

    private bool TryPrepareHubStartPlan(ScenePortal portal)
    {
        ClearStaleHubStartPlanIfNeeded();

        if (!TryValidateStartPortal(portal, out var catalog))
            return false;

        if (HasActivePlan)
            return false;

        if (pendingPlansByPortalId.TryGetValue(portal.PortalId, out var existingPlan) &&
            existingPlan.Catalog == catalog)
        {
            return true;
        }

        if (EnsurePendingPlan(portal))
            return true;

        if (!TryBuildRunPlan(catalog, out var stages))
            return false;

        pendingPlansByPortalId[portal.PortalId] = new PendingPortalPlan(catalog, stages);

        if (verboseLogging)
        {
            Debug.Log(
                $"[PortalRouteManager] Rebuilt pending run plan from start portal fallback. portal={portal.name}, catalog={catalog.name}, stages={stages.Count}",
                portal);
        }

        RecordTransitionEvent(
            $"Fallback prepared pending plan. portal={portal.name}, catalog={catalog.name}, stages={stages.Count}");
        return true;
    }

    private void ClearStaleHubStartPlanIfNeeded()
    {
        if (!HasActivePlan)
            return;

        GamePlayDataManager gameplay = GamePlayDataManager.Instance;
        bool isRunActive = gameplay != null && gameplay.Data != null && gameplay.Data.isRunActive;
        if (isRunActive)
            return;

        if (verboseLogging)
        {
            Debug.Log(
                "[PortalRouteManager] Cleared stale hub-start plan because no run is active.",
                this);
        }

        ClearPlan();
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
                $"[PortalRouteManager] Normal route sets are insufficient. catalog={catalog.name}, required={catalog.RequiredNormalRouteCount}, valid={validNormalRoutes.Count}, fixedOrder={catalog.UseFixedNormalRouteOrder}, allowDuplicate={catalog.AllowDuplicateNormalRoutes}",
                this);
            return false;
        }

        stages = new List<CorridorBossRouteSetSO>(catalog.NormalStageCount + 1);

        if (catalog.UseFixedNormalRouteOrder)
        {
            for (int i = 0; i < validNormalRoutes.Count && stages.Count < catalog.NormalStageCount; i++)
            {
                stages.Add(validNormalRoutes[i]);
            }
        }
        else if (catalog.AllowDuplicateNormalRoutes)
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

    private void ClearLoadPresentationContext()
    {
        SetLoadPresentationContext(TransitionType.None, null, null);
    }

    private void SetLoadPresentationContext(
        TransitionType transitionType,
        string targetSceneName,
        string entryPointId)
    {
        LastLoadPresentationTransitionType = transitionType;
        LastLoadPresentationTargetSceneName = string.IsNullOrWhiteSpace(targetSceneName)
            ? null
            : targetSceneName;
        LastLoadPresentationEntryPointId = string.IsNullOrWhiteSpace(entryPointId)
            ? null
            : entryPointId;
        LastLoadPresentationRealtimeSeconds = Time.realtimeSinceStartup;
    }

    private void RecordTransitionEvent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        transitionHistory.Add(new DebugTransitionEntry(Time.realtimeSinceStartup, message));
        if (transitionHistory.Count > MaxTransitionHistoryEntries)
            transitionHistory.RemoveRange(0, transitionHistory.Count - MaxTransitionHistoryEntries);
    }
}

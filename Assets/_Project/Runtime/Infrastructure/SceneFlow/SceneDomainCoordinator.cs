using System;
using System.Collections.Generic;
using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;

/// <summary>
/// 책임: 씬 로드 시 전역 도메인 서비스를 준비하고 에디터 직접 시작 보정 흐름을 조율한다.
/// </summary>
[DefaultExecutionOrder(-950)]
[DisallowMultipleComponent]
public sealed class SceneDomainCoordinator : MonoBehaviour
{
#if UNITY_EDITOR
    private static bool s_editorDirectStartDecisionMade;
    private static bool s_editorDirectGameplayStartActive;
    private static bool s_editorDirectBootstrapApplied;
    private static string s_editorDirectStartSceneName;
#endif

    public static SceneDomainCoordinator Instance { get; private set; }

    private Coroutine editorPostSceneBootstrapRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
#if UNITY_EDITOR
        ResetEditorDirectStartState();
#endif
        EnsureInstance();
        SceneDomainAppScopeServices.Ensure();
    }

    public static SceneDomainCoordinator EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        SceneDomainCoordinator existing = FindFirstObjectByType<SceneDomainCoordinator>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject root = new GameObject(nameof(SceneDomainCoordinator));
        Instance = root.AddComponent<SceneDomainCoordinator>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneDomainAppScopeServices.Ensure();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneDomainLoadDecision decision = SceneDomainScenePolicy.CreateLoadDecision(scene);
        if (!decision.ShouldProcess)
            return;

        SceneDomainAppScopeServices.Ensure();

#if UNITY_EDITOR
        DetectEditorDirectSceneStart(decision.SceneInfo);
#endif

        if (decision.RequiresTitleCleanup)
        {
            SceneDomainTitleCleanupScope.Cleanup();
            return;
        }

        if (decision.RequiresGameplaySessionScope)
            SceneDomainGameplaySessionScope.Ensure(decision.SceneInfo);

#if UNITY_EDITOR
        ApplyEditorDirectSceneBootstrap(decision.SceneInfo);

        if (editorPostSceneBootstrapRoutine != null)
            StopCoroutine(editorPostSceneBootstrapRoutine);
        editorPostSceneBootstrapRoutine = StartCoroutine(EditorPostSceneBootstrapRoutine(decision.SceneInfo));
#endif
    }

#if UNITY_EDITOR
    private IEnumerator EditorPostSceneBootstrapRoutine(SceneDomainSceneInfo sceneInfo)
    {
        if (!SceneDomainDevelopmentStartPolicy.ShouldRunPostSceneBootstrap(
                s_editorDirectGameplayStartActive,
                sceneInfo))
        {
            editorPostSceneBootstrapRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        PlayerInteractor2D player = null;
        while (elapsed < 1f)
        {
            player = PlayerRuntimeRegistry.CurrentPlayer != null
                ? PlayerRuntimeRegistry.CurrentPlayer
                : FindFirstObjectByType<PlayerInteractor2D>(FindObjectsInactive.Include);

            if (player != null)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Let scene-local startup presentations and DDOL listeners finish their first pass,
        // then normalize direct-start interaction state for editor iteration.
        yield return null;

        bool isHubScene = sceneInfo.IsHubScene;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            if (player != null)
                NormalizeDevelopmentPlayerState(player);

            if (isHubScene)
            {
                EnsureDevelopmentHubPortalsReady();

                if (player != null && HasDevelopmentHubPortalInteraction(player))
                    break;
            }
            else
            {
                break;
            }

            yield return null;
        }

        editorPostSceneBootstrapRoutine = null;
    }

    private static void ResetEditorDirectStartState()
    {
        s_editorDirectStartDecisionMade = false;
        s_editorDirectGameplayStartActive = false;
        s_editorDirectBootstrapApplied = false;
        s_editorDirectStartSceneName = null;
    }

    private static void DetectEditorDirectSceneStart(SceneDomainSceneInfo sceneInfo)
    {
        if (s_editorDirectStartDecisionMade || !Application.isPlaying)
            return;

        s_editorDirectStartDecisionMade = true;
        s_editorDirectGameplayStartActive = SceneDomainDevelopmentStartPolicy.IsDirectGameplayStart(sceneInfo);
        s_editorDirectStartSceneName = s_editorDirectGameplayStartActive ? sceneInfo.SceneName : null;
        s_editorDirectBootstrapApplied = false;
    }

    private static void ApplyEditorDirectSceneBootstrap(SceneDomainSceneInfo sceneInfo)
    {
        if (!s_editorDirectGameplayStartActive || s_editorDirectBootstrapApplied)
            return;

        s_editorDirectBootstrapApplied = true;

        GameDataManager gameDataManager =
            GameDataManager.Instance ??
            FindFirstObjectByType<GameDataManager>(FindObjectsInactive.Include);
        if (gameDataManager != null)
        {
            gameDataManager.LoadSlot(SceneDomainDevelopmentStartPolicy.DevelopmentDefaultSlotIndex);
            gameDataManager.EnsureData().hasInitializedProfile = true;
        }

        GamePlayDataManager gameplayManager =
            GamePlayDataManager.Instance ??
            FindFirstObjectByType<GamePlayDataManager>(FindObjectsInactive.Include);
        if (gameplayManager != null)
            gameplayManager.ResetForDevelopmentStart();

        PortalRouteManager routeManager = PortalRouteManager.Instance;
        routeManager?.ClearPlan();

        if (sceneInfo.IsHubScene)
            return;

        if (gameplayManager != null)
            gameplayManager.StartRun();

        SeedDevelopmentRouteContext(sceneInfo.SceneName, routeManager);
    }

    private static void SeedDevelopmentRouteContext(string sceneName, PortalRouteManager routeManager)
    {
        if (routeManager == null || string.IsNullOrWhiteSpace(sceneName))
            return;

        CorridorBossRouteSetSO stageSet = FindRouteSetForScene(sceneName);
        if (stageSet == null)
            return;

        RunRouteCatalogSO catalog = FindCatalogForRouteSet(stageSet);
        routeManager.SeedDevelopmentPlan(catalog, stageSet, sceneName);
    }

    private static CorridorBossRouteSetSO FindRouteSetForScene(string sceneName)
    {
        string[] paths = EditorAuthoringPlayback.FindAssetPaths("t:CorridorBossRouteSetSO");
        for (int i = 0; i < paths.Length; i++)
        {
            CorridorBossRouteSetSO stageSet = EditorAuthoringPlayback.LoadAssetAtPath<CorridorBossRouteSetSO>(paths[i]);
            if (stageSet == null)
                continue;

            if (stageSet.MatchesCorridorScene(sceneName) ||
                stageSet.MatchesBossScene(sceneName))
            {
                return stageSet;
            }
        }

        return null;
    }

    private static RunRouteCatalogSO FindCatalogForRouteSet(CorridorBossRouteSetSO routeSet)
    {
        if (routeSet == null)
            return null;

        string[] paths = EditorAuthoringPlayback.FindAssetPaths("t:RunRouteCatalogSO");
        for (int i = 0; i < paths.Length; i++)
        {
            RunRouteCatalogSO catalog = EditorAuthoringPlayback.LoadAssetAtPath<RunRouteCatalogSO>(paths[i]);
            if (catalog == null)
                continue;

            if (ReferenceEquals(catalog.FinalRouteSet, routeSet))
                return catalog;

            IReadOnlyList<CorridorBossRouteSetSO> normalRouteSets = catalog.NormalRouteSets;
            if (normalRouteSets == null)
                continue;

            for (int routeIndex = 0; routeIndex < normalRouteSets.Count; routeIndex++)
            {
                if (ReferenceEquals(normalRouteSets[routeIndex], routeSet))
                    return catalog;
            }
        }

        return null;
    }

    private static void NormalizeDevelopmentPlayerState(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        PlayerCinematicProtection protection = player.GetComponent<PlayerCinematicProtection>();
        protection?.ForceReleaseAll();

        GameplayTagSet blockControlTagSet =
            Resources.Load<GameplayTagSet>(SceneDomainDevelopmentStartPolicy.BlockControlByUiTagSetResourcePath);
        PlayerUIControlLockBridge uiLockBridge = player.GetComponent<PlayerUIControlLockBridge>();
        if (uiLockBridge != null && blockControlTagSet != null)
            uiLockBridge.ForceReleaseAll(blockControlTagSet);

        GameplayTag interactBlockedTag =
            Resources.Load<GameplayTag>(SceneDomainDevelopmentStartPolicy.InteractBlockedTagResourcePath);
        TagSystem tagSystem = player.GetComponent<TagSystem>();
        if (tagSystem != null && interactBlockedTag != null)
        {
            while (tagSystem.HasTag(interactBlockedTag))
                tagSystem.RemoveTag(interactBlockedTag, 1);
        }

        player.SetInteractState(InteractState.Idle);
    }

    private static void EnsureDevelopmentHubPortalsReady()
    {
        PortalRouteManager routeManager = PortalRouteManager.Instance;
        if (routeManager == null)
            return;

        routeManager.ClearPlan();

        ScenePortal[] portals = FindObjectsByType<ScenePortal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < portals.Length; i++)
        {
            ScenePortal portal = portals[i];
            if (portal == null || portal.PortalTransitionType != TransitionType.HubToRunStart)
                continue;

            routeManager.EnsurePendingPlan(portal);
        }
    }

    private static bool HasDevelopmentHubPortalInteraction(PlayerInteractor2D player)
    {
        if (player == null)
            return false;

        ScenePortal[] portals = FindObjectsByType<ScenePortal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < portals.Length; i++)
        {
            ScenePortal portal = portals[i];
            if (portal == null || portal.PortalTransitionType != TransitionType.HubToRunStart)
                continue;

            if (portal.CanInteract(player))
                return true;
        }

        return false;
    }
#endif
}

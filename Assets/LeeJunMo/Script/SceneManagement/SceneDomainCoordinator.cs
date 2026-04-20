using System;
using System.Collections.Generic;
using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityGAS;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-950)]
[DisallowMultipleComponent]
public sealed class SceneDomainCoordinator : MonoBehaviour
{
    private const string TitleSceneName = "TitleScene";
    private const string HubSceneName = "ProtoTypeHub";
    private const string DontDestroyOnLoadSceneName = "DontDestroyOnLoad";
    private const int DevelopmentDefaultSlotIndex = 0;
    private const string BlockControlByUiTagSetResourcePath = "Tags/TagSet/TS_BlockControlByUI";
    private const string InteractBlockedTagResourcePath = "Tags/State.Interact.Blocked";

#if UNITY_EDITOR
    private static bool s_editorDirectStartDecisionMade;
    private static bool s_editorDirectGameplayStartActive;
    private static bool s_editorDirectBootstrapApplied;
    private static string s_editorDirectStartSceneName;
    private static bool s_skipHubSpawnPresentationOnNextSpawn;
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
        EnsureAppScopeServices();
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
        EnsureAppScopeServices();
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
        if (!scene.IsValid())
            return;

        EnsureAppScopeServices();

#if UNITY_EDITOR
        DetectEditorDirectSceneStart(scene);
#endif

        if (IsTitleScene(scene))
        {
            CleanupGameplaySessionScope();
            return;
        }

        EnsureGameplaySessionScope(scene);

#if UNITY_EDITOR
        ApplyEditorDirectSceneBootstrap(scene);

        if (editorPostSceneBootstrapRoutine != null)
            StopCoroutine(editorPostSceneBootstrapRoutine);
        editorPostSceneBootstrapRoutine = StartCoroutine(EditorPostSceneBootstrapRoutine(scene));
#endif
    }

    private void CleanupGameplaySessionScope()
    {
        SoundManager.Instance?.StopMusic(0.2f);
        LoadingOverlayController.Instance?.ForceHidePresentation();
        PortalRouteManager.Instance?.ClearPlan();

        DestroyPersistentOfType<PauseMenuUI>();
        DestroyPersistentOfType<SettingsPanelUI>();
        DestroyPersistentOfType<KeyBindingPanelUI>();
        DestroyPersistentOfType<UIManager>();
        DestroyPersistentOfType<GlobalUIRoot>();
        DestroyPersistentOfType<CameraBootstrap>();
    }

    private static void EnsureGameplaySessionScope(Scene scene)
    {
        if (IsTitleScene(scene))
            return;

        CameraBootstrap.EnsureRuntimeRigForCurrentScene();
        CameraShakeService.EnsureInstance();
        RunRouteBgmService.EnsureInstance();
    }

    private static void EnsureAppScopeServices()
    {
        SceneFadeTransitionService.EnsureInstance(allowRuntimeFallback: true);
        LoadingOverlayController.EnsureInstance();
        PresentationPreloadService.EnsureInstance();
        PortalRouteManager.EnsureInstance();
        GamePlayDataManager.EnsureInstance();
        MouseCursorService.EnsureInstance();
    }

    public static bool ConsumeHubSpawnPresentationSkip()
    {
#if UNITY_EDITOR
        if (!s_skipHubSpawnPresentationOnNextSpawn)
            return false;

        s_skipHubSpawnPresentationOnNextSpawn = false;
        return true;
#else
        return false;
#endif
    }

    private static void DestroyPersistentOfType<T>() where T : Component
    {
        T[] instances = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < instances.Length; i++)
        {
            T instance = instances[i];
            if (instance == null)
                continue;

            if (!string.Equals(instance.gameObject.scene.name, DontDestroyOnLoadSceneName, StringComparison.Ordinal))
                continue;

            Destroy(instance.gameObject);
        }
    }

    private static bool IsTitleScene(Scene scene)
    {
        return string.Equals(scene.name, TitleSceneName, StringComparison.OrdinalIgnoreCase);
    }

#if UNITY_EDITOR
    private IEnumerator EditorPostSceneBootstrapRoutine(Scene scene)
    {
        if (!s_editorDirectGameplayStartActive || !scene.IsValid() || IsTitleScene(scene))
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

        bool isHubScene = IsHubSceneName(scene.name);
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
        s_skipHubSpawnPresentationOnNextSpawn = false;
    }

    private static void DetectEditorDirectSceneStart(Scene scene)
    {
        if (s_editorDirectStartDecisionMade || !Application.isPlaying)
            return;

        s_editorDirectStartDecisionMade = true;
        s_editorDirectGameplayStartActive = !IsTitleScene(scene);
        s_editorDirectStartSceneName = s_editorDirectGameplayStartActive ? scene.name : null;
        s_skipHubSpawnPresentationOnNextSpawn = false;
        s_editorDirectBootstrapApplied = false;
    }

    private static void ApplyEditorDirectSceneBootstrap(Scene scene)
    {
        if (!s_editorDirectGameplayStartActive || s_editorDirectBootstrapApplied)
            return;

        s_editorDirectBootstrapApplied = true;
        TitleProfileLaunchContext.Clear();

        GameDataManager gameDataManager =
            GameDataManager.Instance ??
            FindFirstObjectByType<GameDataManager>(FindObjectsInactive.Include);
        if (gameDataManager != null)
        {
            gameDataManager.LoadSlot(DevelopmentDefaultSlotIndex);
            gameDataManager.EnsureData().hasInitializedProfile = true;
        }

        GamePlayDataManager gameplayManager =
            GamePlayDataManager.Instance ??
            FindFirstObjectByType<GamePlayDataManager>(FindObjectsInactive.Include);
        if (gameplayManager != null)
            gameplayManager.ResetForDevelopmentStart();

        PortalRouteManager routeManager = PortalRouteManager.Instance;
        routeManager?.ClearPlan();

        if (IsHubSceneName(scene.name))
        {
            s_skipHubSpawnPresentationOnNextSpawn = true;
            return;
        }

        if (gameplayManager != null)
            gameplayManager.StartRun();

        SeedDevelopmentRouteContext(scene.name, routeManager);
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
        string[] guids = AssetDatabase.FindAssets("t:CorridorBossRouteSetSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            CorridorBossRouteSetSO stageSet = AssetDatabase.LoadAssetAtPath<CorridorBossRouteSetSO>(path);
            if (stageSet == null)
                continue;

            if (string.Equals(stageSet.CorridorSceneName, sceneName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stageSet.BossSceneName, sceneName, StringComparison.OrdinalIgnoreCase))
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

        string[] guids = AssetDatabase.FindAssets("t:RunRouteCatalogSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RunRouteCatalogSO catalog = AssetDatabase.LoadAssetAtPath<RunRouteCatalogSO>(path);
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

    private static bool IsHubSceneName(string sceneName)
    {
        return string.Equals(sceneName, HubSceneName, StringComparison.OrdinalIgnoreCase);
    }

    private static void NormalizeDevelopmentPlayerState(PlayerInteractor2D player)
    {
        if (player == null)
            return;

        PlayerCinematicProtection protection = player.GetComponent<PlayerCinematicProtection>();
        protection?.ForceReleaseAll();

        GameplayTagSet blockControlTagSet = Resources.Load<GameplayTagSet>(BlockControlByUiTagSetResourcePath);
        PlayerUIControlLockBridge uiLockBridge = player.GetComponent<PlayerUIControlLockBridge>();
        if (uiLockBridge != null && blockControlTagSet != null)
            uiLockBridge.ForceReleaseAll(blockControlTagSet);

        GameplayTag interactBlockedTag = Resources.Load<GameplayTag>(InteractBlockedTagResourcePath);
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

using System;
using CapstoneAudio;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class RunRouteBgmService : MonoBehaviour
{
    public static RunRouteBgmService Instance { get; private set; }

    private static bool s_isQuitting;

    [SerializeField, Min(0f)] private float sceneBgmFadeDuration = 0.5f;
    [SerializeField, Min(0f)] private float bossCombatBgmFadeDuration = 0.75f;
    [SerializeField] private bool verboseLogging;

    private SoundRef currentMusicRef;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (s_isQuitting)
            return;

        EnsureInstance();
    }

    public static RunRouteBgmService EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        RunRouteBgmService existing = FindAnyObjectByType<RunRouteBgmService>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject root = new GameObject(nameof(RunRouteBgmService));
        return root.AddComponent<RunRouteBgmService>();
    }

    public void NotifyBossCombatStarted()
    {
        CorridorBossRouteSetSO currentStage = PortalRouteManager.Instance != null
            ? PortalRouteManager.Instance.CurrentStageSet
            : null;

        if (currentStage == null || !currentStage.BossCombatBgm.IsSet)
            return;

        PlayMusicIfChanged(currentStage.BossCombatBgm, bossCombatBgmFadeDuration, "Boss combat started");
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
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSceneBgm(scene);
    }

    private void RefreshSceneBgm(Scene scene)
    {
        if (!scene.IsValid())
            return;

        if (TryResolveHubBgm(scene, out SoundRef hubBgm))
        {
            PlayMusicIfChanged(hubBgm, sceneBgmFadeDuration, $"Hub scene loaded ({scene.name})");
            return;
        }

        if (TryResolveCurrentStageCorridorBgm(scene, out SoundRef corridorBgm))
        {
            PlayMusicIfChanged(corridorBgm, sceneBgmFadeDuration, $"Corridor scene loaded ({scene.name})");
            return;
        }

        if (TryResolveBossScenePreCombatBgm(scene, out SoundRef carryOverBgm) &&
            !AreEquivalent(currentMusicRef, carryOverBgm))
        {
            PlayMusicIfChanged(carryOverBgm, sceneBgmFadeDuration, $"Boss scene pre-combat fallback ({scene.name})");
        }
    }

    private static bool TryResolveCurrentStageCorridorBgm(Scene scene, out SoundRef bgm)
    {
        bgm = default;

        CorridorBossRouteSetSO currentStage = PortalRouteManager.Instance != null
            ? PortalRouteManager.Instance.CurrentStageSet
            : null;

        if (currentStage == null)
            return false;

        if (!SceneNameEquals(scene.name, currentStage.CorridorSceneName))
            return false;

        if (!currentStage.CorridorBgm.IsSet)
            return false;

        bgm = currentStage.CorridorBgm;
        return true;
    }

    private static bool TryResolveBossScenePreCombatBgm(Scene scene, out SoundRef bgm)
    {
        bgm = default;

        CorridorBossRouteSetSO currentStage = PortalRouteManager.Instance != null
            ? PortalRouteManager.Instance.CurrentStageSet
            : null;

        if (currentStage == null)
            return false;

        if (!SceneNameEquals(scene.name, currentStage.BossSceneName))
            return false;

        if (!currentStage.CorridorBgm.IsSet)
            return false;

        bgm = currentStage.CorridorBgm;
        return true;
    }

    private static bool TryResolveHubBgm(Scene scene, out SoundRef bgm)
    {
        bgm = default;

        if (!scene.IsValid())
            return false;

        GameObject[] roots = scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameObject root = roots[rootIndex];
            if (root == null)
                continue;

            ScenePortal[] portals = root.GetComponentsInChildren<ScenePortal>(true);
            for (int portalIndex = 0; portalIndex < portals.Length; portalIndex++)
            {
                ScenePortal portal = portals[portalIndex];
                if (portal == null || portal.PortalTransitionType != TransitionType.HubToRunStart)
                    continue;

                RunRouteCatalogSO catalog = portal.StartRunRouteCatalog;
                if (catalog == null ||
                    !catalog.HubBgm.IsSet ||
                    !SceneNameEquals(scene.name, catalog.HubSceneName))
                {
                    continue;
                }

                bgm = catalog.HubBgm;
                return true;
            }
        }

        return false;
    }

    private void PlayMusicIfChanged(SoundRef soundRef, float fadeDuration, string reason)
    {
        if (!soundRef.IsSet)
            return;

        if (AreEquivalent(currentMusicRef, soundRef))
            return;

        SoundManager.EnsureInstance().PlayMusic(soundRef, fadeDuration);
        currentMusicRef = soundRef;

        if (verboseLogging)
        {
            Debug.Log(
                $"[RunRouteBgmService] Switched BGM. key={soundRef.key}, fade={fadeDuration}, reason={reason}",
                this);
        }
    }

    private static bool AreEquivalent(SoundRef left, SoundRef right)
    {
        return string.Equals(NormalizeKey(left.key), NormalizeKey(right.key), StringComparison.OrdinalIgnoreCase)
               && Mathf.Approximately(left.EffectiveVolumeMultiplier, right.EffectiveVolumeMultiplier);
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }

    private static bool SceneNameEquals(string left, string right)
    {
        return string.Equals(left, right, StringComparison.Ordinal);
    }
}
